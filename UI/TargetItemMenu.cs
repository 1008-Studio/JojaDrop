using JojaDrop.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace JojaDrop.UI;

internal sealed class TargetItemMenu : IClickableMenu
{
    private const int PanelWidth = 848;
    private const int PanelHeight = 488;
    private const int ContentPadding = 40;
    private const int GridTopOffset = 112;
    private const int FooterHeight = 96;
    private const int CellHeight = 84;
    private const int MaxColumns = 12;
    private const int MaxRows = 3;
    private const int PageButtonWidth = 136;
    private const int PageButtonHeight = 56;
    private const int TitleOffset = 28;
    private const int InstructionOffset = 72;
    private const int PageButtonBottomGap = 24;
    private const int PageLabelOffset = 16;
    private const int ValueOffset = 2;
    private const int SlotIdOffset = 1000;
    private const int PreviousId = 900;
    private const int NextId = 901;
    private const int CloseId = 902;

    private readonly IReadOnlyList<TargetItemOption> targetOptions;
    private readonly List<ClickableComponent> slots = new();
    private ClickableComponent previousButton = null!;
    private ClickableComponent nextButton = null!;
    private TargetItemOption? selectedOption;
    private string hoverText = "";
    private Point viewportSize;
    private int columns;
    private int pageSize;
    private int page;

    private int PageCount => Math.Max(1, (targetOptions.Count + pageSize - 1) / pageSize);

    public TargetItemMenu(IReadOnlyList<TargetItemOption> targetOptions, Action<TargetItemOption?> onClosed)
    {
        this.targetOptions = targetOptions ?? throw new ArgumentNullException(nameof(targetOptions));
        ArgumentNullException.ThrowIfNull(onClosed);
        exitFunction = () => onClosed(selectedOption);
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        viewportSize = new Point(Game1.uiViewport.Width, Game1.uiViewport.Height);
        width = Math.Min(PanelWidth, Math.Max(1, viewportSize.X - MenuDrawing.ScreenMargin * 2));
        height = Math.Min(PanelHeight, Math.Max(1, viewportSize.Y - MenuDrawing.ScreenMargin * 2));
        xPositionOnScreen = (viewportSize.X - width) / 2;
        yPositionOnScreen = (viewportSize.Y - height) / 2;
        columns = Math.Clamp((width - ContentPadding * 2) / MenuDrawing.SlotSize, 1, MaxColumns);
        int rows = Math.Clamp((height - GridTopOffset - FooterHeight) / CellHeight, 1, MaxRows);
        pageSize = columns * rows;
        page = Math.Clamp(page, 0, PageCount - 1);
        slots.Clear();

        int gridLeft = xPositionOnScreen + (width - columns * MenuDrawing.SlotSize) / 2;
        int count = Math.Min(pageSize, targetOptions.Count - page * pageSize);
        for (int i = 0; i < count; i++)
        {
            slots.Add(new ClickableComponent(new Rectangle(gridLeft + i % columns * MenuDrawing.SlotSize,
                yPositionOnScreen + GridTopOffset + i / columns * CellHeight,
                MenuDrawing.SlotSize, MenuDrawing.SlotSize), (page * pageSize + i).ToString())
            {
                myID = SlotIdOffset + i,
                leftNeighborID = i % columns > 0 ? SlotIdOffset + i - 1 : PreviousId,
                rightNeighborID = i % columns < columns - 1 && i + 1 < count ? SlotIdOffset + i + 1 : NextId,
                upNeighborID = i >= columns ? SlotIdOffset + i - columns : CloseId,
                downNeighborID = i + columns < count ? SlotIdOffset + i + columns : NextId
            });
        }

        int buttonTop = yPositionOnScreen + height - PageButtonHeight - PageButtonBottomGap;
        previousButton = new ClickableComponent(new Rectangle(xPositionOnScreen + ContentPadding,
            buttonTop, PageButtonWidth, PageButtonHeight), "Previous")
        {
            myID = PreviousId, rightNeighborID = NextId, upNeighborID = SlotIdOffset
        };
        nextButton = new ClickableComponent(new Rectangle(xPositionOnScreen + width - ContentPadding - PageButtonWidth,
            buttonTop, PageButtonWidth, PageButtonHeight), "Next")
        {
            myID = NextId, leftNeighborID = PreviousId, upNeighborID = SlotIdOffset
        };
        initializeUpperRightCloseButton();
        upperRightCloseButton.myID = CloseId;
        upperRightCloseButton.downNeighborID = SlotIdOffset;
        upperRightCloseButton.leftNeighborID = SlotIdOffset;
        allClickableComponents = new List<ClickableComponent>(slots) { previousButton, nextButton, upperRightCloseButton };

        if (Game1.options.SnappyMenus)
            snapToDefaultClickableComponent();
    }

