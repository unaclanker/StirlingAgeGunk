/* Based on BlockEntityFirepit from vssurvivalmod */

using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

public class BlockEntityStirlingEngineBurner : BlockEntityOpenableContainer, IHeatSource, IFirePit, IStirlingBurner {
    internal InventoryStirlingEngineBurner inventory;
    // Temperature before the half second tick
    public float prevFurnaceTemperature = 20;
    // Current temperature of the furnace
    public float furnaceTemperature = 20;
    // Maximum temperature that can be reached with the currently used fuel
    public int maxTemperature;
    // How much of the current fuel is consumed
    public float fuelBurnTime;
    // How much fuel is available
    public float maxFuelBurnTime;
    // How much smoke the current fuel burns?
    public float smokeLevel;
    /// <summary>
    /// If true, then the fire pit is currently hot enough to ignite fuel
    /// </summary>
    public bool canIgniteFuel;

    public float cachedFuel;

    public double extinguishedTotalHours;

    public float HotSideTemperature => furnaceTemperature;
    public float ColdSideTemperature => enviromentTemperature();

    GuiDialogBlockEntityStirlingEngineBurner clientDialog;
    bool clientSidePrevBurning;

    #region Config

    public virtual bool BurnsAllFuell {
        get { return true; }
    }
    public virtual float HeatModifier {
        get { return 1f; }
    }
    public virtual float BurnDurationModifier {
        get { return 1f; }
    }

    public override string InventoryClassName {
        get { return "stove"; }
    }

    public virtual string DialogTitle {
        get { return Lang.Get("Stirling Engine"); }
    }

    public override InventoryBase Inventory {
        get { return inventory; }
    }

    #endregion


    public BlockEntityStirlingEngineBurner() {
        inventory = new InventoryStirlingEngineBurner(null, null);
    }

    public virtual int enviromentTemperature()
    {
        // TODO: world temperature
        return 20;
    }



    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        inventory.pos = Pos;
        inventory.LateInitialize("smelting-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);
        
        RegisterGameTickListener(OnBurnTick, 100);
        RegisterGameTickListener(On500msTick, 500);
    }

    public bool IsSmoldering => canIgniteFuel;

    public bool IsBurning {
        get { return this.fuelBurnTime > 0; }
    }


    public int getInventoryStackLimit() {
        return 64;
    }

    // Sync to client every 500ms
    private void On500msTick(float dt) {
        if (Api is ICoreServerAPI && (IsBurning || prevFurnaceTemperature != furnaceTemperature)) {
            MarkDirty();
        }

        prevFurnaceTemperature = furnaceTemperature;
    }

    Vec3d tmpPos = new Vec3d();

    private void OnBurnTick(float dt) {
        // Use up fuel
        if(fuelBurnTime > 0) {
            fuelBurnTime -= dt;
            if (fuelBurnTime <= 0) {
                fuelBurnTime = 0;
                maxFuelBurnTime = 0;
                if (!canSmelt()) // This check avoids light flicker when a piece of fuel is consumed and more is available
                {
                    setBlockState("extinct");
                    extinguishedTotalHours = Api.World.Calendar.TotalHours;
                }
            }
        }

        // Too cold to ignite fuel after 2 hours
        if (!IsBurning && Block.Variant["burnstate"] == "extinct" && Api.World.Calendar.TotalHours - extinguishedTotalHours > 2)
        {
            canIgniteFuel = false;
            setBlockState("cold");
        }

        // Furnace is burning: Heat furnace
        if (IsBurning)
        {
            furnaceTemperature = changeTemperature(furnaceTemperature, maxTemperature, dt);
        }

        // Furnace is not burning and can burn: Ignite the fuel
        if (!IsBurning && canIgniteFuel && canSmelt())
        {
            igniteFuel();
        }

        // Furnace is not burning: Cool down furnace and ore also turn of fire
        if (!IsBurning)
        {
            furnaceTemperature = changeTemperature(furnaceTemperature, enviromentTemperature(), dt);
        }

    }


    public EnumIgniteState GetIgnitableState(float secondsIgniting)
    {
        if (fuelSlot.Empty) return EnumIgniteState.NotIgnitablePreventDefault;
        if (IsBurning) return EnumIgniteState.NotIgnitablePreventDefault;

        return secondsIgniting > 3 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
    }




    public float changeTemperature(float fromTemp, float toTemp, float dt)
    {
        float diff = Math.Abs(fromTemp - toTemp);

        dt = dt + dt * (diff / 28);


        if (diff < dt)
        {
            return toTemp;
        }

        if (fromTemp > toTemp)
        {
            dt = -dt;
        }

        if (Math.Abs(fromTemp - toTemp) < 1)
        {
            return toTemp;
        }

        return fromTemp + dt;
    }







    private bool canSmelt()
    {
        CombustibleProperties fuelCopts = fuelCombustibleOpts;
        if (fuelCopts == null) return false;

        return BurnsAllFuell
                // Require fuel
                && fuelCopts.BurnTemperature * HeatModifier > 0
        ;
    }

    public void igniteFuel()
    {
        igniteWithFuel(fuelStack);

        fuelStack.StackSize -= 1;

        if (fuelStack.StackSize <= 0)
        {
            fuelStack = null;
        }
    }



    public void igniteWithFuel(IItemStack stack)
    {
        CombustibleProperties fuelCopts = stack.Collectible.CombustibleProps;

        maxFuelBurnTime = fuelBurnTime = fuelCopts.BurnDuration * BurnDurationModifier;
        maxTemperature = (int)(fuelCopts.BurnTemperature * HeatModifier);
        smokeLevel = fuelCopts.SmokeLevel;
        setBlockState("lit");
        MarkDirty(true);
    }




