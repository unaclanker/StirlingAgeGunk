/* Based on BlockMPMultiblockPulverizer from vssurvivalmod */

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

public class BlockStirlingEngineRotor : BlockMPBase
{
    BlockFacing powerOutFacing;

    public override void OnLoaded(ICoreAPI api) {
        powerOutFacing = BlockFacing.FromCode(Variant["side"]).Opposite;
    }

    public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
    {
        IWorldAccessor world = player?.Entity?.World;
        if (world == null) world = api.World;
        BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEMPMultiblock;
        if (be == null || be.Principal == null) return 1f;  //never break
        Block principalBlock = world.BlockAccessor.GetBlock(be.Principal);
        if (api.Side == EnumAppSide.Client)
        {
            //Vintagestory.Client.SystemMouseInWorldInteractions mouse;
            //mouse.loadOrCreateBlockDamage(bs, principalBlock);
            //mouse.curBlockDmg.LastBreakEllapsedMs = game.ElapsedMilliseconds;
        }
        BlockSelection bs = blockSel.Clone();
        bs.Position = be.Principal;
        return principalBlock.OnGettingBroken(player, bs, itemslot, remainingResistance, dt, counter);
    }

    public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(world, blockPos, byItemStack);
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
    {
        // being broken by player: break the main block instead
        IBlockAccessor blockAccess = api.World.BlockAccessor;
        BlockPos downPos = pos.DownCopy();
        Block principalBlock = blockAccess.GetBlock(downPos);
        principalBlock.OnBlockBroken(world, downPos, byPlayer, dropQuantityMultiplier);

        // Need to trigger neighbourchange on client side only (because it's normally in the player block breaking code)
        if (api.Side == EnumAppSide.Client)
        {
            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                BlockPos npos = downPos.AddCopy(facing);
                world.BlockAccessor.GetBlock(npos).OnNeighbourBlockChange(world, npos, downPos);
            }
        }

        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }

    public override Cuboidf GetParticleBreakBox(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing)
    {
        // being broken by player: break the main block instead
        BlockPos downPos = pos.DownCopy();
        Block principalBlock = blockAccess.GetBlock(downPos);
        return principalBlock.GetParticleBreakBox(blockAccess, downPos, facing);
    }

    //Need to override because this fake block has no texture of its own (no texture gives black breaking particles)
    public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
    {
        IBlockAccessor blockAccess = capi.World.BlockAccessor;
        BlockPos downPos = pos.DownCopy();
        Block principalBlock = blockAccess.GetBlock(downPos);
        return principalBlock.GetRandomColor(capi, downPos, facing, rndIndex);
    }

    

    public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) {
        
    }

    public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face) {
        return face == powerOutFacing /*|| face == powerOutFacing.Opposite*/;
    }
}
