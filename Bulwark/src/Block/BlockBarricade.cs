using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Bulwark {
    public class BlockBarricade : Block {

        //=======================
        // D E F I N I T I O N S
        //=======================

            protected ItemStack[] materialStacks;
            protected bool IsConstruct =>
                this.LastCodePart() switch {
                    "construct1" => true,
                    "construct2" => true,
                    "construct3" => true,
                    _            => false,
                }; // ..

            protected string NextStageCodePart =>
                this.LastCodePart() switch {
                    "construct1" => "construct2",
                    "construct2" => "construct3",
                    "construct3" => "built",
                    _            => "built",
                }; // ..


        //===============================
        // I M P L E M E N T A T I O N S
        //===============================

            public override WorldInteraction[] GetPlacedBlockInteractionHelp(
                IWorldAccessor world,
                BlockSelection selection,
                IPlayer forPlayer
            ) {
                this.materialStacks ??= this.GetMaterialStacks(world);
                return new WorldInteraction[] {
                    new WorldInteraction() {
                        ActionLangCode    = "blockhelp-barricade-build",
                        MouseButton       = EnumMouseButton.Right,
                        Itemstacks        = this.materialStacks,
                        GetMatchingStacks = (wi, bs, es) => {

                            if (this.IsConstruct) return wi.Itemstacks;
                            return null;
                            
                        } // ..
                    }, // ..
                }; // ..
            } // ..


            private ItemStack[] GetMaterialStacks(IWorldAccessor world) {
                if (this.materialStacks == null) {

                    Block[] blockMaterials = world.SearchBlocks(new AssetLocation(this.Attributes["barricadeMaterial"].AsString()));
                    Item[]  itemMaterials  = world.SearchItems(new AssetLocation(this.Attributes["barricadeMaterial"].AsString()));
                    this.materialStacks    = new ItemStack[blockMaterials.Length + itemMaterials.Length];
                    for (int i = 0; i < blockMaterials.Length; i++) this.materialStacks[i] = new ItemStack(blockMaterials[i]);
                    for (int i = 0; i < itemMaterials.Length; i++)  this.materialStacks[i + blockMaterials.Length] = new ItemStack(itemMaterials[i]);

                } // if ..

                return this.materialStacks;

            } // ItemStack[] ..


            public override bool OnBlockInteractStart(
                IWorldAccessor world,
                IPlayer byPlayer,
                BlockSelection blockSel
            ) {
                if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible is BlockSoil && TryConstruct(world, blockSel.Position, byPlayer)) {
                    if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                        byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);

                    return true;
                } // if ..

                return false;
            } // bool ..


            public override void OnBlockBroken(
                IWorldAccessor world,
                BlockPos pos,
                IPlayer byPlayer,
                float dropQuantityMultiplier = 1
            ) {
                if (this.Variant["state"] switch {
                    "built"   => "damaged",
                    "damaged" => "broken",
                    _         => null,
                } is string newState) world.BlockAccessor.SetBlock(world.BlockAccessor.GetBlock(this.CodeWithParts(newState)).Id, pos);
                else base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            } // void ..


            public override void OnBlockExploded(
                IWorldAccessor world,
                BlockPos pos,
                BlockPos explosionCenter,
                EnumBlastType blastType
            ) {
                if (this.Variant["state"] switch {
                    "built"   => "damaged",
                    "damaged" => "broken",
                    _         => null,
                } is string newState) world.BlockAccessor.SetBlock(world.BlockAccessor.GetBlock(this.CodeWithParts(newState)).Id, pos);
                base.OnBlockExploded(world, pos, explosionCenter, blastType);
            } // void ..


            private bool TryConstruct(
                IWorldAccessor world,
                BlockPos pos,
                IPlayer player
            ) {

                if (this.Variant["state"] == "built") return false;

                Block block = world.GetBlock(CodeWithParts(this.NextStageCodePart));
                world.BlockAccessor.ExchangeBlock(block.BlockId, pos);
                world.BlockAccessor.MarkBlockDirty(pos);
                if (block.Sounds != null) world.PlaySoundAt(block.Sounds.Place, pos.X, pos.Y, pos.Z, player);

                (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                return true;
            }
    } // class ..
} // namespace ..
