using JojaDrop.Models;
using JojaDrop.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace JojaDrop.UI;

internal sealed class TargetItemMenu : IClickableMenu
{
    private const int PanelWidth = 848;
    private const int ContentPadding = 40;
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
    private const int SearchId = 903;
    private const int SearchTopOffset = 104;
    private const int SearchRowGap = 16;
    private const int SearchBoxWidth = 360;
    private const int SearchLabelGap = 8;
    private const int GridBottomSlack = 28;
    private const string SearchLabel = "Search:";
    private const string SearchHoverText = "Type an item name to filter targets.";

    private readonly IReadOnlyList<TargetItemOption> targetOptions;
    /// <summary>The options rendered by the grid: <see cref="targetOptions"/> narrowed down by the
    /// current search text. The target list itself is never rebuilt here, so the existing
    /// eligibility and probability rules stay untouched.</summary>
    private IReadOnlyList<TargetItemOption> visibleOptions;
    private readonly TargetFilterMode filter;
    /// <summary>The vanilla text box reused for searching; it handles keyboard input and drawing.</summary>
    private readonly TextBox searchBox;
    private ClickableComponent searchBoxComponent = null!;
    private readonly List<ClickableComponent> slots = new();
    private ClickableComponent previousButton = null!;
    private ClickableComponent nextButton = null!;
    private TargetItemOption? selectedOption;
    private string hoverText = "";
    private string searchText = "";
    private Point viewportSize;
    private int columns;
    private int pageSize;
    private int page;
    private int gridTopOffset;
    private int searchLabelCenterX;

    private int PageCount => Math.Max(1, (visibleOptions.Count + pageSize - 1) / pageSize);

