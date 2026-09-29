using JojaDrop.Models;
using JojaDrop.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace JojaDrop.UI;

internal sealed class UpgradeMenu : IClickableMenu
{
    private const int PanelWidth = 720;
    private const int PanelHeight = 440;
    private const int TitleOffset = 36;
    private const int SlotTopOffset = 132;
    private const int LabelGap = 40;
    private const int ButtonWidth = 208;
    private const int ButtonHeight = 64;
    private const int ButtonFooterGap = 64;
    private const int StatusBottomOffset = 40;
    private const int ItemNameGap = 12;
    private const int ItemValueGap = 40;
    private const int ChanceTopOffset = 16;
    private const int MultiplierTopOffset = 56;
    private const int SourceId = 100;
    private const int TargetId = 101;
    private const int UpgradeId = 102;
    private const int CloseId = 103;
    private readonly ItemValueService itemValues;
    private readonly UpgradeCalculator upgradeCalculator;
    private readonly TargetItemProvider targetItemProvider;
    private ClickableComponent sourceSlot = null!;
    private ClickableComponent targetSlot = null!;
    private ClickableComponent upgradeButton = null!;
    private Item? sourceItem;
    private TargetItemOption? targetOption;
    private string hoverText = "";
    private string statusMessage = "";
    private Point viewportSize;
    private float layoutScale;

    public UpgradeMenu(ItemValueService itemValues, UpgradeCalculator upgradeCalculator, TargetItemProvider targetItemProvider)
    {
        this.itemValues = itemValues;
        this.upgradeCalculator = upgradeCalculator;
        this.targetItemProvider = targetItemProvider;
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        int? snappedId = currentlySnappedComponent?.myID;
        viewportSize = new Point(Game1.uiViewport.Width, Game1.uiViewport.Height);
        width = Math.Min(PanelWidth, Math.Max(1, viewportSize.X - MenuDrawing.ScreenMargin * 2));
        height = Math.Min(PanelHeight, Math.Max(1, viewportSize.Y - MenuDrawing.ScreenMargin * 2));
        layoutScale = Math.Min(width / (float)PanelWidth, height / (float)PanelHeight);
        xPositionOnScreen = (viewportSize.X - width) / 2;
        yPositionOnScreen = (viewportSize.Y - height) / 2;
        int slotTop = yPositionOnScreen + Scale(SlotTopOffset);
        int slotSize = Scale(MenuDrawing.SlotSize);
        int buttonWidth = Scale(ButtonWidth);
        int buttonHeight = Scale(ButtonHeight);

        sourceSlot = new ClickableComponent(new Rectangle(xPositionOnScreen + width / 4 - slotSize / 2,
            slotTop, slotSize, slotSize), "Your Item")
        {
            myID = SourceId, rightNeighborID = TargetId, downNeighborID = UpgradeId, upNeighborID = CloseId
        };
        targetSlot = new ClickableComponent(new Rectangle(xPositionOnScreen + width * 3 / 4 - slotSize / 2,
            slotTop, slotSize, slotSize), "Target Item")
        {
            myID = TargetId, leftNeighborID = SourceId, downNeighborID = UpgradeId, upNeighborID = CloseId
        };
        upgradeButton = new ClickableComponent(new Rectangle(xPositionOnScreen + (width - buttonWidth) / 2,
            yPositionOnScreen + height - buttonHeight - Scale(ButtonFooterGap), buttonWidth, buttonHeight), "Upgrade")
        {
            myID = UpgradeId, upNeighborID = SourceId, rightNeighborID = CloseId
        };
        initializeUpperRightCloseButton();
        upperRightCloseButton.myID = CloseId;
        upperRightCloseButton.leftNeighborID = TargetId;
        upperRightCloseButton.downNeighborID = TargetId;
        allClickableComponents = new List<ClickableComponent> { sourceSlot, targetSlot, upgradeButton, upperRightCloseButton };

        if (Game1.options.SnappyMenus)
        {
            setCurrentlySnappedComponentTo(snappedId ?? SourceId);
            snapCursorToCurrentSnappedComponent();
        }
    }

    private int Scale(int value) => Math.Max(1, (int)(value * layoutScale));

    public override void snapToDefaultClickableComponent()
    {
        setCurrentlySnappedComponentTo(SourceId);
        snapCursorToCurrentSnappedComponent();
    }

