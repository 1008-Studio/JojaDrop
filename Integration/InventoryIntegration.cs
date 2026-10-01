using JojaDrop.Services;
using JojaDrop.UI;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace JojaDrop.Integration;

internal sealed class InventoryIntegration
{
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly ItemValueService itemValues;
    private readonly UpgradeRoller upgradeRoller;
    private readonly UpgradeTransactionService transactionService;
    private readonly TargetItemProvider targetItemProvider;
    private readonly PerScreen<UpgradeButton> buttons;
    private readonly Texture2D wheelArrow;
    private readonly Texture2D wheelCenter;
    private readonly Texture2D wheelFrame;
    private float hiddenCursorTransparency;
    private bool restoreCursor;

    public InventoryIntegration(IModHelper helper, IMonitor monitor, ItemValueService itemValues, UpgradeRoller upgradeRoller,
        UpgradeTransactionService transactionService)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.itemValues = itemValues;
        this.upgradeRoller = upgradeRoller;
        this.transactionService = transactionService;
        targetItemProvider = new TargetItemProvider(itemValues, monitor);
        Texture2D buttonTexture = helper.ModContent.Load<Texture2D>("assets/upgrade-button.png");
        wheelArrow = helper.ModContent.Load<Texture2D>("assets/wheel/wheel_arrow.png");
        wheelCenter = helper.ModContent.Load<Texture2D>("assets/wheel/wheel_center.png");
        wheelFrame = helper.ModContent.Load<Texture2D>("assets/wheel/wheel_frame.png");
        buttons = new PerScreen<UpgradeButton>(() => new UpgradeButton(buttonTexture));
    }

    public void RegisterEvents()
    {
        helper.Events.Display.RenderingActiveMenu += OnRenderingActiveMenu;
        helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
    }

    /// <summary>The active menu when it's the player's inventory, even if another menu or overlay
    /// was opened over it. The game draws the inventory page first and any child menu on top of it,
    /// so the inventory stays on screen and the button must stay visible with it.</summary>
    private static GameMenu? GetVisibleInventoryMenu()
    {
        return Context.IsWorldReady
            && Game1.activeClickableMenu is GameMenu menu
            && menu.GetCurrentPage() is InventoryPage
            ? menu
            : null;
    }

    /// <summary>The active inventory menu when no other menu is opened over it, i.e. when the
    /// button may be interacted with.</summary>
    private static GameMenu? GetInventoryMenu()
    {
        return GetVisibleInventoryMenu() is { } menu
            && menu.GetChildMenu() is null
            && menu.GetCurrentPage().GetChildMenu() is null
            ? menu
            : null;
    }

    private void OnRenderingActiveMenu(object? sender, RenderingActiveMenuEventArgs e)
    {
        if (GetVisibleInventoryMenu() is null)
            return;

        hiddenCursorTransparency = Game1.mouseCursorTransparency;
        Game1.mouseCursorTransparency = 0f;
        restoreCursor = true;
    }

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        if (!restoreCursor)
            return;

        Game1.mouseCursorTransparency = hiddenCursorTransparency;
        restoreCursor = false;

        GameMenu? menu = GetVisibleInventoryMenu();
        if (menu is null)
            return;

        // The button stays visible whenever the inventory is on screen, including when another
        // menu/overlay is opened over it; it's drawn disabled exactly when the input handler
        // below refuses to interact with it.
        UpgradeButton button = buttons.Value;
        button.UpdateLayout(menu);
        button.Draw(e.SpriteBatch, Game1.getMouseX(true), Game1.getMouseY(true),
            enabled: GetInventoryMenu() is not null && menu.readyToClose());
        menu.drawMouse(e.SpriteBatch);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        GameMenu? menu = GetInventoryMenu();
        if (menu is null || !menu.readyToClose() || helper.Input.IsSuppressed(e.Button))
            return;

        UpgradeButton button = buttons.Value;
        button.UpdateLayout(menu);
        bool clicked = e.Button == SButton.MouseLeft
            && button.Contains(Game1.getMouseX(true), Game1.getMouseY(true));
        bool shortcut = e.Button is SButton.U or SButton.RightStick;
        if (!clicked && !shortcut)
            return;

        if (Context.IsMultiplayer && !Context.IsMainPlayer)
        {
            Game1.showRedMessage("JojaDrop upgrades are host-only in multiplayer.");
            monitor.Log("Blocked a farmhand from opening the local upgrade menu.", LogLevel.Trace);
            return;
        }

        helper.Input.Suppress(e.Button);
        Game1.playSound("bigSelect");
        Game1.activeClickableMenu = new UpgradeMenu(itemValues, upgradeRoller, transactionService,
            targetItemProvider, monitor, wheelArrow, wheelCenter, wheelFrame);
        monitor.Log("Opened JojaDrop upgrader.", LogLevel.Trace);
    }
}