    public void setBlockState(string state)
    {
        AssetLocation loc = Block.CodeWithVariants(new string[]{"burnstate", "side"}, new string[]{state, Block.Variant["side"]});
        Block block = Api.World.GetBlock(loc);
        if (block == null) {
            return;
        }

        Api.World.BlockAccessor.ExchangeBlock(block.Id, Pos);
        this.Block = block;
    }

    #region Events

    public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
    {
        if (Api.Side == EnumAppSide.Client)
        {
            toggleInventoryDialogClient(byPlayer, () => {
                SyncedTreeAttribute dtree = new SyncedTreeAttribute();
                SetDialogValues(dtree);
                clientDialog = new GuiDialogBlockEntityStirlingEngineBurner(DialogTitle, Inventory, Pos, dtree, Api as ICoreClientAPI);
                return clientDialog;
                });
        }

        return true;
    }




    public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
    {
        base.OnReceivedClientPacket(player, packetid, data);
    }

    public override void OnReceivedServerPacket(int packetid, byte[] data)
    {
        if (packetid == (int)EnumBlockEntityPacketId.Close)
        {
            (Api.World as IClientWorldAccessor).Player.InventoryManager.CloseInventory(Inventory);
            invDialog?.TryClose();
            invDialog?.Dispose();
            invDialog = null;
        }
    }




    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        Inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));

        if (Api != null)
        {
            Inventory.AfterBlocksLoaded(Api.World);
        }


        furnaceTemperature = tree.GetFloat("furnaceTemperature");
        maxTemperature = tree.GetInt("maxTemperature");
        fuelBurnTime = tree.GetFloat("fuelBurnTime");
        maxFuelBurnTime = tree.GetFloat("maxFuelBurnTime");
        extinguishedTotalHours = tree.GetDouble("extinguishedTotalHours");
        canIgniteFuel = tree.GetBool("canIgniteFuel", true);
        cachedFuel = tree.GetFloat("cachedFuel", 0);

        if (Api?.Side == EnumAppSide.Client) {
            if (clientDialog != null) SetDialogValues(clientDialog.Attributes);
        }


        if (Api?.Side == EnumAppSide.Client && (clientSidePrevBurning != IsBurning))
        {
            GetBehavior<BEBehaviorFirepitAmbient>()?.ToggleAmbientSounds(IsBurning);
            clientSidePrevBurning = IsBurning;
            MarkDirty(true);
        }
    }

    void SetDialogValues(ITreeAttribute dialogTree)
    {
        dialogTree.SetFloat("furnaceTemperature", furnaceTemperature);

        dialogTree.SetInt("maxTemperature", maxTemperature);
        dialogTree.SetFloat("maxFuelBurnTime", maxFuelBurnTime);
        dialogTree.SetFloat("fuelBurnTime", fuelBurnTime);
    }




    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        ITreeAttribute invtree = new TreeAttribute();
        Inventory.ToTreeAttributes(invtree);
        tree["inventory"] = invtree;

        tree.SetFloat("furnaceTemperature", furnaceTemperature);
        tree.SetInt("maxTemperature", maxTemperature);
        tree.SetFloat("fuelBurnTime", fuelBurnTime);
        tree.SetFloat("maxFuelBurnTime", maxFuelBurnTime);
        tree.SetDouble("extinguishedTotalHours", extinguishedTotalHours);
        tree.SetBool("canIgniteFuel", canIgniteFuel);
        tree.SetFloat("cachedFuel", cachedFuel);
    }




    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();

        if (clientDialog != null)
        {
            clientDialog.TryClose();
            clientDialog?.Dispose();
            clientDialog = null;
        }
    }

    public override void OnBlockBroken(IPlayer byPlayer = null)
    {
        base.OnBlockBroken();
    }



    #endregion

    #region Helper getters


    public ItemSlot fuelSlot
    {
        get { return inventory[0]; }
    }

    public ItemStack fuelStack
    {
        get { return inventory[0].Itemstack; }
        set { inventory[0].Itemstack = value; inventory[0].MarkDirty(); }
    }

    public CombustibleProperties fuelCombustibleOpts
    {
        get { return getCombustibleOpts(0); }
    }

    public CombustibleProperties getCombustibleOpts(int slotid)
    {
        ItemSlot slot = inventory[slotid];
        if (slot.Itemstack == null) return null;
        return slot.Itemstack.Collectible.CombustibleProps;
    }

    #endregion


    public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
    {
        foreach (var slot in Inventory)
        {
            if (slot.Itemstack == null) continue;

            if (slot.Itemstack.Class == EnumItemClass.Item)
            {
                itemIdMapping[slot.Itemstack.Item.Id] = slot.Itemstack.Item.Code;
            }
            else
            {
                blockIdMapping[slot.Itemstack.Block.BlockId] = slot.Itemstack.Block.Code;
            }

            slot.Itemstack.Collectible.OnStoreCollectibleMappings(Api.World, slot, blockIdMapping, itemIdMapping);
        }
    }

    public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
    {
        foreach (var slot in Inventory)
        {
            if (slot.Itemstack == null) continue;
            if (!slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve))
            {
                slot.Itemstack = null;
            } else
            {
                slot.Itemstack.Collectible.OnLoadCollectibleMappings(worldForResolve, slot, oldBlockIdMapping, oldItemIdMapping);
            }
        }
    }

    public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
    {
        return IsBurning ? 10 : (IsSmoldering ? 0.25f : 0);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb) {
        BlockPos upPos = Pos.UpCopy();
        BlockEntity be = this.Api.World?.BlockAccessor.GetBlockEntity(upPos);
        if (be == null) sb.AppendLine("null be");
        be?.GetBlockInfo(forPlayer, sb);
    }

}