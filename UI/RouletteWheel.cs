using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace JojaDrop.UI;

/// <summary>
/// Draws a two-sector wheel (Joja blue = success share of chance, red = fail share) with pixel-art overlays.
/// The frame, sectors, and center stay fixed while the arrow spins above them.
/// </summary>
internal static class RouletteWheel
{
    private const float WheelRadius = 62f;
    private const float FrameInnerRadius = WheelRadius - 12f;
    private const float VisualSectorRadius = FrameInnerRadius + 2f;
    private const int AssetSize = 32;
    private const int WheelFrameScale = 5;
    private const int WheelCenterScale = 2;
    private const float WheelArrowLengthRatio = 1.4f;
    private const float WheelArrowOriginX = 152f;
    private const float WheelArrowOriginY = 0.5f;
    private const float WheelArrowSourceLength = 1090f;
    private const float WheelArrowRotationOffset = 0f;
    private const float FrameLayerDepth = 0.86f;
    private const float WheelLayerDepth = 0.85f;
    private const float CenterLayerDepth = 0.88f;
    private const float ArrowLayerDepth = 0.87f;
    public const float SpinDuration = 3.5f;
    private const float DecelerationPower = 2.5f;

    private static Texture2D? _pixel;

    private static Texture2D GetPixel(GraphicsDevice device)
    {
        if (_pixel is null || _pixel.IsDisposed)
        {
            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }
        return _pixel;
    }

    /// <summary>
    /// Picks a random stopping point inside the winning (Joja blue) or losing (red) sector.
    /// Returns an angle measured from 12 o'clock, clockwise — the same frame the wheel uses.
    /// </summary>
    public static float CalculateTargetAngle(double chance, bool success, Random? rng = null)
    {
        rng ??= Random.Shared;
        float winningAngle = Math.Clamp((float)chance * MathF.PI * 2f, 0f, MathF.PI * 2f);
        float redAngle = MathF.PI * 2f - winningAngle;

        // Uniform position inside the sector, kept 5% away from both sector edges.
        float t = 0.05f + (float)rng.NextDouble() * 0.9f;

        if (success)
            return winningAngle <= 0f ? 0f : winningAngle * t;
        return redAngle <= 0f ? winningAngle : winningAngle + redAngle * t;
    }

    public static void Draw(
        SpriteBatch b,
        Texture2D frame,
        Texture2D wheelCenter,
        Texture2D arrow,
        Vector2 center,
        double chance,
        float progress,
        bool isSpinning,
        float targetAngle,
        float scale = 1f)
    {
        float radius = VisualSectorRadius * scale;

        Texture2D pixel = GetPixel(b.GraphicsDevice);

        DrawWheel(b, center, radius, chance, pixel);
        b.Draw(frame, CenteredBounds(center, GetAssetSize(WheelFrameScale, scale)), null, Color.White,
            0f, Vector2.Zero, SpriteEffects.None, FrameLayerDepth);
        float arrowAngle = CalculatePointerAngle(progress, isSpinning, targetAngle) - MathF.PI / 2f + WheelArrowRotationOffset;
        Vector2 arrowOrigin = new(WheelArrowOriginX, arrow.Height * WheelArrowOriginY);
        float arrowScale = FrameInnerRadius * scale * WheelArrowLengthRatio / WheelArrowSourceLength;
        b.Draw(arrow, center, null, Color.White, arrowAngle, arrowOrigin, arrowScale,
            SpriteEffects.None, ArrowLayerDepth);
        b.Draw(wheelCenter, CenteredBounds(center, GetAssetSize(WheelCenterScale, scale)), null, Color.White,
            0f, Vector2.Zero, SpriteEffects.None, CenterLayerDepth);
    }

    private static int GetAssetSize(int assetScale, float uiScale) => Math.Max(1, (int)MathF.Round(AssetSize * assetScale * uiScale));

    private static Rectangle CenteredBounds(Vector2 center, int size) => new(
        (int)MathF.Round(center.X - size / 2f), (int)MathF.Round(center.Y - size / 2f), size, size);

    /// <summary>Returns the needle direction in radians (0 = up / 12 o'clock).</summary>
    public static float CalculatePointerAngle(float progress, bool isSpinning, float targetAngle)
    {
        if (!isSpinning)
            return 0f; // neutral: pointing up

        float normalized = Math.Clamp(progress / SpinDuration, 0f, 1f);
        float eased = 1f - MathF.Pow(1f - normalized, DecelerationPower);
        float spins = eased * 6f * MathF.PI * 2f;

        return spins + targetAngle;
    }

    private static void DrawWheel(SpriteBatch b, Vector2 center, float radius, double chance, Texture2D pixel)
    {
        float winningAngle = Math.Clamp((float)chance * MathF.PI * 2f, 0f, MathF.PI * 2f);
        float startAngle = -MathF.PI / 2f; // 12 o'clock

        // The winning (success) sector uses the official Joja blue from the game's palette.
        Color winning = SpriteText.color_JojaBlue;
        Color red = new Color(196, 54, 54);
        Color border = new Color(35, 25, 18);

        // Red first, winning sector on top (it may overlap red by a sub-pixel, covered by the border line).
        DrawWedge(b, center, radius, startAngle + winningAngle, MathF.PI * 2f - winningAngle, red, pixel, WheelLayerDepth);
        if (winningAngle > 0.0005f)
            DrawWedge(b, center, radius, startAngle, winningAngle, winning, pixel, WheelLayerDepth);

        // Radiating border lines at both sector edges.
        if (winningAngle > 0.0005f)
            DrawLine(b, center, PointAt(center, startAngle + winningAngle, radius), border, 2.5f, pixel, WheelLayerDepth + 0.001f);
        if (winningAngle < MathF.PI * 2f - 0.0005f)
            DrawLine(b, center, PointAt(center, startAngle, radius), border, 2.5f, pixel, WheelLayerDepth + 0.001f);

    }

