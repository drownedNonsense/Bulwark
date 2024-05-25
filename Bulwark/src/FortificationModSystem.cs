using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;


namespace Bulwark;


public class FortificationModSystem : ModSystem {

    #region Definitions

    HashSet<Stronghold> _strongholds = new();
    ICoreAPI api;

    public delegate void NewStrongholdDelegate(Stronghold stronghold);
        
    public event NewStrongholdDelegate StrongholdAdded;
    
    #endregion

    #region Initializations

    public override void Start(ICoreAPI api) {
            base.Start(api);
            this.api = api;
    }
    
    public override void StartServerSide(ICoreServerAPI api) {
        base.StartServerSide(api);
        
        var playerConnectionAction = (IPlayer player, PlayerConnectionEvent evt) => this._strongholds
            .Where(s => s.IsMember(player))
            .Foreach(s => s.UpdateActiveMembersAmount(player, evt));
        
        api.Event.DidPlaceBlock += this.PlaceBlockEvent;
        api.Event.DidBreakBlock += this.BreakBlockEvent;
        api.Event.PlayerDeath   += this.PlayerDeathEvent;            
        api.Event.PlayerDisconnect += p => playerConnectionAction(p, PlayerConnectionEvent.Disconnected);
        api.Event.PlayerJoin += p => playerConnectionAction(p, PlayerConnectionEvent.Joined);
        
        api.Event.CanPlaceOrBreakBlock += this.InactivityRaidBlocker;

        #region CommandsRegistration

        api.ChatCommands
            .Create("stronghold")
            .RequiresPrivilege(Privilege.chat)
            .BeginSubCommand("name")
                .WithDescription("Name the claimed area you are in")
                .WithArgs(api.ChatCommands.Parsers.Word("name"))
                .HandleWith(
                    args => {
                        string callerUID = args.Caller.Player.PlayerUID;
                        if (this._strongholds?.FirstOrDefault(
                            stronghold => stronghold.PlayerUID == callerUID
                            && stronghold.Area.Contains(args.Caller.Player.Entity.ServerPos.AsBlockPos),
                            null
                        ) is Stronghold area) {
                            
                            area.Name = args[0].ToString();
                            this.api.World.BlockAccessor.GetBlockEntity(area.Center).MarkDirty();

                        } else TextCommandResult.Success(Lang.Get("You're not in a stronghold you claimed"));
                        return TextCommandResult.Success();

                    } 
                ) 
            .EndSubCommand()
            .BeginSubCommand("league")
                .WithDescription("Affiliate the claimed area you are in with a group")
                .WithArgs(api.ChatCommands.Parsers.Word("group name"))
                .HandleWith(
                    args => {
                        string callerUID = args.Caller.Player.PlayerUID;
                        if (this._strongholds?.FirstOrDefault(
                            stronghold => stronghold.PlayerUID == callerUID
                            && stronghold.Area.Contains(args.Caller.Player.Entity.ServerPos.AsBlockPos),
                            null
                        ) is Stronghold area) {
                            if ((this.api as ICoreServerAPI).Groups.GetPlayerGroupByName(args[0].ToString()) is PlayerGroup playerGroup) {

                                area.ClaimGroup(playerGroup);
                                this.api.World.BlockAccessor.GetBlockEntity(area.Center).MarkDirty();

                            } else TextCommandResult.Success(Lang.Get("No such group found"));
                        } else TextCommandResult.Success(Lang.Get("You're not in a stronghold you claimed"));
                        return TextCommandResult.Success();

                    } 
                ) 
            .EndSubCommand()
            .BeginSubCommand("stopleague")
                .WithDescription("Stops the affiliation with a group")
                .WithArgs(api.ChatCommands.Parsers.Word("group name"))
                .HandleWith(
                    args => {
                        
                        string callerUID = args.Caller.Player.PlayerUID;
                        if (this._strongholds?.FirstOrDefault(
                            stronghold => stronghold.PlayerUID == callerUID
                            && stronghold.Area.Contains(args.Caller.Player.Entity.ServerPos.AsBlockPos),
                            null
                        ) is Stronghold area) {
                            if ((this.api as ICoreServerAPI).Groups.GetPlayerGroupByName(args[0].ToString()) is PlayerGroup playerGroup) {

                                area.UnclaimGroup();
                                this.api.World.BlockAccessor.GetBlockEntity(area.Center).MarkDirty();

                            } else TextCommandResult.Success(Lang.Get("No such group found"));
                        } else TextCommandResult.Success(Lang.Get("You're not in a stronghold you claimed"));
                        return TextCommandResult.Success();

                    } 
                );

        #endregion
    }

    #endregion


    #region Implementations
    
    private bool InactivityRaidBlocker(IServerPlayer plr, BlockSelection bs, out string claimant)
    {
        var privs = this.HasPrivilege(plr, bs, out Stronghold testedArea);
        var hasPermission = privs.ContainsKey(Stronghold.StrongholdPrivilege.CanBreakBlocks);
    
        claimant = null;
        if (!hasPermission) {
            claimant = $"Area owned by {testedArea.GroupName}";    
        }
        return hasPermission;
    }