    public override void snapToDefaultClickableComponent()
    {
        setCurrentlySnappedComponentTo(slots.Count > 0 ? SlotIdOffset : CloseId);
        snapCursorToCurrentSnappedComponent();
    }

    public override void update(GameTime time)
    {
        base.update(time);
        if (viewportSize.X != Game1.uiViewport.Width || viewportSize.Y != Game1.uiViewport.Height)
            UpdateLayout();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds) => UpdateLayout();

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (upperRightCloseButton.containsPoint(x, y))
        {
            exitThisMenu();
            return;
        }
        if (previousButton.containsPoint(x, y))
        {
            ChangePage(-1);
            return;
        }
        if (nextButton.containsPoint(x, y))
        {
            ChangePage(1);
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].containsPoint(x, y))
                continue;

            TargetItemOption option = GetOption(i);
            if (option.Value <= 0)
                return;

            selectedOption = option;
            exitThisMenu(playSound);
            return;
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
        else if (button == Buttons.LeftShoulder)
            ChangePage(-1);
        else if (button == Buttons.RightShoulder)
            ChangePage(1);
        else
            base.receiveGamePadButton(button);
    }

    public override void receiveScrollWheelAction(int direction) => ChangePage(direction > 0 ? -1 : 1);

    private void ChangePage(int delta)
    {
        int newPage = Math.Clamp(page + delta, 0, PageCount - 1);
        if (newPage == page)
            return;

        page = newPage;
        hoverText = "";
        Game1.playSound("shwip");
        UpdateLayout();
    }

    private TargetItemOption GetOption(int slotIndex) => targetOptions[page * pageSize + slotIndex];

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        hoverText = "";
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].containsPoint(x, y))
                continue;

            TargetItemOption option = GetOption(i);
            hoverText = $"{option.DisplayName}\n{option.Value:N0}g\nx{option.Multiplier:0.00}\n{option.BaseChance * 100:0.00}% base chance (1 → 1)";
            break;
        }
    }

    public override void draw(SpriteBatch b)
    {
        MenuDrawing.Panel(b, this);
        int centerX = xPositionOnScreen + width / 2;
        MenuDrawing.CenteredText(b, "Choose Target", centerX, yPositionOnScreen + TitleOffset, scale: 1.25f);
        MenuDrawing.CenteredText(b, MenuDrawing.FitText("Choose an item to upgrade toward.", width - ContentPadding * 2),
            centerX, yPositionOnScreen + InstructionOffset);
        int mouseX = Game1.getMouseX(true);
        int mouseY = Game1.getMouseY(true);
        for (int i = 0; i < slots.Count; i++)
        {
            TargetItemOption option = GetOption(i);
            bool valid = option.Value > 0;
            MenuDrawing.Slot(b, slots[i].bounds, option.PreviewItem, slots[i].containsPoint(mouseX, mouseY), valid);
            MenuDrawing.CenteredText(b, MenuDrawing.FitText($"{option.Value:N0}g", slots[i].bounds.Width),
                slots[i].bounds.Center.X, slots[i].bounds.Bottom + ValueOffset, scale: 0.75f);
        }
        MenuDrawing.TextButton(b, previousButton, "Previous", previousButton.containsPoint(mouseX, mouseY), page > 0);
        MenuDrawing.TextButton(b, nextButton, "Next", nextButton.containsPoint(mouseX, mouseY), page + 1 < PageCount);
        MenuDrawing.CenteredText(b, $"Page {page + 1} / {PageCount}", centerX, nextButton.bounds.Y + PageLabelOffset);
        base.draw(b);
        if (hoverText.Length > 0)
            drawHoverText(b, hoverText, Game1.smallFont);
        drawMouse(b);
    }
}