    /// <summary>
    /// Fills a circular sector as a fan of thick radial lines.
    /// A radial line is a properly aligned rotated rectangle, so this covers the wedge exactly
    /// (a triangle-fan vertex in the centre cannot be drawn correctly with SpriteBatch).
    /// </summary>
    private static void DrawWedge(SpriteBatch b, Vector2 center, float radius, float startAngle, float sweep, Color color, Texture2D pixel, float depth)
    {
        if (sweep <= 0.0005f || radius <= 0f)
            return;
        if (sweep >= MathF.PI * 2f - 0.0005f)
        {
            DrawDisc(b, center, radius, color, pixel, depth);
            return;
        }

        // Step chosen so the line half-width stays under a pixel at the rim.
        int count = Math.Clamp((int)(sweep / 0.02f) + 1, 2, 400);
        float step = sweep / count;
        float thickness = radius * step * 1.4f + 0.4f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + step * (i + 0.5f);
            DrawLine(b, center, PointAt(center, angle, radius), color, thickness, pixel, depth);
        }
    }

    private static void DrawDisc(SpriteBatch b, Vector2 center, float radius, Color color, Texture2D pixel, float depth)
    {
        int count = 160;
        float step = MathF.PI * 2f / count;
        float thickness = radius * step * 1.4f + 0.4f;
        for (int i = 0; i < count; i++)
        {
            float angle = step * (i + 0.5f);
            DrawLine(b, center, PointAt(center, angle, radius), color, thickness, pixel, depth);
        }
    }

    private static Vector2 PointAt(Vector2 origin, float angle, float distance)
        => origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;

    /// <summary>Draws an axis-aligned rotated rectangle from <paramref name="start"/> toward <paramref name="end"/>.</summary>
    private static void DrawLine(SpriteBatch b, Vector2 start, Vector2 end, Color color, float thickness, Texture2D pixel, float depth)
    {
        Vector2 edge = end - start;
        float length = edge.Length();
        if (length < 0.5f || thickness <= 0f)
            return;

        float angle = MathF.Atan2(edge.Y, edge.X);
        // Centre the rectangle on the segment so thick lines have no half-width bias.
        Vector2 offset = new Vector2(MathF.Cos(angle + MathF.PI / 2f), MathF.Sin(angle + MathF.PI / 2f)) * (thickness / 2f);
        b.Draw(pixel, start - offset, null, color, angle, Vector2.Zero,
            new Vector2(length, thickness), SpriteEffects.None, depth);
    }

    /// <summary>
    /// Properly fills a triangle with horizontal 1px scanlines (SpriteBatch cannot draw
    /// arbitrary triangles — a rotated rectangle approximation shows up as a square).
    /// </summary>
    private static void FillTriangle(SpriteBatch b, Vector2 p1, Vector2 p2, Vector2 p3, Color color, Texture2D pixel, float depth)
    {
        // Sort by Y ascending.
        if (p1.Y > p2.Y) (p1, p2) = (p2, p1);
        if (p1.Y > p3.Y) (p1, p3) = (p3, p1);
        if (p2.Y > p3.Y) (p2, p3) = (p3, p2);

        float minY = p1.Y;
        float maxY = p3.Y;
        if (maxY - minY < 0.5f)
            return;

        int firstRow = (int)MathF.Ceiling(minY);
        int lastRow = (int)MathF.Floor(maxY);
        var intersections = new List<float>(4);

        for (int y = firstRow; y <= lastRow; y++)
        {
            float sample = y + 0.5f;
            if (sample < minY || sample > maxY)
                continue;

            intersections.Clear();
            CollectIntersection(p1, p2, sample, intersections);
            CollectIntersection(p2, p3, sample, intersections);
            CollectIntersection(p3, p1, sample, intersections);
            if (intersections.Count < 2)
                continue;

            intersections.Sort();
            for (int i = 0; i + 1 < intersections.Count; i += 2)
            {
                float x1 = intersections[i];
                float x2 = intersections[i + 1];
                if (x2 - x1 < 0.5f)
                    continue;
                b.Draw(pixel, new Rectangle((int)MathF.Round(x1), y, Math.Max(1, (int)MathF.Round(x2 - x1)), 1),
                    null, color, 0f, Vector2.Zero, SpriteEffects.None, depth);
            }
        }
    }

    private static void CollectIntersection(Vector2 a, Vector2 b, float y, List<float> output)
    {
        if ((a.Y <= y && b.Y >= y) || (b.Y <= y && a.Y >= y))
        {
            if (MathF.Abs(a.Y - b.Y) < 0.0001f)
                return;
            float t = (y - a.Y) / (b.Y - a.Y);
            if (t is >= 0f and <= 1f)
                output.Add(a.X + (b.X - a.X) * t);
        }
    }
}
