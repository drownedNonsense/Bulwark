using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;


namespace Bulwark {
    public class BlockEntityBehaviorLogistic : BlockEntityBehavior {

        //=======================
        // D E F I N I T I O N S
        //=======================

            public ItemStack Banner { get; protected set; }
            
            protected Stronghold stronghold;
            protected BlockEntityBehaviorFlag flag;
            
            public float CellarExpectancy { get; protected set; }

            private VerticalFlagRenderer renderer;

            protected BlockBehaviorLogistic blockBehavior;
            protected FortificationModSystem fortificationModSystem;
            private long? computeRef;
            private long? cellarRef;


        //===============================
        // I N I T I A L I Z A T I O N S
        //===============================

            public BlockEntityBehaviorLogistic(BlockEntity blockEntity) : base(blockEntity) {}

            public override void Initialize(ICoreAPI api, JsonObject properties) {

                base.Initialize(api, properties);

                this.blockBehavior          = this.Block.GetBehavior<BlockBehaviorLogistic>();
                this.computeRef             = api.Event.RegisterGameTickListener(this.ComputeCellar, 1000);
                this.cellarRef              = api.Event.RegisterGameTickListener(this.UpdateCellar, 6000);
                this.fortificationModSystem = this.Api.ModLoader.GetModSystem<FortificationModSystem>();

                this.fortificationModSystem.StrongholdAdded += this.OnStrongholdAdded;
                if (this.fortificationModSystem.TryGetStronghold(this.Pos, out this.stronghold)) {

                    this.flag = this.Api.World.BlockAccessor.GetBlockEntity(this.stronghold.Center)?.GetBehavior<BlockEntityBehaviorFlag>();
                    this.flag?.LogisticPoints.Add(this);

                } // if ..

                this.Banner?.ResolveBlockOrItem(this.Api.World);
                if (this.Banner != null && this.Api is ICoreClientAPI client) {

                    client.Tesselator.TesselateShape(this.Banner.Item, Shape.TryGet(client, "bulwark:shapes/flag/vertical-banner.json"), out MeshData meshData);
                    this.renderer = new VerticalFlagRenderer(client, meshData, this.Pos);
                    client.Event.RegisterRenderer(this.renderer, EnumRenderStage.Opaque, "flag");
                        
                } // if ..
            } // void ..


            public override void OnBlockBroken(IPlayer byPlayer = null) {
                base.OnBlockBroken(byPlayer);
                this.renderer?.Dispose();
                this.flag?.LogisticPoints.Remove(this);
                if (this.computeRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.computeRef.Value);
                if (this.cellarRef.HasValue)  this.Api.Event.UnregisterGameTickListener(this.cellarRef.Value);
                if(this.Banner != null) this.Api.World.SpawnItemEntity(this.Banner, this.Pos.ToVec3d());
            } // void ..


            public override void OnBlockRemoved() {
                base.OnBlockRemoved();
                this.renderer?.Dispose();
                this.flag?.LogisticPoints.Remove(this);
                if (this.computeRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.computeRef.Value);
                if (this.cellarRef.HasValue)  this.Api.Event.UnregisterGameTickListener(this.cellarRef.Value);
            } // void ..


            public override void OnBlockUnloaded() {
                base.OnBlockUnloaded();
                this.renderer?.Dispose();
                this.flag?.LogisticPoints.Remove(this);
                if (this.computeRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.computeRef.Value);
                if (this.cellarRef.HasValue)  this.Api.Event.UnregisterGameTickListener(this.cellarRef.Value);
            } // void ..


            public void OnStrongholdAdded(Stronghold stronghold) {
                if (stronghold.Area.Contains(this.Pos)) {

                    this.stronghold = stronghold;
                    this.flag       = this.Api.World.BlockAccessor.GetBlockEntity(this.stronghold.Center)?.GetBehavior<BlockEntityBehaviorFlag>();
                    this.flag?.LogisticPoints.Add(this);

                } // if ..
            } // ..


        //===============================
        // I M P L E M E N T A T I O N S
        //===============================

            public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
                base.GetBlockInfo(forPlayer, dsc);
                if (this.stronghold?.IsClaimed ?? false && this.Banner?.Collectible == this.flag?.Banner?.Collectible)
                    dsc.AppendLine(Lang.Get("+{0} stronghold expectancy", this.CellarExpectancy));
            } // void ..


