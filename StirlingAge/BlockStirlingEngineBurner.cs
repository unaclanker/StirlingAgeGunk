/* Based on BlockCreativeRotor.cs and BlockFirepit.cs and BlockPulverizer.cs
   from vssurvivalmod */

using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

public class BlockStirlingEngineBurner : BlockMPBase, IIgnitable {

    public bool IsExtinct;

    private BlockFacing our_orientation;

    WorldInteraction[] interactions;

    public override void OnLoaded(ICoreAPI api) {
        base.OnLoaded(api);
        our_orientation = BlockFacing.FromFirstLetter(Variant["side"][0]);
        interactions = ObjectCacheUtil.GetOrCreate(api, "stirlingEngineInteractions", () => {
            List<ItemStack> canIgniteStacks = BlockBehaviorCanIgnite.CanIgniteStacks(api, true);

            return new WorldInteraction[] {
                new WorldInteraction() {
                    ActionLangCode = "blockhelp-firepit-open",
                    MouseButton = EnumMouseButton.Right,
                },
                new WorldInteraction() {
                    ActionLangCode = "blockhelp-firepit-ignite",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "shift",
                    Itemstacks = canIgniteStacks.ToArray(),
                    GetMatchingStacks = (wi, bs, es) => {
                        BlockEntityStirlingEngineBurner bef = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityStirlingEngineBurner;
                        if (bef?.fuelSlot != null && !bef.fuelSlot.Empty && !bef.IsBurning)
                        {
                            return wi.Itemstacks;
                        }
                        return null;
                    }
                },
                new WorldInteraction() {
                    ActionLangCode = "blockhelp-firepit-refuel",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "shift"
                }
            };
        });
    }

    public bool IsOrientedTo(BlockFacing facing)
    {
        return facing == our_orientation;
    }

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode) {
        if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) {
            return false;
        }

        // HACK: HorizontalOrientable should be doing this for us but it isn't!
        // also, we want to face away anyway
        BlockFacing[] horVer = Block.SuggestedHVOrientation(byPlayer, blockSel);
        horVer[0] = horVer[0].Opposite;
        if(our_orientation != horVer[0]) {
            Block b = api.World.BlockAccessor.GetBlock(CodeWithVariant("side", horVer[0].Code));
            if(b != null) {
                return b.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
            }
        }

        bool ok = base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        if (ok) {
            WasPlaced(world, blockSel.Position, null);
        }
        return ok;
    }

    EnumIgniteState IIgnitable.OnTryIgniteStack(EntityAgent byEntity, BlockPos pos, ItemSlot slot, float secondsIgniting)
    {
        BlockEntityStirlingEngineBurner burner = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityStirlingEngineBurner;
        if (burner == null) return EnumIgniteState.NotIgnitable;
        if (burner.IsBurning) return secondsIgniting > 2 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
        return EnumIgniteState.NotIgnitable;
    }
    public EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting) {
        BlockEntityStirlingEngineBurner burner = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityStirlingEngineBurner;
        if (burner == null) return EnumIgniteState.NotIgnitable;
        return burner.GetIgnitableState(secondsIgniting);
    }

    public void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling) {
        BlockEntityStirlingEngineBurner burner = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityStirlingEngineBurner;
        if (burner != null && !burner.canIgniteFuel)
        {
            burner.canIgniteFuel = true;
            burner.extinguishedTotalHours = api.World.Calendar.TotalHours;
        }

        handling = EnumHandling.PreventDefault;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
        ItemStack stack = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack;

        BlockEntityStirlingEngineBurner burner = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityStirlingEngineBurner;
        
        if (burner!=null && stack?.Block != null && stack.Block.HasBehavior<BlockBehaviorCanIgnite>() && burner.GetIgnitableState(0) == EnumIgniteState.Ignitable)
        {
            return false;
        }

        if (burner != null && stack != null && byPlayer.Entity.Controls.ShiftKey)
        {
            if (stack.Collectible.CombustibleProps != null && stack.Collectible.CombustibleProps.BurnTemperature > 0)
            {
                ItemStackMoveOperation op = new ItemStackMoveOperation(world, EnumMouseButton.Left, 0, EnumMergePriority.DirectMerge, 1);
                byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(burner.fuelSlot, ref op);
                if (op.MovedQuantity > 0)
                {
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                    var loc = stack.ItemAttributes?["placeSound"].Exists == true ? AssetLocation.Create(stack.ItemAttributes["placeSound"].AsString(), stack.Collectible.Code.Domain) : null;

                    if (loc != null)
                    {
                        api.World.PlaySoundAt(loc.WithPathPrefixOnce("sounds/"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, 0.88f + (float)api.World.Rand.NextDouble() * 0.24f, 16);
                    }

                    return true;
                }
            }
        }
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override void WasPlaced(IWorldAccessor world, BlockPos ownPos, BlockFacing connectedOnFacing)
    {
        base.WasPlaced(world, ownPos, connectedOnFacing);
        PlaceFakeBlock(world, ownPos);
    }

    private void PlaceFakeBlock(IWorldAccessor world, BlockPos pos)
    {
        AssetLocation loc = new AssetLocation("stirlingage:stirlingenginerotor-"+Variant["side"]);
        Block toPlaceBlock = world.GetBlock(loc);
        if(toPlaceBlock == null) {
            api.Logger.Log(EnumLogType.Error, "no block found for "+loc.ToString());
        }
        else {
            world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, pos.UpCopy());
        }
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        Block upBlock = api.World.BlockAccessor.GetBlock(pos.UpCopy());
        if (upBlock.Code.BeginsWith("stirlingage", "stirlingenginerotor-"))
        {
            world.BlockAccessor.SetBlock(0, pos.UpCopy());
        }

        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }

    public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
    {
        if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) return false;

        BlockSelection bs = blockSel.Clone();
        bs.Position = blockSel.Position.UpCopy();
        if (!base.CanPlaceBlock(world, byPlayer, bs, ref failureCode)) return false;

        return true;
    }

    public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) {
        
    }

    public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face) {
        return false;
    }
}