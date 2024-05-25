using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;


namespace Bulwark;

public class Stronghold
{
    #region Definitions

    public string Name;
    public Cuboidi Area;
    public BlockPos Center;

    public string PlayerUID;
    public string PlayerName;

    public int? GroupUID;
    public string GroupName;

    private int activeMembersAmount = 0;

    private bool isClientInside;

    public HashSet<Entity> BesiegingEntities = new();

    public float SiegeIntensity;

    internal long? UpdateRef;

    public ICoreAPI Api;

    private int UPDATE_EVENT_TICK_MS = 2000;
    private int UPDATE_EVENT_DELAY_OFFSET_MS = 1000;

    #endregion

    #region HelperDatasTypes

    [Flags]
    public enum StrongholdPrivilege
    {
        CanPlaceBlocks = 1,
        CanBreakBlocks = 2,
        //TODO: maybe add stronghold hierarchy privileges
    }

    public static Dictionary<StrongholdPrivilege, bool> MemberPrivileges()
    {
        return new() {
            { StrongholdPrivilege.CanBreakBlocks, true },
            { StrongholdPrivilege.CanPlaceBlocks, true },
        };
    }

    #endregion

    #region Implementations

    public bool IsClaimed => this.PlayerUID != null;

    public void Claim(IPlayer byPlayer)
    {
        this.PlayerUID = byPlayer.PlayerUID;
        this.PlayerName = byPlayer.PlayerName;

        if (this.Api is ICoreServerAPI Sapi)
        {
            Sapi.SendMessage(
                byPlayer,
                GlobalConstants.InfoLogChatGroup,
                Lang.Get("You can use /stronghold name to name your claim"),
                EnumChatType.Notification
            );

            if (this.Name is string claimName)
                Sapi.SendMessageToGroup(
                    GlobalConstants.GeneralChatGroup,
                    Lang.Get("{0} captured {1}", this.PlayerName, claimName),
                    EnumChatType.Notification
                );
        }
    }

    public void Unclaim(EnumUnclaimCause unclaimCause = EnumUnclaimCause.Server)
    {
        if (unclaimCause != EnumUnclaimCause.Server && this.Api is ICoreServerAPI Sapi)
        {
            string message = Lang.Get((this.Name is not null ? "{0}" : "One of your claim") + unclaimCause switch
            {
                EnumUnclaimCause.EmptyCellar => " has run out of food!",
                EnumUnclaimCause.Player => " has been captured!",
                EnumUnclaimCause.FlagBroken => " has been destroyed!",
                _ => "",
            }, this.Name);

            if (this.GroupUID is int groupUID) Sapi.SendMessageToGroup(groupUID, message, EnumChatType.Notification);
            else
                Sapi.SendMessage(Sapi.World.PlayerByUid(this.PlayerUID), GlobalConstants.GeneralChatGroup, message,
                    EnumChatType.Notification);
        }

        this.PlayerUID = null;
        this.PlayerName = null;
        this.GroupUID = null;
        this.GroupName = null;

    }

    public void ClaimGroup(PlayerGroup group)
    {

        this.GroupUID = group.Uid;
        this.GroupName = group.Name;

        if (this.Api is ICoreServerAPI Sapi && this.Name is string claimName)
            Sapi.SendMessageToGroup(
                group.Uid,
                Lang.Get("{0} now leagues with {1}", claimName, group.Name),
                EnumChatType.Notification
            );
    }

    public void UnclaimGroup()
    {
        if (this.Api is ICoreServerAPI Sapi && this.GroupUID is int groupUID && this.Name is string claimName)
            Sapi.SendMessageToGroup(
                groupUID,
                Lang.Get("{0} no longer leagues with {1}", claimName, this.GroupName),
                EnumChatType.Notification
            );

        this.GroupUID = null;
        this.GroupName = null;

    }

