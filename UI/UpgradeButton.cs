using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace JojaDrop.UI;

internal sealed class UpgradeButton
{
    private const int Size = 64;
    private const int Gap = 16;
    private const int TopOffset = 96;
    private static readonly Rectangle ArrowSource = new(421, 459, 11, 12);
    private Rectangle bounds;
    private bool visible;

    public void UpdateLayout(GameMenu menu)
    {
        int screenWidth = Game1.uiViewport.Width;
        int screenHeight = Game1.uiViewport.Height;
        int menuRight = menu.xPositionOnScreen + menu.width;
        int menuBottom = menu.yPositionOnScreen + menu.height;
        visible = false;

        // Search the right edge without covering vanilla components like the trash can.
        for (int y = menu.yPositionOnScreen + TopOffset; y + Size <= menuBottom; y += Size + Gap)
        {
            var candidate = new Rectangle(menuRight + Gap, y, Size, Size);
            if (candidate.Right > screenWidth - Gap || candidate.Bottom > screenHeight - Gap || candidate.Top < Gap)
                continue;
            if (IntersectsVanillaButton(menu, candidate))
                continue;

            bounds = candidate;
            visible = true;
            return;
        }

        // At narrow UI widths, use free space below the panel instead of covering inventory slots.
        if (menuBottom + Gap + Size <= screenHeight - Gap)
        {
            var candidate = new Rectangle(Math.Clamp(menuRight - Size, Gap, Math.Max(Gap, screenWidth - Size - Gap)),
                menuBottom + Gap, Size, Size);
            if (!IntersectsVanillaButton(menu, candidate))
            {
                bounds = candidate;
                visible = true;
            }
        }
    }

    private static bool IntersectsVanillaButton(GameMenu menu, Rectangle candidate)
    {
        return menu.GetCurrentPage().allClickableComponents?.Any(c => c.bounds.Intersects(candidate)) == true
            || menu.upperRightCloseButton?.bounds.Intersects(candidate) == true;
    }

    public bool Contains(int x, int y) => visible && bounds.Contains(x, y);

    public void Draw(SpriteBatch b, int mouseX, int mouseY, bool enabled)
    {
        if (!visible)
            return;

        bool hovered = Contains(mouseX, mouseY);
        Color tint = !enabled ? Color.Gray : hovered ? Color.LightSkyBlue : Color.White;
        IClickableMenu.drawTextureBox(b, bounds.X, bounds.Y, bounds.Width, bounds.Height, tint);
        float scale = hovered && enabled ? 3.2f : 3f;
        b.Draw(Game1.mouseCursors, new Vector2(bounds.Center.X, bounds.Center.Y), ArrowSource,
            tint, 0f, new Vector2(ArrowSource.Width / 2f, ArrowSource.Height / 2f), scale, SpriteEffects.None, 1f);

        if (hovered)
            IClickableMenu.drawHoverText(b, "JojaDrop Upgrader", Game1.smallFont);
    }
}
