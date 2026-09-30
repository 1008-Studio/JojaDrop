using JojaDrop.Models;
using JojaDrop.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
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
    private readonly UpgradeRoller upgradeRoller;
    private readonly UpgradeTransactionService transactionService;
    private readonly TargetItemProvider targetItemProvider;
    private readonly IMonitor monitor;
    private ClickableComponent sourceSlot = null!;
    private ClickableComponent targetSlot = null!;
    private ClickableComponent upgradeButton = null!;
    private Item? sourceItem;
    private TargetItemOption? targetOption;
    private string hoverText = "";
    private string statusMessage = "";
    private Point viewportSize;
    private float layoutScale;
    private bool isProcessingUpgrade;
    private bool hasRolledCurrentSelection;

    private bool isAnimatingRoulette;
    private float rouletteAnimationTimer;
    private double currentChance;
    private bool rouletteResult;
    private UpgradeTransactionResult? pendingTransaction;

    public UpgradeMenu(ItemValueService itemValues, UpgradeCalculator upgradeCalculator, UpgradeRoller upgradeRoller,
        UpgradeTransactionService transactionService, TargetItemProvider targetItemProvider, IMonitor monitor)
    {
        this.itemValues = itemValues;
        this.upgradeCalculator = upgradeCalculator;
        this.upgradeRoller = upgradeRoller;
        this.transactionService = transactionService;
        this.targetItemProvider = targetItemProvider;
        this.monitor = monitor;
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
        if (isAnimatingRoulette)
        {
            // The transaction already ran at click time and may have (in)validly removed our
            // source item — that is expected. Never cancel a spin mid-flight; only advance it.
            rouletteAnimationTimer += (float)time.ElapsedGameTime.TotalSeconds;
            if (rouletteAnimationTimer >= RouletteWheel.SpinDuration)
            {
                isAnimatingRoulette = false;
                CompleteUpgrade();
            }
            return;
        }

        if (sourceItem is not null && !IsSourceValid())
        {
            sourceItem = null;
            targetOption = null;
            hasRolledCurrentSelection = false;
            rouletteAnimationTimer = 0f;
            pendingTransaction = null;
            rouletteResult = false;
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
        // The spin is one-shot: do not let slot re-selection wipe the pending result.
        if (isAnimatingRoulette)
            return;
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
                    hasRolledCurrentSelection = false;
                    isAnimatingRoulette = false;
                    rouletteAnimationTimer = 0f;
                    pendingTransaction = null;
                    rouletteResult = false;
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
                    hasRolledCurrentSelection = false;
                    isAnimatingRoulette = false;
                    rouletteAnimationTimer = 0f;
                    pendingTransaction = null;
                    rouletteResult = false;
                    statusMessage = "";
                }
                Game1.activeClickableMenu = this;
                UpdateLayout();
            });
            return;
        }
        if (upgradeButton.containsPoint(x, y) && CanUpgrade)
            ProcessUpgrade();
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
            : upgradeButton.containsPoint(x, y) && isAnimatingRoulette ? "Upgrade in progress..."
            : upgradeButton.containsPoint(x, y) && hasRolledCurrentSelection ? "Select an item or target to make another attempt."
            : upgradeButton.containsPoint(x, y) && HasUpgradeSelection ? "Attempt an upgrade using the shown chance."
            : "";
    }

    public override void draw(SpriteBatch b)
    {
        MenuDrawing.Panel(b, this);
        MenuDrawing.CenteredText(b, "JojaDrop", xPositionOnScreen + width / 2, yPositionOnScreen + Scale(TitleOffset), scale: 1.5f * layoutScale);
        DrawItemSlot(b, sourceSlot, "Your Item", sourceItem, sourceItem is null ? null : itemValues.GetValue(sourceItem));
        DrawItemSlot(b, targetSlot, "Target Item", targetOption?.PreviewItem, targetOption?.Value, sourceItem is not null);

        // Chance and multiplier above UPGRADE button
        int buttonTop = upgradeButton.bounds.Y;
        MenuDrawing.CenteredText(b, GetChanceText(), xPositionOnScreen + width / 2, buttonTop - Scale(56), Color.SteelBlue, 1.5f * layoutScale);
        MenuDrawing.CenteredText(b, GetMultiplierText(), xPositionOnScreen + width / 2, buttonTop - Scale(24), Color.SteelBlue, layoutScale);

        MenuDrawing.TextButton(b, upgradeButton, "UPGRADE", upgradeButton.containsPoint(Game1.getMouseX(true), Game1.getMouseY(true)),
            enabled: CanUpgrade && !isAnimatingRoulette, textScale: layoutScale);
        string status = statusMessage.Length > 0 ? statusMessage
            : sourceItem is null ? "Select an item to begin."
            : targetOption is null ? "Select a target item."
            : "Target selected. Upgrade preview is ready.";
        MenuDrawing.CenteredText(b, MenuDrawing.FitText(status, width - MenuDrawing.ScreenMargin * 2),
            xPositionOnScreen + width / 2, yPositionOnScreen + height - Scale(StatusBottomOffset), scale: layoutScale);

        // Wheel must be drawn before hover text and the cursor so it stays inside the menu.
        if (HasUpgradeSelection && TryGetUpgradePreview(out double wheelChance, out _))
        {
            Vector2 wheelCenter = new(xPositionOnScreen + width / 2f, yPositionOnScreen + height / 2f - Scale(40));
            RouletteWheel.Draw(b, wheelCenter, wheelChance, rouletteAnimationTimer, isAnimatingRoulette, rouletteResult, layoutScale);
        }

        base.draw(b);
        if (hoverText.Length > 0)
            drawHoverText(b, hoverText, Game1.smallFont);
        drawMouse(b);
    }

    private bool HasUpgradeSelection => sourceItem is not null && targetOption is not null;
    private bool CanUpgrade => HasUpgradeSelection && !hasRolledCurrentSelection;

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
        int? targetValue = itemValues.GetValue(targetOption.PreviewItem);
        if (!sourceValue.HasValue || !targetValue.HasValue || sourceValue.Value <= 0 || targetValue.Value <= sourceValue.Value)
            return false;

        chance = upgradeCalculator.CalculateChance(sourceValue.Value, targetValue.Value);
        multiplier = (double)targetValue.Value / sourceValue.Value;
        return true;
    }

    private void ProcessUpgrade()
    {
        if (isProcessingUpgrade || hasRolledCurrentSelection || sourceItem is null || targetOption is null || isAnimatingRoulette)
            return;

        isProcessingUpgrade = true;
        try
        {
            Item source = sourceItem;
            Item targetPreview = targetOption.PreviewItem;
            if (!IsSourceValid())
            {
                statusMessage = "Upgrade unavailable: source item is no longer in your inventory.";
                sourceItem = null;
                targetOption = null;
                hasRolledCurrentSelection = false;
                return;
            }

            if (!IsTargetValid(targetPreview))
            {
                statusMessage = "Upgrade unavailable: target item is invalid.";
                return;
            }

            int? sourceValue = itemValues.GetValue(source);
            int? targetValue = itemValues.GetValue(targetPreview);
            if (!sourceValue.HasValue || sourceValue.Value <= 0)
            {
                statusMessage = "Upgrade unavailable: source item has no valid value.";
                return;
            }
            if (!targetValue.HasValue || targetValue.Value <= sourceValue.Value)
            {
                statusMessage = "Upgrade unavailable: target must be more valuable.";
                return;
            }

            double chance = upgradeCalculator.CalculateChance(sourceValue.Value, targetValue.Value);
            bool success = upgradeRoller.Roll(chance);
            UpgradeTransactionResult transaction = transactionService.Apply(Game1.player, source, targetPreview, success);
            monitor.Log($"Upgrade attempt: source={source.QualifiedItemId}; sourceValue={sourceValue.Value}; "
                + $"target={targetPreview.QualifiedItemId}; targetValue={targetValue.Value}; chance={chance:0.####}; "
                + $"result={(success ? "success" : "fail")}; transaction={transaction.Status}.", LogLevel.Trace);

            currentChance = chance;
            rouletteResult = success;
            pendingTransaction = transaction;
            hasRolledCurrentSelection = true;
            isAnimatingRoulette = true;
            rouletteAnimationTimer = 0f;
            Game1.playSound("cowboy_monsterhit");
        }
        finally
        {
            isProcessingUpgrade = false;
        }
    }

    private void CompleteUpgrade()
    {
        if (pendingTransaction is null)
            return;

        if (!pendingTransaction.Value.IsSuccess)
        {
            statusMessage = GetTransactionFailureMessage(pendingTransaction.Value.Status);
            hasRolledCurrentSelection = false;
            pendingTransaction = null;
            return;
        }

        sourceItem = null;
        targetOption = null;
        hasRolledCurrentSelection = false;
        statusMessage = rouletteResult ? "Upgrade successful!" : "Upgrade failed.";
        Game1.playSound(rouletteResult ? "discoverMineral" : "cancel");
        pendingTransaction = null;
    }

    private bool IsSourceValid()
    {
        return sourceItem is { Stack: > 0 } source
            && Game1.player.Items.Any(item => ReferenceEquals(item, source));
    }

    private static bool IsTargetValid(Item? targetPreview)
    {
        if (targetPreview is null || string.IsNullOrWhiteSpace(targetPreview.QualifiedItemId))
            return false;

        try
        {
            return ItemRegistry.GetData(targetPreview.QualifiedItemId) is { IsErrorItem: false };
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string GetTransactionFailureMessage(UpgradeTransactionStatus status)
    {
        return status switch
        {
            UpgradeTransactionStatus.SourceMissing => "Upgrade unavailable: source item is no longer in your inventory.",
            UpgradeTransactionStatus.InventoryFull => "Upgrade could not be completed: inventory is full.",
            UpgradeTransactionStatus.InvalidTarget => "Upgrade unavailable: target item is invalid.",
            UpgradeTransactionStatus.TransactionFailed => "Upgrade could not be completed safely.",
            _ => "Upgrade could not be completed."
        };
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
