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
    private const int BasePanelWidth = 720;
    private const int BasePanelHeight = 440;
    private const float MenuScale = 1.18f;
    private static readonly int PanelWidth = (int)MathF.Round(BasePanelWidth * MenuScale);
    private static readonly int PanelHeight = (int)MathF.Round(BasePanelHeight * MenuScale);
    private const int TitleOffset = 36;
    private const int SlotTopOffset = 132;
    private const int LabelGap = 40;
    private const int ButtonWidth = 208;
    private const int ButtonHeight = 64;
    private const int ButtonFooterGap = 64;
    private const int QuantityButtonSize = 36;
    // One upgrade operation always produces exactly one target item.
    private const int SingleOutputQuantity = 1;
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
    private const int FilterIdOffset = 108;
    private const int FilterButtonSize = 36;
    private const int FilterButtonGap = 4;
    private const int FilterButtonTextPadding = 4;
    private const int FilterButtonTextReferenceWidth = 30;
    private static readonly TargetFilterMode[] FilterModes =
    {
        TargetFilterMode.X2, TargetFilterMode.X3, TargetFilterMode.X5, TargetFilterMode.X10
    };
    private static readonly string[] FilterLabels = { "x2", "x3", "x5", "x10" };
    private readonly ItemValueService itemValues;
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
    private readonly ClickableComponent[] filterButtons = new ClickableComponent[FilterModes.Length];
    private Item? sourceItem;
    private TargetItemOption? targetOption;
    private int sourceQuantity = 1;
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
    private int lastSpinTickIndex;
    private Item? pendingSource;
    private TargetItemOption? pendingTarget;
    private int pendingSourceQuantity;

    public UpgradeMenu(ItemValueService itemValues, UpgradeRoller upgradeRoller,
        UpgradeTransactionService transactionService, TargetItemProvider targetItemProvider, IMonitor monitor,
        Texture2D wheelArrow, Texture2D wheelCenter, Texture2D wheelFrame)
    {
        this.itemValues = itemValues;
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
        layoutScale = Math.Min(width / (float)BasePanelWidth, height / (float)BasePanelHeight);
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
            myID = TargetId, leftNeighborID = SourceId, rightNeighborID = FilterIdOffset,
            downNeighborID = UpgradeId, upNeighborID = CloseId
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
        sourceDecreaseButton.rightNeighborID = SourceIncreaseId;
        sourceIncreaseButton.leftNeighborID = SourceDecreaseId;
        sourceIncreaseButton.rightNeighborID = TargetId;
        sourceDecreaseButton.upNeighborID = sourceIncreaseButton.upNeighborID = SourceId;
        sourceDecreaseButton.downNeighborID = sourceIncreaseButton.downNeighborID = UpgradeId;

        // Keep the filter row under the target's name and value, centred on its slot.
        int filterSize = Scale(FilterButtonSize);
        int filterGap = Scale(FilterButtonGap);
        int filterWidth = filterButtons.Length * filterSize + (filterButtons.Length - 1) * filterGap;
        int filterLeft = targetSlot.bounds.Center.X - filterWidth / 2;
        int filterTop = targetSlot.bounds.Bottom + Scale(72);
        for (int i = 0; i < filterButtons.Length; i++)
        {
            int id = FilterIdOffset + i;
            filterButtons[i] = new ClickableComponent(
                new Rectangle(filterLeft + i * (filterSize + filterGap), filterTop, filterSize, filterSize), FilterLabels[i])
            {
                myID = id,
                leftNeighborID = i == 0 ? TargetId : id - 1,
                rightNeighborID = i == filterButtons.Length - 1 ? CloseId : id + 1,
                upNeighborID = TargetId,
                downNeighborID = UpgradeId
            };
        }

        initializeUpperRightCloseButton();
        upperRightCloseButton.myID = CloseId;
        upperRightCloseButton.leftNeighborID = TargetId;
        upperRightCloseButton.downNeighborID = TargetId;
        allClickableComponents = new List<ClickableComponent> { sourceSlot, targetSlot, sourceDecreaseButton, sourceIncreaseButton,
            upgradeButton, upperRightCloseButton };
        allClickableComponents.AddRange(filterButtons);

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
            PlaySpinTick();
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
        if (targetSlot.containsPoint(x, y) && sourceItem is not null)
        {
            OpenTargetPicker(TargetFilterMode.All, playSound);
            return;
        }
        if (CanSelectTargetFilter && TryGetFilterAt(x, y, out int filterIndex))
        {
            OpenTargetPicker(FilterModes[filterIndex], playSound);
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
        Game1.activeClickableMenu = new TargetItemMenu(targetItemProvider.GetTargets(sourceItem, sourceQuantity, filter), option =>
        {
            if (option is not null)
            {
                targetOption = option;

                string? selectionError = GetTargetSelectionError();
                if (selectionError is not null)
                {
                    targetOption = null;
                    statusMessage = selectionError;
                    Game1.activeClickableMenu = this;
                    UpdateLayout();
                    return;
                }

                hasRolledCurrentSelection = false;
                isAnimatingRoulette = false;
                rouletteAnimationTimer = 0f;
                pendingSource = null;
                pendingTarget = null;
                pendingSourceQuantity = 0;
                rouletteResult = false;
                statusMessage = "";
            }
            Game1.activeClickableMenu = this;
            UpdateLayout();
        }, filter);
    }

    /// <summary>Why the freshly picked target cannot be used, or null when it is valid.</summary>
    private string? GetTargetSelectionError()
    {
        if (sourceItem is null)
            return "Upgrade unavailable: source item is no longer in your inventory.";

        int? sourceValue = itemValues.GetValue(sourceItem);
        int? targetValue = targetOption is null ? null : itemValues.GetValue(targetOption.PreviewItem);
        if (!sourceValue.HasValue || sourceValue.Value <= 0 || !targetValue.HasValue || targetValue.Value <= 0)
            return "Upgrade unavailable: target item has no valid value.";

        return TargetEconomics.IsEligible(sourceQuantity, sourceValue.Value, sourceItem.QualifiedItemId,
                targetValue.Value, targetOption?.QualifiedItemId)
            ? null
            : "Upgrade unavailable: target must be worth more than the source batch.";
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
            : targetSlot.containsPoint(x, y) && sourceItem is null ? "Select Your Item first."
            : targetSlot.containsPoint(x, y) ? "Choose a target item."
            : TryGetFilterHoverText(x, y, out string filterHoverText) ? filterHoverText
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
        DrawItemSlot(b, targetSlot, $"Target ×{SingleOutputQuantity}", targetOption?.PreviewItem, targetOption?.Value, SingleOutputQuantity, sourceItem is not null);
        DrawQuantityControl(b, sourceDecreaseButton, sourceIncreaseButton, sourceQuantity, CanDecreaseSource, CanIncreaseSource);
        DrawTargetFilterButtons(b);

        Vector2 wheelPosition = new(xPositionOnScreen + width / 2f, yPositionOnScreen + height / 2f - Scale(40));
        string chanceText = GetChanceText();
        string multiplierText = GetMultiplierText();
        Rectangle wheelBounds = RouletteWheel.GetFrameBounds(wheelPosition, layoutScale);
        int textGap = Scale(2);
        int textMargin = Scale(2);
        int availableTop = wheelBounds.Bottom + textMargin;
        int availableBottom = upgradeButton.bounds.Top - textMargin;
        int availableHeight = Math.Max(0, availableBottom - availableTop);
        float chanceScale = 1.8f * layoutScale;
        float multiplierScale = chanceScale * 0.45f;
        float textHeight = Game1.smallFont.MeasureString(chanceText).Y * chanceScale
            + Game1.smallFont.MeasureString(multiplierText).Y * multiplierScale;
        float fitScale = textHeight <= 0 ? 1f : Math.Min(1f, Math.Max(0f, (availableHeight - textGap) / textHeight));
        chanceScale *= fitScale;
        multiplierScale *= fitScale;
        int chanceHeight = (int)Math.Ceiling(Game1.smallFont.MeasureString(chanceText).Y * chanceScale);
        int multiplierHeight = (int)Math.Ceiling(Game1.smallFont.MeasureString(multiplierText).Y * multiplierScale);
        int blockHeight = chanceHeight + textGap + multiplierHeight;
        int textTop = availableTop + Math.Max(0, (availableHeight - blockHeight) / 2);
        MenuDrawing.CenteredText(b, chanceText, xPositionOnScreen + width / 2, textTop, Color.SteelBlue, chanceScale);
        MenuDrawing.CenteredText(b, multiplierText, xPositionOnScreen + width / 2, textTop + chanceHeight + textGap,
            Color.SteelBlue, multiplierScale);

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
    private bool CanSelectTargetFilter => sourceItem is not null;

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

        if (!TargetEconomics.IsEligible(sourceQuantity, sourceValue.Value, sourceItem.QualifiedItemId,
                targetValue.Value, targetOption.QualifiedItemId))
            return false;

        chance = TargetEconomics.CalculateChance(sourceQuantity, sourceValue.Value, targetValue.Value);
        multiplier = TargetEconomics.CalculateMultiplier(sourceQuantity, sourceValue.Value, targetValue.Value);
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
            if (!TargetEconomics.IsEligible(sourceQuantity, sourceValue.Value, source.QualifiedItemId,
                    targetValue.Value, targetPreview.QualifiedItemId))
            {
                statusMessage = "Upgrade unavailable: target must be worth more than the source batch.";
                return;
            }

            double chance = TargetEconomics.CalculateChance(sourceQuantity, sourceValue.Value, targetValue.Value);
            bool success = upgradeRoller.Roll(chance);

            // The roll is decided now, but the transaction is applied only after the
            // spin finishes so nothing is revealed while the needle is still moving.
            currentChance = chance;
            rouletteResult = success;
            rouletteTargetAngle = RouletteWheel.CalculateTargetAngle(chance, success);
            pendingSource = source;
            pendingTarget = targetOption;
            pendingSourceQuantity = sourceQuantity;
            hasRolledCurrentSelection = true;
            isAnimatingRoulette = true;
            rouletteAnimationTimer = 0f;
            lastSpinTickIndex = GetSpinBoundaryIndex(RouletteWheel.CalculatePointerAngle(0f, true, rouletteTargetAngle));
            Game1.playSound("cowboy_monsterhit");
        }
        finally
        {
            isProcessingUpgrade = false;
        }
    }

    /// <summary>
    /// Plays the Stardew Valley Fair wheel tick cue (<c>Cowboy_gunshot</c>) whenever the
    /// needle crosses a sector edge, mirroring <c>WheelSpinGame</c>. The cue is one-shot,
    /// so nothing needs to be stopped: ticks stop by themselves once the spin ends.
    /// </summary>
    private void PlaySpinTick()
    {
        float angle = RouletteWheel.CalculatePointerAngle(rouletteAnimationTimer, true, rouletteTargetAngle);
        int tickIndex = GetSpinBoundaryIndex(angle);
        if (tickIndex == lastSpinTickIndex)
            return;

        lastSpinTickIndex = tickIndex;
        Game1.playSound("Cowboy_gunshot");
    }

    /// <summary>
    /// Counts sector edges passed by the needle: one at 12 o'clock, one at the end of the
    /// winning sector. Increments once per edge, so an unchanged value means no tick.
    /// </summary>
    private int GetSpinBoundaryIndex(float angle)
    {
        const float twoPi = MathF.PI * 2f;
        float winningAngle = Math.Clamp((float)currentChance * twoPi, 0f, twoPi);
        return (int)MathF.Floor(angle / twoPi) + (int)MathF.Floor((angle - winningAngle) / twoPi);
    }

    private void CompleteUpgrade()
    {
        if (pendingSource is null || pendingTarget is null)
            return;

        Item source = pendingSource;
        Item targetPreview = pendingTarget.PreviewItem;
        int sourceQuantity = pendingSourceQuantity;
        pendingSource = null;
        pendingTarget = null;
        pendingSourceQuantity = 0;

        if (sourceQuantity < 1)
        {
            statusMessage = "Upgrade unavailable: invalid batch quantity.";
            hasRolledCurrentSelection = false;
            return;
        }

        int? sourceValue = itemValues.GetValue(source);
        int? targetValue = itemValues.GetValue(targetPreview);
        if (!IsTargetValid(targetPreview) || !sourceValue.HasValue || sourceValue.Value <= 0
            || !targetValue.HasValue || targetValue.Value <= 0
            || !TargetEconomics.IsEligible(sourceQuantity, sourceValue.Value, source.QualifiedItemId,
                targetValue.Value, targetPreview.QualifiedItemId))
        {
            statusMessage = "Upgrade unavailable: target is no longer valid.";
            hasRolledCurrentSelection = false;
            return;
        }

        UpgradeTransactionResult transaction = transactionService.Apply(Game1.player, source, targetPreview,
            sourceQuantity, SingleOutputQuantity, rouletteResult);
        monitor.Log($"Upgrade attempt: source={source.QualifiedItemId}; q={sourceQuantity}; sourceValue={sourceValue.Value}; "
            + $"sourceTotal={(long)sourceQuantity * sourceValue.Value}; target={targetPreview.QualifiedItemId}; r={SingleOutputQuantity}; "
            + $"targetValue={targetValue.Value}; targetTotal={(long)SingleOutputQuantity * targetValue.Value}; chance={currentChance:0.####}; "
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
        availableQuantity = sourceItem is null ? 0 : inventory.GetCompatibleQuantity(Game1.player, sourceItem);
    }

    private void RefreshQuantityState()
    {
        availableQuantity = sourceItem is null ? 0 : inventory.GetCompatibleQuantity(Game1.player, sourceItem);
    }

    private static ClickableComponent CreateQuantityButton(int x, int y, int size, int id) => new(new Rectangle(x, y, size, size), "Quantity") { myID = id };

    private bool TryGetFilterAt(int x, int y, out int filterIndex)
    {
        for (int i = 0; i < filterButtons.Length; i++)
        {
            if (filterButtons[i].containsPoint(x, y))
            {
                filterIndex = i;
                return true;
            }
        }

        filterIndex = -1;
        return false;
    }

    private bool TryGetFilterHoverText(int x, int y, out string text)
    {
        for (int i = 0; i < filterButtons.Length; i++)
        {
            if (!filterButtons[i].containsPoint(x, y))
                continue;

            text = CanSelectTargetFilter
                ? $"Targets with ~{TargetProbabilityFilter.GetChanceText(FilterModes[i])} upgrade chance."
                : "Select Your Item first.";
            return true;
        }

        text = "";
        return false;
    }

    private void DrawTargetFilterButtons(SpriteBatch b)
    {
        int mouseX = Game1.getMouseX(true);
        int mouseY = Game1.getMouseY(true);
        bool enabled = CanSelectTargetFilter;
        for (int i = 0; i < filterButtons.Length; i++)
        {
            ClickableComponent button = filterButtons[i];
            MenuDrawing.TextButton(b, button, FilterLabels[i], button.containsPoint(mouseX, mouseY), enabled,
                GetFilterTextScale(FilterLabels[i]));
        }
    }

    private float GetFilterTextScale(string label)
    {
        // Labels keep the size they had at the original button width, so growing the
        // button never grows its text.
        float naturalWidth = Game1.smallFont.MeasureString(label).X;
        return naturalWidth <= 0
            ? layoutScale
            : Math.Min(layoutScale, (Scale(FilterButtonTextReferenceWidth) - Scale(FilterButtonTextPadding)) / naturalWidth);
    }

    private void ChangeSourceQuantity(int delta)
    {
        RefreshQuantityState();
        if (availableQuantity < 1)
            return;

        int newQuantity = Math.Clamp(sourceQuantity + delta, 1, availableQuantity);
        if (newQuantity == sourceQuantity)
            return;

        sourceQuantity = newQuantity;
        RefreshSelectedTargetForSourceQuantity();
    }

    private void RefreshSelectedTargetForSourceQuantity()
    {
        if (sourceItem is null || targetOption is null)
            return;

        int? sourceValue = itemValues.GetValue(sourceItem);
        int? targetValue = itemValues.GetValue(targetOption.PreviewItem);
        if (!sourceValue.HasValue || !targetValue.HasValue
            || !TargetEconomics.IsEligible(sourceQuantity, sourceValue.Value, sourceItem.QualifiedItemId,
                targetValue.Value, targetOption.QualifiedItemId))
        {
            targetOption = null;
            hasRolledCurrentSelection = false;
            statusMessage = "Selected target no longer qualifies for this source quantity.";
            return;
        }

        targetOption = targetOption with
        {
            BatchChance = TargetEconomics.CalculateChance(sourceQuantity, sourceValue.Value, targetValue.Value),
            BatchMultiplier = TargetEconomics.CalculateMultiplier(sourceQuantity, sourceValue.Value, targetValue.Value)
        };
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
                ? quantity == 1 ? $"{value.Value:N0} pts / item" : $"{quantity} × {value.Value:N0} pts = {(long)quantity * value.Value:N0} pts"
                : "Value unavailable";
            MenuDrawing.CenteredText(b, valueText,
                slot.bounds.Center.X, slot.bounds.Bottom + Scale(ItemValueGap), scale: layoutScale);
        }
    }
}
