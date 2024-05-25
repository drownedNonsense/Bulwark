using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;


namespace Bulwark;

public class BlockBehaviorFlag : BlockBehavior {

    #region Definitions

    protected ItemStack[] bannerStacks;
    public float ExpectancyBonus { get; protected set; }
    public float PoleTop         { get; protected set; }
    public float PoleBottom      { get; protected set; }

    #endregion    

    #region Initializations
    
    public override void Initialize(JsonObject properties) {
        base.Initialize(properties);
        this.ExpectancyBonus = properties["expectancyBonus"].AsFloat(0f);
        this.PoleTop         = properties["poleTop"].AsFloat(3f);
        this.PoleBottom      = properties["poleBottom"].AsFloat(2f);
    }

    public override void OnLoaded(ICoreAPI api) {
        base.OnLoaded(api);
        if (api.Side == EnumAppSide.Client)
        {
            this.bannerStacks = ObjectCacheUtil.GetOrCreate(api, "bannerStacks", delegate {
                List<ItemStack> bannerStacks = new ();
                Item[] banners = api.World.SearchItems(new AssetLocation("cloth-*"));
                
                foreach (Item banner in banners)
                    bannerStacks.AddRange(banner.GetHandBookStacks(api as ICoreClientAPI));

                return bannerStacks.ToArray();
            });
        }
    }

    #endregion
    
    public BlockBehaviorFlag(Block block) : base(block) {}

    #region Implementations

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(
        IWorldAccessor world,
        BlockSelection selection,
        IPlayer forPlayer,
        ref EnumHandling handling
    ) {
        return new WorldInteraction[3] {
            new () {
                ActionLangCode = "blockhelp-flag-pullup",
                MouseButton    = EnumMouseButton.Right,
            },
            new () {
                ActionLangCode = "blockhelp-flag-pulldown",
                MouseButton    = EnumMouseButton.Right,
                HotKeyCode     = "ctrl",
            },
            new () {
                ActionLangCode = "blockhelp-flag-set",
                MouseButton    = EnumMouseButton.Right,
                Itemstacks     = this.bannerStacks
            }
        };
    }


    public override bool OnBlockInteractStart(
        IWorldAccessor world,
        IPlayer byPlayer,
        BlockSelection blockSel,
        ref EnumHandling handling
    ) {
        handling = EnumHandling.PreventDefault;
        
        //TODO: Check the capture tentative result
        // show HUD error if it's not ok
        world.BlockAccessor
            .GetBlockEntity(blockSel.Position)?
            .GetBehavior<BlockEntityBehaviorFlag>()?
            .TryStartCapture(byPlayer);
        
        return true;
    }

    public override bool OnBlockInteractStep(
        float secondsUsed,
        IWorldAccessor world,
        IPlayer byPlayer,
        BlockSelection blockSel,
        ref EnumHandling handling
    ) {

        handling = EnumHandling.PreventSubsequent;
        return true;

    }

    public override void OnBlockInteractStop(
        float secondsUsed,
        IWorldAccessor world, 
        IPlayer byPlayer,
        BlockSelection blockSel,
        ref EnumHandling handling
    ) {

        handling = EnumHandling.PreventDefault;
        world.BlockAccessor
            .GetBlockEntity(blockSel.Position)?
            .GetBehavior<BlockEntityBehaviorFlag>()?
            .EndCapture();
    }

    public override bool OnBlockInteractCancel(
        float secondsUsed,
        IWorldAccessor world,
        IPlayer byPlayer,
        BlockSelection blockSel,
        ref EnumHandling handling
    ) {
        handling = EnumHandling.PreventDefault;
        world.BlockAccessor
            .GetBlockEntity(blockSel.Position)?
            .GetBehavior<BlockEntityBehaviorFlag>()?
            .EndCapture();

        return true;
    }

    #endregion
}
