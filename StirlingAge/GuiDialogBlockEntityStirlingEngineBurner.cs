/* Based on GuiDialogBlockEntityFirepit from vssurvivalmod */

using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

public class GuiDialogBlockEntityStirlingEngineBurner : GuiDialogBlockEntity {

    long lastRedrawMs;
    EnumPosFlag screenPos;

    protected override double FloatyDialogPosition => 0.6;
    protected override double FloatyDialogAlign => 0.8;

    public override double DrawOrder => 0.2;

    public GuiDialogBlockEntityStirlingEngineBurner(string dialogTitle, InventoryBase Inventory, BlockPos BlockEntityPosition,
                                        SyncedTreeAttribute tree, ICoreClientAPI capi)
        : base(dialogTitle, Inventory, BlockEntityPosition, capi)
    {
        if (IsDuplicate) return;
        tree.OnModified.Add(new TreeModifiedListener() { listener = OnAttributesModified } );
        Attributes = tree;
    }

    private void OnInventorySlotModified(int slotid)
    {
        // Direct call can cause InvalidOperationException
        capi.Event.EnqueueMainThreadTask(SetupDialog, "setupstirlingdlg");
    }

    void SetupDialog()
    {
        ElementBounds stoveBounds = ElementBounds.Fixed(0, 0, 210, 150);

        ItemSlot hoveredSlot = capi.World.Player.InventoryManager.CurrentHoveredSlot;
        if (hoveredSlot != null && hoveredSlot.Inventory?.InventoryID != Inventory?.InventoryID) {
            hoveredSlot = null;
        }

        ElementBounds fuelSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 40, 1, 1);

        // 2. Around all that is 10 pixel padding
        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;
        bgBounds.WithChildren(stoveBounds);

        // 3. Finally Dialog
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithFixedAlignmentOffset(IsRight(screenPos) ? -GuiStyle.DialogToScreenPadding : GuiStyle.DialogToScreenPadding, 0)
            .WithAlignment(IsRight(screenPos) ? EnumDialogArea.RightMiddle : EnumDialogArea.LeftMiddle)
        ;


        if (!capi.Settings.Bool["immersiveMouseMode"])
        {
            dialogBounds.fixedOffsetY += (stoveBounds.fixedHeight + 65) * YOffsetMul(screenPos);
        }
        SingleComposer = capi.Gui
            .CreateCompo("blockentitystirlingburner"+BlockEntityPosition, dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
            .BeginChildElements(bgBounds)
                .AddDynamicCustomDraw(stoveBounds, OnBgDraw, "symbolDrawer")
                .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 0 }, fuelSlotBounds, "fuelslot")
                .AddDynamicText("", CairoFont.WhiteDetailText(), fuelSlotBounds.RightCopy(17, 16).WithFixedSize(60, 30), "fueltemp")
            .EndChildElements()
            .Compose();

        lastRedrawMs = capi.ElapsedMilliseconds;

        if (hoveredSlot != null)
        {
            SingleComposer.OnMouseMove(new MouseEvent(capi.Input.MouseX, capi.Input.MouseY));
        }
    }


    private void OnAttributesModified()
    {
        if (!IsOpened()) return;

        float ftemp = Attributes.GetFloat("furnaceTemperature");

        string fuelTemp = ftemp.ToString("#");

        fuelTemp += fuelTemp.Length > 0 ? "°C" : "";

        if (ftemp <= 20) fuelTemp = Lang.Get("Cold");

        SingleComposer.GetDynamicText("fueltemp").SetNewText(fuelTemp);

        if (capi.ElapsedMilliseconds - lastRedrawMs > 500)
        {
            if (SingleComposer != null) SingleComposer.GetCustomDraw("symbolDrawer").Redraw();
            lastRedrawMs = capi.ElapsedMilliseconds;
        }
    }



    private void OnBgDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
    {
        // 1. Fire
        ctx.Save();
        Matrix m = ctx.Matrix;
        m.Translate(GuiElement.scaled(5), GuiElement.scaled(53 + 40));
        m.Scale(GuiElement.scaled(0.25), GuiElement.scaled(0.25));
        ctx.Matrix = m;
        capi.Gui.Icons.DrawFlame(ctx);

        double dy = 210 - 210 * (Attributes.GetFloat("fuelBurnTime", 0) / Attributes.GetFloat("maxFuelBurnTime", 1));
        ctx.Rectangle(0, dy, 200, 210 - dy);
        ctx.Clip();
        LinearGradient gradient = new LinearGradient(0, GuiElement.scaled(250), 0, 0);
        gradient.AddColorStop(0, new Color(1, 1, 0, 1));
        gradient.AddColorStop(1, new Color(1, 0, 0, 1));
        ctx.SetSource(gradient);
        capi.Gui.Icons.DrawFlame(ctx, 0, false, false);
        gradient.Dispose();
        ctx.Restore();
    }



    private void SendInvPacket(object packet)
    {
        capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, packet);
    }


    private void OnTitleBarClose()
    {
        TryClose();
    }


    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        Inventory.SlotModified += OnInventorySlotModified;

        screenPos = GetFreePos("smallblockgui");
        OccupyPos("smallblockgui", screenPos);
        SetupDialog();
    }

    public override void OnGuiClosed()
    {
        Inventory.SlotModified -= OnInventorySlotModified;

        SingleComposer.GetSlotGrid("fuelslot").OnGuiClosed(capi);

        base.OnGuiClosed();

        FreePos("smallblockgui", screenPos);
    }
}
