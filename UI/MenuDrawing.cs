using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace JojaDrop.UI;

internal static class MenuDrawing
{
    public const int ScreenMargin = 32;
    public const int SlotSize = 64;
    private const float SlotLayerDepth = 0.9f;
    private const float HoverLayerDepth = 0.95f;
    private const int InventorySlotIndex = 10;
    private const int PanelBorderThickness = 64;
    private const int BackgroundBandHeight = 4;
    private static readonly Color BackgroundTop = new(246, 224, 177);
    private static readonly Color BackgroundMiddle = new(240, 215, 165);
    private static readonly Color BackgroundBottom = new(234, 206, 153);

    public static void Panel(SpriteBatch b, IClickableMenu menu)
    {
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.5f);
        IClickableMenu.drawTextureBox(b, menu.xPositionOnScreen, menu.yPositionOnScreen, menu.width, menu.height, Color.White);

        Rectangle background = new(menu.xPositionOnScreen + PanelBorderThickness,
            menu.yPositionOnScreen + PanelBorderThickness,
            menu.width - PanelBorderThickness * 2,
            menu.height - PanelBorderThickness * 2);
        if (background.Width <= 0 || background.Height <= 0)
            return;

        for (int y = 0; y < background.Height; y += BackgroundBandHeight)
        {
            int bandHeight = Math.Min(BackgroundBandHeight, background.Height - y);
            float position = (y + bandHeight / 2f) / background.Height;
            Color shade = position < 0.5f
                ? Color.Lerp(BackgroundTop, BackgroundMiddle, position * 2f)
                : Color.Lerp(BackgroundMiddle, BackgroundBottom, (position - 0.5f) * 2f);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(background.X, background.Y + y, background.Width, bandHeight), shade);
        }
    }

    public static void CenteredText(SpriteBatch b, string text, int centerX, int y, Color? color = null, float scale = 1f)
    {
        Vector2 size = Game1.smallFont.MeasureString(text) * scale;
        b.DrawString(Game1.smallFont, text, new Vector2(centerX - size.X / 2f, y), color ?? Game1.textColor,
            0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
    }

    public static void Slot(SpriteBatch b, Rectangle bounds, Item? item, bool hovered, bool enabled = true)
    {
        Rectangle source = Game1.getSourceRectForStandardTileSheet(Game1.menuTexture, InventorySlotIndex);
        b.Draw(Game1.menuTexture, bounds, source, enabled ? Color.White : Color.White * 0.5f,
            0f, Vector2.Zero, SpriteEffects.None, SlotLayerDepth);
        if (hovered)
            b.Draw(Game1.fadeToBlackRect, bounds, null, Color.Goldenrod * 0.2f,
                0f, Vector2.Zero, SpriteEffects.None, HoverLayerDepth);
        item?.drawInMenu(b, new Vector2(bounds.X, bounds.Y), bounds.Width / (float)SlotSize,
            enabled ? 1f : 0.45f, 1f, StackDrawType.Hide);
    }

    public static void TextButton(SpriteBatch b, ClickableComponent button, string text, bool hovered, bool enabled, float textScale = 1f)
    {
        Color tint = !enabled ? Color.Gray : hovered ? Color.LightSkyBlue : Color.White;
        IClickableMenu.drawTextureBox(b, button.bounds.X, button.bounds.Y, button.bounds.Width, button.bounds.Height, tint);
        CenteredText(b, text, button.bounds.Center.X,
            button.bounds.Center.Y - (int)(Game1.smallFont.MeasureString(text).Y * textScale) / 2,
            enabled ? Game1.textColor : Color.DimGray, textScale);
    }

    public static string FitText(string text, int maxWidth)
    {
        if (Game1.smallFont.MeasureString(text).X <= maxWidth)
            return text;

        while (text.Length > 0 && Game1.smallFont.MeasureString(text + "...").X > maxWidth)
            text = text[..^1];
        return text + "...";
    }
}