    public TargetItemMenu(IReadOnlyList<TargetItemOption> targetOptions, Action<TargetItemOption?> onClosed,
        TargetFilterMode filter = TargetFilterMode.All)
    {
        this.targetOptions = targetOptions ?? throw new ArgumentNullException(nameof(targetOptions));
        ArgumentNullException.ThrowIfNull(onClosed);
        visibleOptions = targetOptions;
        this.filter = filter;
        // Reuse the vanilla text box (the same component NumberSelectionMenu and the options menu
        // use); it subscribes itself to the game's keyboard dispatcher while it's selected.
        searchBox = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Color.Black);
        exitFunction = () =>
        {
            // Never leave the game typing into a menu that's gone.
            if (Game1.keyboardDispatcher.Subscriber == searchBox)
                Game1.keyboardDispatcher.Subscriber = null;
            onClosed(selectedOption);
        };
        UpdateLayout();
    }

    private int ComputePanelHeight()
    {
        // The search row sits between the instruction line and the grid, so the panel grows
        // with the vanilla text box height instead of squeezing the grid.
        return SearchTopOffset + searchBox.Height + SearchRowGap + MaxRows * CellHeight + FooterHeight + GridBottomSlack;
    }

    private Rectangle SearchBoxBounds => new(searchBox.X, searchBox.Y, searchBox.Width, searchBox.Height);

    /// <summary>Recompute menu geometry and rebuild the slot list. When <paramref name="snapCursor"/>
    /// is set (menu open, resize, page change), the hardware cursor also snaps to the default
    /// component; search filtering passes <c>false</c> because it runs on every keystroke.</summary>
    private void UpdateLayout(bool snapCursor = true)
    {
        viewportSize = new Point(Game1.uiViewport.Width, Game1.uiViewport.Height);
        width = Math.Min(PanelWidth, Math.Max(1, viewportSize.X - MenuDrawing.ScreenMargin * 2));
        height = Math.Min(ComputePanelHeight(), Math.Max(1, viewportSize.Y - MenuDrawing.ScreenMargin * 2));
        xPositionOnScreen = (viewportSize.X - width) / 2;
        yPositionOnScreen = (viewportSize.Y - height) / 2;

        // Search row: a "Search:" label and the text box, centred together on the panel.
        int labelWidth = (int)Game1.smallFont.MeasureString(SearchLabel).X;
        int boxWidth = Math.Max(1, Math.Min(SearchBoxWidth, width - ContentPadding * 2 - labelWidth - SearchLabelGap));
        int groupWidth = labelWidth + SearchLabelGap + boxWidth;
        int groupLeft = xPositionOnScreen + (width - groupWidth) / 2;
        searchLabelCenterX = groupLeft + labelWidth / 2;
        searchBox.X = groupLeft + labelWidth + SearchLabelGap;
        searchBox.Y = yPositionOnScreen + SearchTopOffset;
        searchBox.Width = boxWidth;
        searchBoxComponent = new ClickableComponent(SearchBoxBounds, "Search")
        {
            myID = SearchId, leftNeighborID = PreviousId, rightNeighborID = NextId,
            upNeighborID = CloseId, downNeighborID = SlotIdOffset
        };
        gridTopOffset = SearchTopOffset + searchBox.Height + SearchRowGap;

        columns = Math.Clamp((width - ContentPadding * 2) / MenuDrawing.SlotSize, 1, MaxColumns);
        int rows = Math.Clamp((height - gridTopOffset - FooterHeight) / CellHeight, 1, MaxRows);
        pageSize = columns * rows;
        page = Math.Clamp(page, 0, PageCount - 1);
        slots.Clear();

        int gridLeft = xPositionOnScreen + (width - columns * MenuDrawing.SlotSize) / 2;
        int count = Math.Min(pageSize, visibleOptions.Count - page * pageSize);
        for (int i = 0; i < count; i++)
        {
            slots.Add(new ClickableComponent(new Rectangle(gridLeft + i % columns * MenuDrawing.SlotSize,
                yPositionOnScreen + gridTopOffset + i / columns * CellHeight,
                MenuDrawing.SlotSize, MenuDrawing.SlotSize), (page * pageSize + i).ToString())
            {
                myID = SlotIdOffset + i,
                leftNeighborID = i % columns > 0 ? SlotIdOffset + i - 1 : PreviousId,
                rightNeighborID = i % columns < columns - 1 && i + 1 < count ? SlotIdOffset + i + 1 : NextId,
                upNeighborID = i >= columns ? SlotIdOffset + i - columns : SearchId,
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
        allClickableComponents = new List<ClickableComponent>(slots) { previousButton, nextButton, searchBoxComponent, upperRightCloseButton };

        if (Game1.options.SnappyMenus)
        {
            if (snapCursor)
                snapToDefaultClickableComponent();
            else if (currentlySnappedComponent != null)
                setCurrentlySnappedComponentTo(currentlySnappedComponent.myID); // re-point at the rebuilt list without moving the cursor
        }
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

        // Only react to text changes here. The text box itself is updated on click instead:
        // TextBox.Update() derives focus from the cursor position, so running it every frame
        // would make focus follow the cursor and silently drop typing once the mouse moves away.
        if (!string.Equals(searchText, searchBox.Text, StringComparison.Ordinal))
            ApplySearch();
    }

    /// <summary>Re-narrow the visible options from the search text without touching the
    /// economics: the filter only removes entries from the already validated target list
    /// and keeps its existing order.</summary>
    private void ApplySearch()
    {
        searchText = searchBox.Text;
        visibleOptions = string.IsNullOrWhiteSpace(searchText)
            ? targetOptions
            : targetOptions
                .Where(option => Utility.fuzzyCompare(searchText, option.DisplayName) is not null)
                .ToArray();
        hoverText = "";
        // Rebuild the slots without moving the cursor: this runs on every keystroke.
        UpdateLayout(snapCursor: false);
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds) => UpdateLayout();

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        // Re-derive text box focus on click, exactly like vanilla menus (see NamingMenu):
        // this focuses the box when the click lands on it, and releases it for any other click.
        searchBox.Update();

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
        if (SearchBoxBounds.Contains(x, y))
        {
            // Update() above already focused it for a normal mouse click; SelectMe() keeps the
            // focus guaranteed even if the click came from a snapped gamepad cursor.
            searchBox.SelectMe();
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
        {
            exitThisMenu();
            return;
        }

        // While the search box has focus, character keys belong to the search text; the menu's
        // own close/movement keys stay untouched when it's not focused.
        if (searchBox.Selected)
            return;
        base.receiveKeyPress(key);
    }

    public override void receiveGamePadButton(Buttons button)
    {
        // Same as NamingMenu: while the search box has focus, directional input releases it so
        // the player can navigate the grid again (the receiveKeyPress guard blocks movement keys).
        if (searchBox.Selected && button is Buttons.DPadUp or Buttons.DPadDown or Buttons.DPadLeft or Buttons.DPadRight
            or Buttons.LeftThumbstickLeft or Buttons.LeftThumbstickUp or Buttons.LeftThumbstickDown or Buttons.LeftThumbstickRight)
        {
            searchBox.Selected = false;
        }

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

    private TargetItemOption GetOption(int slotIndex) => visibleOptions[page * pageSize + slotIndex];

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        hoverText = "";
        if (SearchBoxBounds.Contains(x, y))
        {
            hoverText = SearchHoverText;
            return;
        }
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].containsPoint(x, y))
                continue;

            TargetItemOption option = GetOption(i);
            hoverText = $"{option.DisplayName}\n{option.Value:N0} pts\nx{option.BatchMultiplier:0.00}\n{option.BatchChance * 100:0.00}% batch chance";
            break;
        }
    }

    public override void draw(SpriteBatch b)
    {
        MenuDrawing.Panel(b, this);
        int centerX = xPositionOnScreen + width / 2;
        MenuDrawing.CenteredText(b, "Choose Target", centerX, yPositionOnScreen + TitleOffset, scale: 1.25f);
        string instruction = filter == TargetFilterMode.All
            ? "Choose an item to upgrade toward."
            : $"Showing targets with ~{TargetProbabilityFilter.GetChanceText(filter)} upgrade chance.";
        MenuDrawing.CenteredText(b, MenuDrawing.FitText(instruction, width - ContentPadding * 2),
            centerX, yPositionOnScreen + InstructionOffset);
        int labelHeight = (int)Game1.smallFont.MeasureString(SearchLabel).Y;
        MenuDrawing.CenteredText(b, SearchLabel, searchLabelCenterX,
            searchBox.Y + Math.Max(0, (searchBox.Height - labelHeight) / 2));
        searchBox.Draw(b);
        if (visibleOptions.Count == 0)
        {
            string emptyMessage = targetOptions.Count == 0
                ? (filter == TargetFilterMode.All ? "No target items available." : "No targets match this filter.")
                : "No targets match your search.";
            MenuDrawing.CenteredText(b, emptyMessage,
                centerX, yPositionOnScreen + (gridTopOffset + height - FooterHeight) / 2);
        }
        int mouseX = Game1.getMouseX(true);
        int mouseY = Game1.getMouseY(true);
        for (int i = 0; i < slots.Count; i++)
        {
            TargetItemOption option = GetOption(i);
            bool valid = option.Value > 0;
            MenuDrawing.Slot(b, slots[i].bounds, option.PreviewItem, slots[i].containsPoint(mouseX, mouseY), valid);
            MenuDrawing.CenteredText(b, MenuDrawing.FitText($"{option.Value:N0} pts", slots[i].bounds.Width),
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
