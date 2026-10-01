using JojaDrop.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace JojaDrop.UI;

internal sealed class SourceItemMenu : IClickableMenu
{
    private const int PanelWidth = 848;
    private const int PanelHeight = 416;
    private const int ContentPadding = 40;
    private const int GridTopOffset = 112;
    private const int FooterHeight = 96;
    private const int MaxColumns = 12;
    private const int MaxRows = 3;
    private const int PageButtonWidth = 136;
    private const int PageButtonHeight = 56;
    private const int TitleOffset = 28;
    private const int InstructionOffset = 72;
    private const int PageButtonBottomGap = 24;
    private const int PageLabelOffset = 16;
    private const int SlotIdOffset = 1000;
    private const int PreviousId = 900;
    private const int NextId = 901;
    private const int CloseId = 902;
    private readonly ItemValueService itemValues;
    private readonly List<ClickableComponent> slots = new();
    private ClickableComponent previousButton = null!;
    private ClickableComponent nextButton = null!;
    private Item? selectedItem;
    private string hoverText = "";
    private Point viewportSize;
    private int columns;
    private int pageSize;
    private int page;

    private static int InventorySize => Math.Min(Game1.player.MaxItems, Game1.player.Items.Count);
    private int PageCount => Math.Max(1, (InventorySize + pageSize - 1) / pageSize);

    public SourceItemMenu(ItemValueService itemValues, Action<Item?> onClosed)
    {
        this.itemValues = itemValues;
        exitFunction = () => onClosed(selectedItem);
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
        int rows = Math.Clamp((height - GridTopOffset - FooterHeight) / MenuDrawing.SlotSize, 1, MaxRows);
        pageSize = columns * rows;
        page = Math.Clamp(page, 0, PageCount - 1);
        slots.Clear();
        int gridLeft = xPositionOnScreen + (width - columns * MenuDrawing.SlotSize) / 2;
        int count = Math.Min(pageSize, InventorySize - page * pageSize);

        for (int i = 0; i < count; i++)
        {
            slots.Add(new ClickableComponent(new Rectangle(gridLeft + i % columns * MenuDrawing.SlotSize,
                yPositionOnScreen + GridTopOffset + i / columns * MenuDrawing.SlotSize,
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

            Item? item = GetItem(i);
            if (item is null || itemValues.GetValue(item) is null)
                return;

            // Store only a reference. Do not call inventory click/transfer APIs or change Stack.
            selectedItem = item;
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

    private Item? GetItem(int slotIndex)
    {
        int inventoryIndex = page * pageSize + slotIndex;
        return inventoryIndex < InventorySize ? Game1.player.Items[inventoryIndex] : null;
    }

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        hoverText = "";
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].containsPoint(x, y))
                continue;

            Item? item = GetItem(i);
            if (item is not null)
            {
                int? value = itemValues.GetValue(item);
                hoverText = value.HasValue ? $"{item.DisplayName}\n{value.Value:N0} pts / item\nSelect without moving this item."
                    : $"{item.DisplayName}\nThis item has no supported upgrade value yet.";
            }
            break;
        }
    }

    public override void draw(SpriteBatch b)
    {
        MenuDrawing.Panel(b, this);
        int centerX = xPositionOnScreen + width / 2;
        MenuDrawing.CenteredText(b, "Your Item", centerX, yPositionOnScreen + TitleOffset, scale: 1.25f);
        MenuDrawing.CenteredText(b, MenuDrawing.FitText("Choose an item. It stays in your backpack.", width - ContentPadding * 2),
            centerX, yPositionOnScreen + InstructionOffset);
        int mouseX = Game1.getMouseX(true);
        int mouseY = Game1.getMouseY(true);
        for (int i = 0; i < slots.Count; i++)
        {
            Item? item = GetItem(i);
            bool supported = item is null || itemValues.GetValue(item).HasValue;
            MenuDrawing.Slot(b, slots[i].bounds, item, slots[i].containsPoint(mouseX, mouseY), supported);
        }
        MenuDrawing.TextButton(b, previousButton, "Previous", previousButton.containsPoint(mouseX, mouseY), page > 0);
        MenuDrawing.TextButton(b, nextButton, "Next", nextButton.containsPoint(mouseX, mouseY), page + 1 < PageCount);
        MenuDrawing.CenteredText(b, $"{page + 1} / {PageCount}", centerX, nextButton.bounds.Y + PageLabelOffset);
        base.draw(b);
        if (hoverText.Length > 0)
            drawHoverText(b, hoverText, Game1.smallFont);
        drawMouse(b);
    }
}
