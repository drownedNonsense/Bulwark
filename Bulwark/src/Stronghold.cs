using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;


namespace Bulwark {
    public class Stronghold {

        //=======================
        // D E F I N I T I O N S
        //=======================
            
            public string Name;
            public Cuboidi Area;
            public BlockPos Center;

            public string PlayerUID;
            public string PlayerName;

            public int? GroupUID;
            public string GroupName;

            private bool isClientInside;
            
            public HashSet<Entity> BesiegingEntities = new();
            public float SiegeIntensity;

            internal long? UpdateRef;
            
            public ICoreAPI Api;

            public bool IsClaimed => this.PlayerUID != null;


        //===============================
        // I M P L E M E N T A T I O N S
        //===============================

            public void Claim(IPlayer byPlayer) {

                this.PlayerUID      = byPlayer.PlayerUID;
                this.PlayerName = byPlayer.PlayerName;

                if (this.Api is ICoreServerAPI Sapi) {
                    Sapi.SendMessage(
                        byPlayer,
                        GlobalConstants.InfoLogChatGroup,
                        Lang.Get("You can use /stronghold name to name your claim"),
                        EnumChatType.Notification
                    ); // ..

                    if (this.Name is string claimName)
                        Sapi.SendMessageToGroup(
                            GlobalConstants.GeneralChatGroup,
                            Lang.Get("{0} captured {1}", this.PlayerName, claimName),
                            EnumChatType.Notification
                        ); // ..
                } // if ..
            } // void ..


            public void Unclaim(EnumUnclaimCause unclaimCause = EnumUnclaimCause.Server) {
                if (unclaimCause != EnumUnclaimCause.Server && this.Api is ICoreServerAPI Sapi) {
                    string message = Lang.Get((this.Name is not null ? "{0}" : "One of your claim") + unclaimCause switch {
                        EnumUnclaimCause.EmptyCellar => " has run out of food!",
                        EnumUnclaimCause.Player      => " has been captured!",
                        EnumUnclaimCause.FlagBroken  => " has been destroyed!",
                        _ => "",
                    }, this.Name); // ..

                    if (this.GroupUID is int groupUID) Sapi.SendMessageToGroup(groupUID, message, EnumChatType.Notification );
                    else Sapi.SendMessage(Sapi.World.PlayerByUid(this.PlayerUID), GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
                } // if ..

                this.PlayerUID  = null;
                this.PlayerName = null;
                this.GroupUID   = null;
                this.GroupName  = null;

            } // void ..


            public void ClaimGroup(PlayerGroup group) {

                this.GroupUID  = group.Uid;
                this.GroupName = group.Name;

                if (this.Api is ICoreServerAPI Sapi && this.Name is string claimName)
                    Sapi.SendMessageToGroup(
                        group.Uid,
                        Lang.Get("{0} now leagues with {1}", claimName, group.Name),
                        EnumChatType.Notification
                    ); // ..
            } // void ..


            public void UnclaimGroup() {
                if (this.Api is ICoreServerAPI Sapi && this.GroupUID is int groupUID && this.Name is string claimName)
                    Sapi.SendMessageToGroup(
                        groupUID,
                        Lang.Get("{0} no longer leagues with {1}", claimName, this.GroupName),
                        EnumChatType.Notification
                    ); // ..

                this.GroupUID  = null;
                this.GroupName = null;

            } // void ..


            public void IncreaseSiegeIntensity(
                float intensity,
                Entity byEntity = null
            ) {
                
                if (this.Api is ICoreServerAPI Sapi) {

                    int newBesiegingCount = this.BesiegingEntities.Count + (byEntity is not null ? 1 : 0);
                    float newIntensity    = this.SiegeIntensity + intensity;

                    if (newIntensity >= 1f && this.SiegeIntensity < 1f) {

                        string message = Lang.Get((this.Name is not null ? "{0}" : "One of your claim") + " is under attack!", this.Name);

                        if (this.GroupUID is int groupUID) Sapi.SendMessageToGroup(groupUID, message, EnumChatType.Notification );
                        else Sapi.SendMessage(Sapi.World.PlayerByUid(this.PlayerUID), GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
                        
                    } else if (newIntensity >= 2f && this.Name is string name) {

                        if (newBesiegingCount >= 2 && this.BesiegingEntities.Count < 2)      Sapi.SendMessageToGroup(GlobalConstants.GeneralChatGroup, Lang.Get("{0} is currently being besieged by a small band", name), EnumChatType.Notification);
                        else if (newBesiegingCount >= 4 && this.BesiegingEntities.Count < 4) Sapi.SendMessageToGroup(GlobalConstants.GeneralChatGroup, Lang.Get("{0} is currently being besieged by a medium sized army", name), EnumChatType.Notification);
                    
                    } // if ..


                    if (byEntity != null) this.BesiegingEntities.Add(byEntity);
                    this.SiegeIntensity += intensity;
                
                } // if ..
            } // void ..


            //---------
            // M A I N
            //---------

                internal void Update(float _) {
                    if (this.Api is ICoreClientAPI Capi) {
                        if (this.Name != null)
                            if (this.isClientInside)
                                this.isClientInside = this.Area.Contains(Capi.World.Player.Entity.Pos.AsBlockPos);

                            else if (this.Area.Contains(Capi.World.Player.Entity.Pos.AsBlockPos)) {

                                this.isClientInside = true;
                                Capi.TriggerIngameDiscovery(
                                    this,
                                    "stronghold-enter",
                                    this.PlayerUID is not null ? this.Name : Lang.Get("Ruins of {0}",
                                    this.Name)
                                ); // ..
                            } // if ..
                    } else {
                        this.SiegeIntensity = GameMath.Max(this.SiegeIntensity - 0.01f, 0f);
                        if (this.SiegeIntensity < 1f)
                            this.BesiegingEntities.Clear();

                    } // if ..
                } // void ..
    } // class ..
} // namespace ..