    public void IncreaseSiegeIntensity(
        float intensity,
        Entity byEntity = null
    )
    {
        if (this.Api is ICoreServerAPI Sapi)
        {
            int newBesiegingCount = this.BesiegingEntities.Count + (byEntity is not null ? 1 : 0);
            float newIntensity = this.SiegeIntensity + intensity;

            if (newIntensity >= 1f && this.SiegeIntensity < 1f)
            {

                string message = Lang.Get((this.Name is not null ? "{0}" : "One of your claim") + " is under attack!",
                    this.Name);

                if (this.GroupUID is int groupUID)
                    Sapi.SendMessageToGroup(groupUID, message, EnumChatType.Notification);
                else
                    Sapi.SendMessage(Sapi.World.PlayerByUid(this.PlayerUID), GlobalConstants.GeneralChatGroup, message,
                        EnumChatType.Notification);

            }
            else if (newIntensity >= 2f && this.Name is string name)
            {

                if (newBesiegingCount >= 2 && this.BesiegingEntities.Count < 2)
                    Sapi.SendMessageToGroup(GlobalConstants.GeneralChatGroup,
                        Lang.Get("{0} is currently being besieged by a small band", name), EnumChatType.Notification);
                else if (newBesiegingCount >= 4 && this.BesiegingEntities.Count < 4)
                    Sapi.SendMessageToGroup(GlobalConstants.GeneralChatGroup,
                        Lang.Get("{0} is currently being besieged by a medium sized army", name),
                        EnumChatType.Notification);

            }


            if (byEntity != null) this.BesiegingEntities.Add(byEntity);
            this.SiegeIntensity += intensity;
        }
    }

    internal void Update(float _)
    {
        if (this.Api is ICoreClientAPI Capi)
        {
            if (this.Name == null) return;

            if (this.isClientInside)
            {
                this.isClientInside = this.Area.Contains(Capi.World.Player.Entity.Pos.AsBlockPos);
            }
            else if (this.Area.Contains(Capi.World.Player.Entity.Pos.AsBlockPos))
            {

                this.isClientInside = true;
                Capi.TriggerIngameDiscovery(
                    this,
                    "stronghold-enter",
                    this.PlayerUID is not null ? this.Name : Lang.Get("Ruins of {0}", this.Name)
                );
            }
        }
        else
        {
            this.SiegeIntensity = GameMath.Max(this.SiegeIntensity - 0.01f, 0f);
            if (this.SiegeIntensity < 1f)
                this.BesiegingEntities.Clear();
        }
    }

    public void RegisterUpdateEvent()
    {
        this.UpdateRef = this.Api?
            .Event
            .RegisterGameTickListener(this.Update,
                UPDATE_EVENT_TICK_MS,
                UPDATE_EVENT_DELAY_OFFSET_MS);
    }

    public bool IsMember(IPlayer player) =>
        player.Groups.Any(g => g.GroupUid == this.GroupUID)
        || player.PlayerUID == this.PlayerUID;

    public bool HasActiveMembers() => activeMembersAmount > 0;

    public void UpdateActiveMembersAmount(IPlayer player, PlayerConnectionEvent evt)
    {
        if (!IsMember(player)) return;

        activeMembersAmount = evt switch {
            PlayerConnectionEvent.Joined => this.activeMembersAmount + 1,
            PlayerConnectionEvent.Disconnected => this.activeMembersAmount - 1,
            _ => activeMembersAmount
        };
    }

    public bool IsStrongholdTerritory(BlockSelection blockSel) =>
        this.Area.Contains(blockSel.Position);

    public Dictionary<StrongholdPrivilege, bool> TakePrivilege(IPlayer player)
    {
        Dictionary<StrongholdPrivilege, bool> privileges = new();
        if (this.PlayerUID is null) {
            privileges.AddRange(MemberPrivileges());
            return privileges;
        }
        
        if (IsMember(player)) {
            //TODO: put all hierarchy logic here
            privileges.AddRange(MemberPrivileges());
            return privileges;
        }

        if (!HasActiveMembers() && !this.IsClaimed) {
            privileges.AddRange(MemberPrivileges());
        }
        
        return privileges;
    }

    #endregion
}