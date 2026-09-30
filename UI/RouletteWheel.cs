using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace JojaDrop.UI;

/// <summary>
/// Draws a two-sector wheel (green = success share of chance, red = fail share) with a needle.
/// The wheel itself never rotates; only the needle spins.
/// </summary>
internal static class RouletteWheel
{
    private const float WheelRadius = 60f;
    private const float ArrowLength = 65f;
    private const float ArrowWidth = 3f;
    private const float HubRadius = 14f;
    private const float WheelLayerDepth = 0.85f;
    private const float PointerLayerDepth = 0.87f;
    private const float HubLayerDepth = 0.88f;
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

    public static void Draw(
        SpriteBatch b,
        Vector2 center,
        double chance,
        float progress,
        bool isSpinning,
        bool success,
        float scale = 1f)
    {
        float radius = WheelRadius * scale;
        float arrowLen = ArrowLength * scale;
        float arrowW = ArrowWidth * scale;
        float hubRad = HubRadius * scale;

        Texture2D pixel = GetPixel(b.GraphicsDevice);

        // CalculatePointerAngle is measured from 12 o'clock; DrawPointer expects a standard
        // screen angle (0 = 3 o'clock), so shift by -90°.
        float pointerAngle = CalculatePointerAngle(chance, progress, isSpinning, success) - MathF.PI / 2f;

        DrawWheel(b, center, radius, chance, pixel);
        DrawPointer(b, center, arrowLen, arrowW, pointerAngle, pixel);
        DrawHub(b, center, hubRad, pixel);
    }

    /// <summary>Returns the needle direction in radians (0 = up / 12 o'clock).</summary>
    private static float CalculatePointerAngle(double chance, float progress, bool isSpinning, bool success)
    {
        float greenAngle = (float)(chance * MathF.PI * 2);
        // Center of the green sector, measured from top, clockwise.
        float targetAngle = success ? greenAngle / 2f : greenAngle + (MathF.PI * 2f - greenAngle) / 2f;

        if (!isSpinning)
            return 0f; // neutral: pointing up

        float normalized = Math.Clamp(progress / SpinDuration, 0f, 1f);
        float eased = 1f - MathF.Pow(1f - normalized, DecelerationPower);
        float spins = eased * 6f * MathF.PI * 2f;

        return spins + targetAngle;
    }

    private static void DrawWheel(SpriteBatch b, Vector2 center, float radius, double chance, Texture2D pixel)
    {
        float greenAngle = Math.Clamp((float)chance * MathF.PI * 2f, 0f, MathF.PI * 2f);
        float startAngle = -MathF.PI / 2f; // 12 o'clock

        Color green = new Color(60, 180, 75);
        Color red = new Color(196, 54, 54);
        Color border = new Color(35, 25, 18);

        // Red first, green on top (green may overlap red by a sub-pixel, covered by the border line).
        DrawWedge(b, center, radius, startAngle + greenAngle, MathF.PI * 2f - greenAngle, red, pixel, WheelLayerDepth);
        if (greenAngle > 0.0005f)
            DrawWedge(b, center, radius, startAngle, greenAngle, green, pixel, WheelLayerDepth);

        // Radiating border lines at both sector edges.
        if (greenAngle > 0.0005f)
            DrawLine(b, center, PointAt(center, startAngle + greenAngle, radius), border, 2.5f, pixel, WheelLayerDepth + 0.001f);
        if (greenAngle < MathF.PI * 2f - 0.0005f)
            DrawLine(b, center, PointAt(center, startAngle, radius), border, 2.5f, pixel, WheelLayerDepth + 0.001f);

        // Outer ring.
        DrawRing(b, center, radius, 3f, border, pixel, WheelLayerDepth + 0.002f);
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

    private static void DrawRing(SpriteBatch b, Vector2 center, float radius, float thickness, Color color, Texture2D pixel, float depth)
    {
        int count = 96;
        float step = MathF.PI * 2f / count;
        Vector2 previous = PointAt(center, 0f, radius);
        for (int i = 1; i <= count; i++)
        {
            Vector2 current = PointAt(center, step * i, radius);
            DrawLine(b, previous, current, color, thickness, pixel, depth);
            previous = current;
        }
    }

    private static void DrawPointer(SpriteBatch b, Vector2 center, float length, float width, float angle, Texture2D pixel)
    {
        Color needle = new Color(235, 225, 185);
        Color shade = new Color(30, 22, 14);

        Vector2 dir = PointAt(Vector2.Zero, angle, 1f);          // unit vector
        Vector2 perp = new Vector2(-dir.Y, dir.X);
        float shaftLength = length * 0.86f;
        Vector2 shaftEnd = center + dir * shaftLength;
        Vector2 tip = center + dir * length;

        Vector2 offset = perp * (width * 0.5f);

        // Shadow (drawn first, sits under the needle).
        Vector2 shadow = new Vector2(1.5f, 1.5f);
        DrawLine(b, center + shadow, shaftEnd + shadow, shade * 0.45f, width, pixel, PointerLayerDepth - 0.001f);
        FillTriangle(b, tip + shadow, shaftEnd + offset * 2.2f + shadow, shaftEnd - offset * 2.2f + shadow,
            shade * 0.45f, pixel, PointerLayerDepth - 0.001f);

        // Shaft: thin rectangle from the centre to 86% of the needle.
        DrawLine(b, center, shaftEnd, needle, width, pixel, PointerLayerDepth);
        // Tip: small triangle ending in a point.
        FillTriangle(b, tip, shaftEnd + offset * 2.2f, shaftEnd - offset * 2.2f, needle, pixel, PointerLayerDepth);
    }

    private static void DrawHub(SpriteBatch b, Vector2 center, float radius, Texture2D pixel)
    {
        Color hub = new Color(86, 64, 42);
        Color hubEdge = new Color(38, 28, 18);

        DrawDisc(b, center, radius, hub, pixel, HubLayerDepth);
        DrawRing(b, center, radius, 2.5f, hubEdge, pixel, HubLayerDepth + 0.001f);
        DrawRing(b, center, radius * 0.4f, 2f, hubEdge, pixel, HubLayerDepth + 0.002f);
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