            public void TryAttachBanner(IPlayer byPlayer) {

                ItemSlot itemSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                if (itemSlot.CanTake()
                    && this.Banner == null
                    && (itemSlot.Itemstack?.Collectible.Code.Path.Contains("cloth-") ?? false)
                ) {

                    this.Banner = itemSlot.TakeOut(1);
                    itemSlot.MarkDirty();
                    this.Blockentity.MarkDirty();

                    if (this.Api is ICoreClientAPI client) {
                    
                        this.renderer?.Dispose();
                        client.Tesselator.TesselateShape(this.Banner.Item, Shape.TryGet(client, "bulwark:shapes/flag/vertical-banner.json"), out MeshData meshData);
                        this.renderer = new VerticalFlagRenderer(client, meshData, this.Pos);
                        client.Event.RegisterRenderer(this.renderer, EnumRenderStage.Opaque, "flag");

                    } // if ..

                    this.Api.ModLoader.GetModSystem<FortificationModSystem>().TryGetStronghold(this.Pos, out this.stronghold);
                    if (this.stronghold?.Center != null) {

                        this.flag = this.Api.World.BlockAccessor.GetBlockEntity(this.stronghold?.Center).GetBehavior<BlockEntityBehaviorFlag>();
                        this.flag?.LogisticPoints.Add(this);

                    } // if ..

                    if (byPlayer is IServerPlayer serverPlayer
                        && this.flag != null
                        && this.Banner?.Collectible != this.flag.Banner?.Collectible
                    ) serverPlayer.SendIngameError("logisticpoint-wrongbanner");

                } else if(this.Banner != null) {
                    this.Api.World.SpawnItemEntity(this.Banner, this.Pos.ToVec3d());
                    this.Banner = null;
                    this.flag?.LogisticPoints.Remove(this);
                    this.renderer?.Dispose();
                } // if ..
            } // bool ..


            private void ComputeCellar(float _) {
                if (this.stronghold?.IsClaimed ?? false && this.Banner?.Collectible == this.flag?.Banner?.Collectible) {

                    List<BlockEntityContainer> cellars = new(4);
                    if (this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 1, 0,  0)) is BlockEntityContainer cellarA) cellars.Add(cellarA);
                    if (this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos(-1, 0,  0)) is BlockEntityContainer cellarB) cellars.Add(cellarB);
                    if (this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 0, 0,  1)) is BlockEntityContainer cellarC) cellars.Add(cellarC);
                    if (this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 0, 0, -1)) is BlockEntityContainer cellarD) cellars.Add(cellarD);

                    this.CellarExpectancy = 0f;

                    foreach(BlockEntityContainer cellar in cellars)
                        if (cellar != null)
                            foreach (ItemSlot itemSlot in cellar.Inventory)
                                if (itemSlot.Itemstack is ItemStack itemStack)
                                    if (itemStack.Collectible?.NutritionProps is FoodNutritionProperties foodNutrition)
                                        this.CellarExpectancy += foodNutrition.Satiety
                                            * itemStack.StackSize
                                            * (BulwarkModSystem.ClaimDurationPerSatiety * (1f + this.blockBehavior.ExpectancyBonus + this.flag.BlockBehavior.ExpectancyBonus));

                } // if ..
            } // void ..


            private void UpdateCellar(float deltaTime) {
                if (this.stronghold?.IsClaimed ?? false && this.Banner?.Collectible == this.flag?.Banner?.Collectible) {

                    float nowDurationPerSatiety = BulwarkModSystem.ClaimDurationPerSatiety * (1f + this.blockBehavior.ExpectancyBonus + this.flag.BlockBehavior.ExpectancyBonus);

                    float satiety       = 0f;
                    float targetSatiety = deltaTime / 86400f / this.Api.World.Calendar.SpeedOfTime / nowDurationPerSatiety;

                    BlockEntityContainer[] cellars = new BlockEntityContainer [4];
                    cellars[0] = this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 1, 0,  0));
                    cellars[1] = this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos(-1, 0,  0));
                    cellars[2] = this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 0, 0,  1));
                    cellars[3] = this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 0, 0, -1));

                    foreach(BlockEntityContainer cellar in cellars)
                        if (cellar != null)
                            foreach (ItemSlot itemSlot in cellar.Inventory)
                                if (satiety >= targetSatiety) return;
                                else if (itemSlot.Itemstack is ItemStack itemStack)
                                    if (itemStack.Collectible?.NutritionProps is FoodNutritionProperties foodNutrition) {

                                        int targetSize = GameMath.Min(
                                            itemStack.StackSize,
                                            GameMath.RoundRandom(this.Api.World.Rand, targetSatiety / (foodNutrition.Satiety * nowDurationPerSatiety)
                                            - satiety / (foodNutrition.Satiety * nowDurationPerSatiety))
                                        ); // ..

                                        satiety += foodNutrition.Satiety * targetSize * nowDurationPerSatiety;
                                        itemSlot.TakeOut(targetSize);
                                        itemSlot.MarkDirty();

                                    } // if ..
                } // if ..
            } // void ..


            //-------------------------------
            // T R E E   A T T R I B U T E S
            //-------------------------------

                public override void FromTreeAttributes(
                    ITreeAttribute tree,
                    IWorldAccessor worldForResolving
                ) {

                    this.CellarExpectancy = tree.GetFloat("cellarExpectancy");
                    this.Banner           = tree.GetItemstack("banner");

                    base.FromTreeAttributes(tree, worldForResolving);

                } // void ..


                public override void ToTreeAttributes(ITreeAttribute tree) {

                    if (this.Block != null)                              tree.SetString("forBlockCode", this.Block.Code.ToShortString());
                    if (this.Banner != null) tree.SetItemstack("banner", this.Banner); else tree.RemoveAttribute("banner");

                    tree.SetFloat("cellarExpectancy", this.CellarExpectancy);

                    base.ToTreeAttributes(tree);

                } // void ..
    } // class ..
} // namespace ..