    public override void update(GameTime time)
    {
        base.update(time);
        if (viewportSize.X != Game1.uiViewport.Width || viewportSize.Y != Game1.uiViewport.Height)
            UpdateLayout();
        if (sourceItem is not null && !Game1.player.Items.Any(item => ReferenceEquals(item, sourceItem)))
        {
            sourceItem = null;
            targetOption = null;
        }
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds) => UpdateLayout();

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (upperRightCloseButton.containsPoint(x, y))
        {
            exitThisMenu();
            return;
        }
        if (sourceSlot.containsPoint(x, y))
        {
            if (playSound)
                Game1.playSound("smallSelect");
            Game1.activeClickableMenu = new SourceItemMenu(itemValues, item =>
            {
                if (item is not null)
                {
                    sourceItem = item;
                    targetOption = null;
                    statusMessage = "";
                }
                Game1.activeClickableMenu = this;
                UpdateLayout();
            });
            return;
        }
        if (targetSlot.containsPoint(x, y) && sourceItem is not null)
        {
            if (playSound)
                Game1.playSound("smallSelect");
            Game1.activeClickableMenu = new TargetItemMenu(targetItemProvider.GetTargets(sourceItem), option =>
            {
                if (option is not null)
                {
                    targetOption = option;
                    statusMessage = "";
                }
                Game1.activeClickableMenu = this;
                UpdateLayout();
            });
            return;
        }
        if (upgradeButton.containsPoint(x, y) && HasUpgradeSelection)
        {
            Game1.playSound("cancel");
            statusMessage = "Upgrade execution is coming in the next milestone.";
        }
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape)
            exitThisMenu();
        else
            base.receiveKeyPress(key);
    }

    public override void receiveGamePadButton(Buttons button)
    {
        if (button == Buttons.B)
            exitThisMenu();
        else
            base.receiveGamePadButton(button);
    }

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        hoverText = sourceSlot.containsPoint(x, y) ? "Choose an item from your inventory.\nSelection leaves it in your backpack."
            : targetSlot.containsPoint(x, y) && sourceItem is null ? "Select Your Item first."
            : targetSlot.containsPoint(x, y) ? "Choose a target item."
            : upgradeButton.containsPoint(x, y) && HasUpgradeSelection ? "Upgrade execution is coming in the next milestone.\nNo items will be consumed."
            : "";
    }

    public override void draw(SpriteBatch b)
    {
        MenuDrawing.Panel(b, this);
        MenuDrawing.CenteredText(b, "JojaDrop", xPositionOnScreen + width / 2, yPositionOnScreen + Scale(TitleOffset), scale: 1.5f * layoutScale);
        DrawItemSlot(b, sourceSlot, "Your Item", sourceItem, sourceItem is null ? null : itemValues.GetValue(sourceItem));
        DrawItemSlot(b, targetSlot, "Target Item", targetOption?.PreviewItem, targetOption?.Value, sourceItem is not null);
        MenuDrawing.CenteredText(b, GetChanceText(), xPositionOnScreen + width / 2, sourceSlot.bounds.Y + Scale(ChanceTopOffset), Color.SteelBlue, 1.5f * layoutScale);
        MenuDrawing.CenteredText(b, GetMultiplierText(), xPositionOnScreen + width / 2, sourceSlot.bounds.Y + Scale(MultiplierTopOffset), Color.SteelBlue, layoutScale);
        MenuDrawing.TextButton(b, upgradeButton, "UPGRADE", upgradeButton.containsPoint(Game1.getMouseX(true), Game1.getMouseY(true)),
            enabled: HasUpgradeSelection, textScale: layoutScale);
        string status = statusMessage.Length > 0 ? statusMessage
            : sourceItem is null ? "Select an item to begin."
            : targetOption is null ? "Select a target item."
            : "Target selected. Upgrade preview is ready.";
        MenuDrawing.CenteredText(b, MenuDrawing.FitText(status, width - MenuDrawing.ScreenMargin * 2),
            xPositionOnScreen + width / 2, yPositionOnScreen + height - Scale(StatusBottomOffset), scale: layoutScale);
        base.draw(b);
        if (hoverText.Length > 0)
            drawHoverText(b, hoverText, Game1.smallFont);
        drawMouse(b);
    }

    private bool HasUpgradeSelection => sourceItem is not null && targetOption is not null;

    private string GetChanceText()
    {
        return TryGetUpgradePreview(out double chance, out _) ? $"{chance * 100:0.00}%" : "--";
    }

    private string GetMultiplierText()
    {
        return TryGetUpgradePreview(out _, out double multiplier) ? $"x{multiplier:0.00}" : "--";
    }

    private bool TryGetUpgradePreview(out double chance, out double multiplier)
    {
        chance = 0;
        multiplier = 0;
        if (sourceItem is null || targetOption is null)
            return false;

        int? sourceValue = itemValues.GetValue(sourceItem);
        if (!sourceValue.HasValue || sourceValue.Value <= 0 || targetOption.Value <= sourceValue.Value)
            return false;

        chance = upgradeCalculator.CalculateChance(sourceValue.Value, targetOption.Value);
        multiplier = (double)targetOption.Value / sourceValue.Value;
        return true;
    }

    private void DrawItemSlot(SpriteBatch b, ClickableComponent slot, string label, Item? item, int? value, bool enabled = true)
    {
        MenuDrawing.CenteredText(b, label, slot.bounds.Center.X, slot.bounds.Y - Scale(LabelGap), scale: layoutScale);
        MenuDrawing.Slot(b, slot.bounds, item, slot.containsPoint(Game1.getMouseX(true), Game1.getMouseY(true)), enabled);
        string name = item?.DisplayName ?? "Empty";
        MenuDrawing.CenteredText(b, MenuDrawing.FitText(name, width / 3), slot.bounds.Center.X,
            slot.bounds.Bottom + Scale(ItemNameGap), scale: layoutScale);
        if (item is not null)
        {
            MenuDrawing.CenteredText(b, value.HasValue ? $"{value.Value:N0}g / item" : "Value unavailable",
                slot.bounds.Center.X, slot.bounds.Bottom + Scale(ItemValueGap), scale: layoutScale);
        }
    }
}