    private void PlaceBlockEvent(
        IServerPlayer byPlayer,
        int oldblockId,
        BlockSelection blockSel,
        ItemStack withItemStack
    ) {
        if (blockSel == null || byPlayer == null) return;

        var canPlaceBlock = 
            this.HasPrivilege(byPlayer, blockSel, out _)
                .ContainsKey(Stronghold.StrongholdPrivilege.CanPlaceBlocks);
            
        if (!(withItemStack?.Collectible.Attributes?["siegeEquipment"]?.AsBool() ?? false) 
            && !canPlaceBlock) 
        {
            byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack = withItemStack;
            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
            byPlayer.Entity.World.BlockAccessor.SetBlock(oldblockId, blockSel.Position);
            byPlayer.SendIngameError("stronghold-nobuildprivilege");
        } 
    }

    private void BreakBlockEvent(
        IServerPlayer byPlayer,
        int oldblockId,
        BlockSelection blockSel
    )
    {
        if (blockSel == null || byPlayer == null) return;

        var priv = this.HasPrivilege(byPlayer, blockSel, out Stronghold stronghold);
        if (!priv.ContainsKey(Stronghold.StrongholdPrivilege.CanBreakBlocks)) {
            if (byPlayer.Entity.World.Calendar.TotalHours - byPlayer.Entity.WatchedAttributes.GetDouble("strongholdBreakWarning") < 1) {
                stronghold.IncreaseSiegeIntensity(0.5f);
            } else {
                byPlayer.Entity.WatchedAttributes.SetDouble("strongholdBreakWarning", byPlayer.Entity.World.Calendar.TotalHours);
                byPlayer.SendIngameError("stronghold-nobreakprivilege-warning");
            } 
        } 
    }

    private void PlayerDeathEvent(
        IServerPlayer forPlayer,
        DamageSource damageSource
    )
    {
        var st = this._strongholds.FirstOrDefault(area => area.IsMember(forPlayer), null);
        if (st is Stronghold stronghold) {

            Entity byEntity = damageSource.CauseEntity ?? damageSource.SourceEntity;

            if (byEntity is EntityPlayer playerCause
                && stronghold.Area.Contains(byEntity.ServerPos.AsBlockPos)
                && !(playerCause.Player.Groups?.Any(group => group.GroupUid == stronghold.GroupUID) ?? false
                    || playerCause.PlayerUID == stronghold.PlayerUID)
               ) stronghold.IncreaseSiegeIntensity(1f, byEntity);

            else if (byEntity.WatchedAttributes.GetString("guardedPlayerUid") is string playerUid
                     && this.api.World.PlayerByUid(playerUid) is IPlayer byPlayer
                     && stronghold.Area.Contains(byEntity.ServerPos.AsBlockPos)
                     && !(byPlayer.Groups?.Any(group => group.GroupUid == stronghold.GroupUID) ?? false
                         || byPlayer.PlayerUID == stronghold.PlayerUID)
                    ) stronghold.IncreaseSiegeIntensity(1f, damageSource.CauseEntity);
        } 
    }

    public bool TryRegisterStronghold(Stronghold stronghold) {
        stronghold.Api = this.api;
            
        if (this._strongholds.Contains(stronghold)) return true;
        if (this._strongholds.Any(x => x.Area.Intersects(stronghold.Area))) return false;
            
        this._strongholds.Add(stronghold);
        stronghold.RegisterUpdateEvent();

        this.StrongholdAdded?.Invoke(stronghold);
        return true;
    }

    public void RemoveStronghold(Stronghold stronghold) {
        if (stronghold is not null) {
            if (stronghold.UpdateRef.HasValue) stronghold.Api.Event.UnregisterGameTickListener(stronghold.UpdateRef.Value);
            this._strongholds.Remove(stronghold);
        }
    }

    public bool TryGetStronghold(BlockPos pos, out Stronghold value) {
        if (this._strongholds?.FirstOrDefault(stronghold => stronghold.Area.Contains(pos), null) is Stronghold area) {
            value = area;
            return true;
        } else  {
            value = null;
            return false;
        } 
    }

    public Dictionary<Stronghold.StrongholdPrivilege, bool> HasPrivilege(
        IPlayer byPlayer,
        BlockSelection blockSel,
        out Stronghold area
    ) {
        area = null;
        var privilege = Stronghold.MemberPrivileges();
        var strongholds = this._strongholds?
            .Where(s => s.IsStrongholdTerritory(blockSel));

        if (strongholds is null) return privilege;

        foreach (Stronghold stronghold in strongholds)
        {
            area = stronghold;
            privilege = stronghold.TakePrivilege(byPlayer);
        }
        return privilege;
    }

    #endregion
}
