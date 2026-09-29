using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace JojaDrop.UI;

internal sealed class UpgradeButton
{
    private const int Size = 64;
    private const int HoverSize = 68;
    private const int Gap = 16;
    private const int TopOffset = 96;
    private readonly Texture2D texture;
    private Rectangle bounds;
    private bool visible;

    public UpgradeButton(Texture2D texture)
    {
        this.texture = texture;
    }

    public void UpdateLayout(GameMenu menu)
    {
        int screenWidth = Game1.uiViewport.Width;
        int screenHeight = Game1.uiViewport.Height;
        int menuRight = menu.xPositionOnScreen + menu.width;
        int menuBottom = menu.yPositionOnScreen + menu.height;
        visible = false;

        if (TryLayoutInVanillaColumn(menu, screenWidth, screenHeight))
            return;

        LayoutFallback(menu, screenWidth, screenHeight, menuRight, menuBottom);
    }

    private bool TryLayoutInVanillaColumn(GameMenu menu, int screenWidth, int screenHeight)
    {
        if (menu.GetCurrentPage() is not InventoryPage page)
            return false;

        var controls = new[] { page.organizeButton, page.trashCan }
            .Where(control => control is not null && control.bounds.Width > 0 && control.bounds.Height > 0)
            .OrderBy(control => control.bounds.Top)
            .ToList();
        if (controls.Count == 0)
            return false;

        ClickableTextureComponent anchor = controls[0];
        int columnCenterX = anchor.bounds.Center.X;
        List<ClickableTextureComponent> columnControls = controls
            .Where(control => Math.Abs(control.bounds.Center.X - columnCenterX) <= Size / 2)
            .ToList();
        if (columnControls.Count == 0)
            return false;

        int columnGap = GetColumnGap(columnControls);
        int x = columnCenterX - Size / 2;

        // Prefer the slot immediately above the first vanilla control, then the
        // slots neighboring the other controls in the same column.
        IEnumerable<int> candidateYs = columnControls
            .Select(control => control.bounds.Top - columnGap - Size)
            .Concat(columnControls.Select(control => control.bounds.Bottom + columnGap))
            .Distinct();

        foreach (int y in candidateYs)
        {
            var candidate = new Rectangle(x, y, Size, Size);
            if (!FitsViewport(candidate, screenWidth, screenHeight) || IntersectsVanillaButton(menu, candidate))
                continue;

            bounds = candidate;
            visible = true;
            return true;
        }

        return false;
    }

    private static int GetColumnGap(IReadOnlyList<ClickableTextureComponent> controls)
    {
        for (int i = 1; i < controls.Count; i++)
        {
            int gap = controls[i].bounds.Top - controls[i - 1].bounds.Bottom;
            if (gap is >= 4 and <= 32)
                return gap;
        }

        return Gap;
    }

    private void LayoutFallback(GameMenu menu, int screenWidth, int screenHeight, int menuRight, int menuBottom)
    {
        // Search the right edge without covering vanilla components like the trash can.
        for (int y = menu.yPositionOnScreen + TopOffset; y + Size <= menuBottom; y += Size + Gap)
        {
            var candidate = new Rectangle(menuRight + Gap, y, Size, Size);
            if (!FitsViewport(candidate, screenWidth, screenHeight))
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

    private static bool FitsViewport(Rectangle candidate, int screenWidth, int screenHeight)
    {
        return candidate.Left >= Gap
            && candidate.Top >= Gap
            && candidate.Right <= screenWidth - Gap
            && candidate.Bottom <= screenHeight - Gap;
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
        int drawSize = hovered && enabled ? HoverSize : Size;
        var destination = new Rectangle(
            bounds.Center.X - drawSize / 2,
            bounds.Center.Y - drawSize / 2,
            drawSize,
            drawSize);
        b.Draw(texture, destination, null, enabled ? Color.White : Color.Gray,
            0f, Vector2.Zero, SpriteEffects.None, 1f);

        if (hovered)
            IClickableMenu.drawHoverText(b, "JojaDrop Upgrader", Game1.smallFont);
    }
}
