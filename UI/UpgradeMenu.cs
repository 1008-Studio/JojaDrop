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
    private const int QuantityButtonSize = 36;
    private const int StatusBottomOffset = 40;
    private const int ItemNameGap = 12;
    private const int ItemValueGap = 40;
    private const int ChanceTopOffset = 16;
    private const int MultiplierTopOffset = 56;
    private const int SourceId = 100;
    private const int TargetId = 101;
    private const int UpgradeId = 102;
    private const int CloseId = 103;
    private const int SourceDecreaseId = 104;
    private const int SourceIncreaseId = 105;
    private const int OutputDecreaseId = 106;
    private const int OutputIncreaseId = 107;
    private readonly ItemValueService itemValues;
    private readonly UpgradeCalculator upgradeCalculator;
    private readonly UpgradeRoller upgradeRoller;
    private readonly UpgradeTransactionService transactionService;
    private readonly InventoryBatchService inventory = new();
    private readonly TargetItemProvider targetItemProvider;
    private readonly IMonitor monitor;
    private readonly Texture2D wheelArrow;
    private readonly Texture2D wheelCenter;
    private readonly Texture2D wheelFrame;
    private ClickableComponent sourceSlot = null!;
    private ClickableComponent targetSlot = null!;
    private ClickableComponent upgradeButton = null!;
    private ClickableComponent sourceDecreaseButton = null!;
    private ClickableComponent sourceIncreaseButton = null!;
    private ClickableComponent outputDecreaseButton = null!;
    private ClickableComponent outputIncreaseButton = null!;
    private Item? sourceItem;
    private TargetItemOption? targetOption;
    private int sourceQuantity = 1;
    private int outputQuantity = 1;
    private int availableQuantity;
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
    private float rouletteTargetAngle;
    private Item? pendingSource;
    private TargetItemOption? pendingTarget;
    private int pendingSourceQuantity;
    private int pendingOutputQuantity;

    public UpgradeMenu(ItemValueService itemValues, UpgradeCalculator upgradeCalculator, UpgradeRoller upgradeRoller,
        UpgradeTransactionService transactionService, TargetItemProvider targetItemProvider, IMonitor monitor,
        Texture2D wheelArrow, Texture2D wheelCenter, Texture2D wheelFrame)
    {
        this.itemValues = itemValues;
        this.upgradeCalculator = upgradeCalculator;
        this.upgradeRoller = upgradeRoller;
        this.transactionService = transactionService;
        this.targetItemProvider = targetItemProvider;
        this.monitor = monitor;
        this.wheelArrow = wheelArrow;
        this.wheelCenter = wheelCenter;
        this.wheelFrame = wheelFrame;
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
        int quantitySize = Scale(QuantityButtonSize);
        int quantityTop = slotTop + slotSize + Scale(72);
        sourceDecreaseButton = CreateQuantityButton(sourceSlot.bounds.Center.X - Scale(52), quantityTop, quantitySize, SourceDecreaseId);
        sourceIncreaseButton = CreateQuantityButton(sourceSlot.bounds.Center.X + Scale(16), quantityTop, quantitySize, SourceIncreaseId);
        outputDecreaseButton = CreateQuantityButton(targetSlot.bounds.Center.X - Scale(52), quantityTop, quantitySize, OutputDecreaseId);
        outputIncreaseButton = CreateQuantityButton(targetSlot.bounds.Center.X + Scale(16), quantityTop, quantitySize, OutputIncreaseId);
        sourceDecreaseButton.rightNeighborID = SourceIncreaseId;
        sourceIncreaseButton.leftNeighborID = SourceDecreaseId;
        sourceIncreaseButton.rightNeighborID = OutputDecreaseId;
        outputDecreaseButton.leftNeighborID = SourceIncreaseId;
        outputDecreaseButton.rightNeighborID = OutputIncreaseId;
        outputIncreaseButton.leftNeighborID = OutputDecreaseId;
        sourceDecreaseButton.upNeighborID = sourceIncreaseButton.upNeighborID = SourceId;
        outputDecreaseButton.upNeighborID = outputIncreaseButton.upNeighborID = TargetId;
        sourceDecreaseButton.downNeighborID = sourceIncreaseButton.downNeighborID = UpgradeId;
        outputDecreaseButton.downNeighborID = outputIncreaseButton.downNeighborID = UpgradeId;
        initializeUpperRightCloseButton();
        upperRightCloseButton.myID = CloseId;
        upperRightCloseButton.leftNeighborID = TargetId;
        upperRightCloseButton.downNeighborID = TargetId;
        allClickableComponents = new List<ClickableComponent> { sourceSlot, targetSlot, sourceDecreaseButton, sourceIncreaseButton,
            outputDecreaseButton, outputIncreaseButton, upgradeButton, upperRightCloseButton };

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
            // The transaction is applied after the spin finishes. Never cancel a spin mid-flight.
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
            ResetQuantityState();
            hasRolledCurrentSelection = false;
            rouletteAnimationTimer = 0f;
            pendingSource = null;
            pendingTarget = null;
            pendingSourceQuantity = 0;
            pendingOutputQuantity = 0;
            rouletteResult = false;
        }
        else if (sourceItem is not null)
        {
            RefreshQuantityState();
        }
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds) => UpdateLayout();

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (isAnimatingRoulette)
            return;
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
                    ResetQuantityState();
                    hasRolledCurrentSelection = false;
                    isAnimatingRoulette = false;
                    rouletteAnimationTimer = 0f;
                    pendingSource = null;
                    pendingTarget = null;
                    pendingSourceQuantity = 0;
                    pendingOutputQuantity = 0;
                    rouletteResult = false;
                    statusMessage = "";
                }
                Game1.activeClickableMenu = this;
                UpdateLayout();
            });
            return;
        }
        if (sourceDecreaseButton.containsPoint(x, y))
        {
            ChangeSourceQuantity(-1);
            return;
        }
        if (sourceIncreaseButton.containsPoint(x, y))
        {
            ChangeSourceQuantity(1);
            return;
        }
        if (outputDecreaseButton.containsPoint(x, y))
        {
            ChangeOutputQuantity(-1);
            return;
        }
        if (outputIncreaseButton.containsPoint(x, y))
        {
            ChangeOutputQuantity(1);
            return;
        }
        if (targetSlot.containsPoint(x, y) && sourceItem is not null)
        {
            OpenTargetPicker(TargetFilterMode.All, playSound);
            return;
        }
        if (upgradeButton.containsPoint(x, y) && CanUpgrade)
            ProcessUpgrade();
    }

    /// <summary>Open the target picker, optionally narrowed to a target probability filter.</summary>
    private void OpenTargetPicker(TargetFilterMode filter, bool playSound)
    {
        if (sourceItem is null)
            return;

        if (playSound)
            Game1.playSound("smallSelect");
        Game1.activeClickableMenu = new TargetItemMenu(targetItemProvider.GetTargets(sourceItem, filter), option =>
        {
            if (option is not null)
            {
                targetOption = option;
                if (!TryGetMinimumOutputQuantity(out int minimumOutput))
                {
                    targetOption = null;
                    statusMessage = "Upgrade unavailable: target item has no valid value.";
                    Game1.activeClickableMenu = this;
                    UpdateLayout();
                    return;
                }

                outputQuantity = minimumOutput;
                RefreshQuantityState();
                hasRolledCurrentSelection = false;
                isAnimatingRoulette = false;
                rouletteAnimationTimer = 0f;
                pendingSource = null;
                pendingTarget = null;
                pendingSourceQuantity = 0;
                pendingOutputQuantity = 0;
                rouletteResult = false;
                statusMessage = "";
            }
            Game1.activeClickableMenu = this;
            UpdateLayout();
        }, filter);
    }

    public override void receiveKeyPress(Keys key)
    {
        if (isAnimatingRoulette)
            return;
        if (key == Keys.Escape)
            exitThisMenu();
        else
            base.receiveKeyPress(key);
    }

    public override void receiveGamePadButton(Buttons button)
    {
        if (isAnimatingRoulette)
            return;
        if (button == Buttons.B)
            exitThisMenu();
        else
            base.receiveGamePadButton(button);
    }

    public override bool readyToClose() => !isAnimatingRoulette && base.readyToClose();

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        hoverText = sourceSlot.containsPoint(x, y) ? "Choose an item from your inventory.\nSelection leaves it in your backpack."
            : sourceDecreaseButton.containsPoint(x, y) ? "Decrease source quantity."
            : sourceIncreaseButton.containsPoint(x, y) ? "Increase source quantity."
            : outputDecreaseButton.containsPoint(x, y) ? "Decrease output quantity."
            : outputIncreaseButton.containsPoint(x, y) ? "Increase output quantity."
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
        DrawItemSlot(b, sourceSlot, $"Your Item ×{sourceQuantity}", sourceItem, sourceItem is null ? null : itemValues.GetValue(sourceItem), sourceQuantity);
        DrawItemSlot(b, targetSlot, $"Target ×{outputQuantity}", targetOption?.PreviewItem, targetOption?.Value, outputQuantity, sourceItem is not null);
        DrawQuantityControl(b, sourceDecreaseButton, sourceIncreaseButton, sourceQuantity, CanDecreaseSource, CanIncreaseSource);
        DrawQuantityControl(b, outputDecreaseButton, outputIncreaseButton, outputQuantity, CanDecreaseOutput, CanIncreaseOutput);

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
        double wheelChance = currentChance;
        if (HasUpgradeSelection && (isAnimatingRoulette || TryGetUpgradePreview(out wheelChance, out _)))
        {
            Vector2 wheelPosition = new(xPositionOnScreen + width / 2f, yPositionOnScreen + height / 2f - Scale(40));
            RouletteWheel.Draw(b, wheelFrame, wheelCenter, wheelArrow, wheelPosition, wheelChance, rouletteAnimationTimer,
                isAnimatingRoulette, rouletteTargetAngle, layoutScale);
        }

        base.draw(b);
        if (hoverText.Length > 0)
            drawHoverText(b, hoverText, Game1.smallFont);
        drawMouse(b);
    }

    private bool HasUpgradeSelection => sourceItem is not null && targetOption is not null;
    private bool CanUpgrade => HasUpgradeSelection && availableQuantity >= sourceQuantity && !hasRolledCurrentSelection
        && TryGetUpgradePreview(out _, out _);
    private bool CanDecreaseSource => sourceItem is not null && sourceQuantity > 1;
    private bool CanIncreaseSource => sourceItem is not null && sourceQuantity < availableQuantity;
    private bool CanDecreaseOutput => TryGetMinimumOutputQuantity(out int minimumOutput) && outputQuantity > minimumOutput;
    private bool CanIncreaseOutput => sourceItem is not null && outputQuantity < int.MaxValue;

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
        if (!sourceValue.HasValue || !targetValue.HasValue || sourceValue.Value <= 0 || targetValue.Value <= 0)
            return false;

        if (!upgradeCalculator.IsBatchTargetValueValid(sourceQuantity, outputQuantity, sourceValue.Value, targetValue.Value))
            return false;

        chance = upgradeCalculator.CalculateChance(sourceQuantity, outputQuantity, sourceValue.Value, targetValue.Value);
        multiplier = (double)outputQuantity * targetValue.Value / ((double)sourceQuantity * sourceValue.Value);
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
                ResetQuantityState();
                hasRolledCurrentSelection = false;
                return;
            }

            RefreshQuantityState();
            if (availableQuantity < sourceQuantity)
            {
                statusMessage = "Upgrade unavailable: not enough compatible source items remain.";
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
            if (!targetValue.HasValue || targetValue.Value <= 0)
            {
                statusMessage = "Upgrade unavailable: target item has no valid value.";
                return;
            }
            if (!upgradeCalculator.IsBatchTargetValueValid(sourceQuantity, outputQuantity, sourceValue.Value, targetValue.Value))
            {
                statusMessage = "Upgrade unavailable: target batch value is too low.";
                return;
            }

            double chance = upgradeCalculator.CalculateChance(sourceQuantity, outputQuantity, sourceValue.Value, targetValue.Value);
            bool success = upgradeRoller.Roll(chance);

            // The roll is decided now, but the transaction is applied only after the
            // spin finishes so nothing is revealed while the needle is still moving.
            currentChance = chance;
            rouletteResult = success;
            rouletteTargetAngle = RouletteWheel.CalculateTargetAngle(chance, success);
            pendingSource = source;
            pendingTarget = targetOption;
            pendingSourceQuantity = sourceQuantity;
            pendingOutputQuantity = outputQuantity;
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
        if (pendingSource is null || pendingTarget is null)
            return;

        Item source = pendingSource;
        Item targetPreview = pendingTarget.PreviewItem;
        int sourceQuantity = pendingSourceQuantity;
        int outputQuantity = pendingOutputQuantity;
        pendingSource = null;
        pendingTarget = null;
        pendingSourceQuantity = 0;
        pendingOutputQuantity = 0;

        if (sourceQuantity < 1 || outputQuantity < 1)
        {
            statusMessage = "Upgrade unavailable: invalid batch quantity.";
            hasRolledCurrentSelection = false;
            return;
        }

        int? sourceValue = itemValues.GetValue(source);
        int? targetValue = itemValues.GetValue(targetPreview);
        if (!IsTargetValid(targetPreview) || !sourceValue.HasValue || sourceValue.Value <= 0
            || !targetValue.HasValue || targetValue.Value <= 0
            || !upgradeCalculator.IsBatchTargetValueValid(sourceQuantity, outputQuantity, sourceValue.Value, targetValue.Value))
        {
            statusMessage = "Upgrade unavailable: target is no longer valid.";
            hasRolledCurrentSelection = false;
            return;
        }

        UpgradeTransactionResult transaction = transactionService.Apply(Game1.player, source, targetPreview,
            sourceQuantity, outputQuantity, rouletteResult);
        monitor.Log($"Upgrade attempt: source={source.QualifiedItemId}; q={sourceQuantity}; sourceValue={sourceValue.Value}; "
            + $"sourceTotal={(long)sourceQuantity * sourceValue.Value}; target={targetPreview.QualifiedItemId}; r={outputQuantity}; "
            + $"targetValue={targetValue.Value}; targetTotal={(long)outputQuantity * targetValue.Value}; chance={currentChance:0.####}; "
            + $"roll={(rouletteResult ? "success" : "fail")}; transaction={transaction.Status}.", LogLevel.Trace);

        if (!transaction.IsSuccess)
        {
            statusMessage = GetTransactionFailureMessage(transaction.Status);
            hasRolledCurrentSelection = false;
            return;
        }

        sourceItem = null;
        targetOption = null;
        ResetQuantityState();
        hasRolledCurrentSelection = false;
        statusMessage = rouletteResult ? "Upgrade successful!" : "Upgrade failed.";
        Game1.playSound(rouletteResult ? "discoverMineral" : "cancel");
    }

    private bool IsSourceValid()
    {
        return sourceItem is { Stack: > 0 } source
            && Game1.player.Items.Any(item => ReferenceEquals(item, source));
    }

    private void ResetQuantityState()
    {
        sourceQuantity = 1;
        outputQuantity = 1;
        availableQuantity = sourceItem is null ? 0 : inventory.GetCompatibleQuantity(Game1.player, sourceItem);
    }

    private void RefreshQuantityState()
    {
        availableQuantity = sourceItem is null ? 0 : inventory.GetCompatibleQuantity(Game1.player, sourceItem);
        if (availableQuantity > 0)
            sourceQuantity = Math.Clamp(sourceQuantity, 1, availableQuantity);
        outputQuantity = Math.Max(1, outputQuantity);
        EnsureMinimumOutputQuantity();
    }

    private static ClickableComponent CreateQuantityButton(int x, int y, int size, int id) => new(new Rectangle(x, y, size, size), "Quantity") { myID = id };

    private void ChangeSourceQuantity(int delta)
    {
        RefreshQuantityState();
        sourceQuantity = Math.Clamp(sourceQuantity + delta, 1, Math.Max(1, availableQuantity));
        EnsureMinimumOutputQuantity();
    }

    private void ChangeOutputQuantity(int delta)
    {
        if (delta < 0 && TryGetMinimumOutputQuantity(out int minimumOutput))
            outputQuantity = Math.Max(minimumOutput, outputQuantity - 1);
        else if (delta > 0 && outputQuantity < int.MaxValue)
            outputQuantity++;
    }

    private void EnsureMinimumOutputQuantity()
    {
        if (TryGetMinimumOutputQuantity(out int minimumOutput))
            outputQuantity = Math.Max(outputQuantity, minimumOutput);
    }

    private bool TryGetMinimumOutputQuantity(out int minimumOutput)
    {
        minimumOutput = 0;
        if (sourceItem is null || targetOption is null)
            return false;

        int? sourceValue = itemValues.GetValue(sourceItem);
        return sourceValue.HasValue
            && upgradeCalculator.TryGetMinimumTargetQuantity(sourceQuantity, sourceValue.Value, targetOption.Value, out minimumOutput);
    }

    private void DrawQuantityControl(SpriteBatch b, ClickableComponent decrease, ClickableComponent increase, int quantity, bool canDecrease, bool canIncrease)
    {
        int mouseX = Game1.getMouseX(true);
        int mouseY = Game1.getMouseY(true);
        MenuDrawing.TextButton(b, decrease, "-", decrease.containsPoint(mouseX, mouseY), canDecrease, layoutScale);
        MenuDrawing.CenteredText(b, quantity.ToString(), (decrease.bounds.Right + increase.bounds.Left) / 2,
            decrease.bounds.Center.Y - (int)(Game1.smallFont.MeasureString(quantity.ToString()).Y * layoutScale) / 2, scale: layoutScale);
        MenuDrawing.TextButton(b, increase, "+", increase.containsPoint(mouseX, mouseY), canIncrease, layoutScale);
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
            UpgradeTransactionStatus.InvalidQuantity => "Upgrade unavailable: invalid batch quantity.",
            UpgradeTransactionStatus.InsufficientQuantity => "Upgrade unavailable: not enough compatible source items remain.",
            UpgradeTransactionStatus.InventoryFull => "Upgrade could not be completed: inventory is full.",
            UpgradeTransactionStatus.InvalidTarget => "Upgrade unavailable: target item is invalid.",
            UpgradeTransactionStatus.TransactionFailed => "Upgrade could not be completed safely.",
            _ => "Upgrade could not be completed."
        };
    }

    private void DrawItemSlot(SpriteBatch b, ClickableComponent slot, string label, Item? item, int? value, int quantity = 1, bool enabled = true)
    {
        MenuDrawing.CenteredText(b, label, slot.bounds.Center.X, slot.bounds.Y - Scale(LabelGap), scale: layoutScale);
        MenuDrawing.Slot(b, slot.bounds, item, slot.containsPoint(Game1.getMouseX(true), Game1.getMouseY(true)), enabled);
        string name = item?.DisplayName ?? "Empty";
        MenuDrawing.CenteredText(b, MenuDrawing.FitText(name, width / 3), slot.bounds.Center.X,
            slot.bounds.Bottom + Scale(ItemNameGap), scale: layoutScale);
        if (item is not null)
        {
            string valueText = value.HasValue
                ? quantity == 1 ? $"{value.Value:N0}g / item" : $"{quantity} × {value.Value:N0}g = {(long)quantity * value.Value:N0}g"
                : "Value unavailable";
            MenuDrawing.CenteredText(b, valueText,
                slot.bounds.Center.X, slot.bounds.Bottom + Scale(ItemValueGap), scale: layoutScale);
        }
    }
}
