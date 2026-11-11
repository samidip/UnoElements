using SkiaSharp;

namespace UnoElements.Scene;

/// <summary>Element silhouettes (flame, orb, droplet, crystal) and their line icons.</summary>
internal static class Shapes
{
    static SKPoint U(SKRect r, float x, float y) => new(r.Left + x * r.Width, r.Top + y * r.Height);

    public static SKPath Create(ElementKind kind, SKRect r)
    {
        var p = new SKPath();
        switch (kind)
        {
            case ElementKind.Fire:
                p.MoveTo(U(r, .50f, .00f));
                p.CubicTo(U(r, .62f, .16f), U(r, .93f, .34f), U(r, .93f, .64f));
                p.CubicTo(U(r, .93f, .86f), U(r, .74f, 1f), U(r, .50f, 1f));
                p.CubicTo(U(r, .26f, 1f), U(r, .07f, .86f), U(r, .07f, .64f));
                p.CubicTo(U(r, .07f, .46f), U(r, .18f, .35f), U(r, .27f, .22f));
                p.CubicTo(U(r, .30f, .32f), U(r, .34f, .39f), U(r, .41f, .41f));
                p.CubicTo(U(r, .38f, .26f), U(r, .42f, .12f), U(r, .50f, .00f));
                p.Close();
                break;

            case ElementKind.Wind:
                var radius = r.Width / 2f;
                p.AddCircle(r.MidX, r.Bottom - radius, radius);
                break;

            case ElementKind.Water:
                AddDroplet(p, r);
                break;

            case ElementKind.Earth:
                AddRoundedPolygon(p,
                [
                    U(r, .5f, 0f), U(r, 1f, .22f), U(r, 1f, .78f),
                    U(r, .5f, 1f), U(r, 0f, .78f), U(r, 0f, .22f),
                ], r.Width * 0.09f);
                break;
        }

        return p;
    }

    public static void AddDroplet(SKPath p, SKRect r)
    {
        p.MoveTo(U(r, .5f, 0f));
        p.CubicTo(U(r, .56f, .14f), U(r, .90f, .44f), U(r, .90f, .66f));
        p.CubicTo(U(r, .90f, .88f), U(r, .72f, 1f), U(r, .5f, 1f));
        p.CubicTo(U(r, .28f, 1f), U(r, .10f, .88f), U(r, .10f, .66f));
        p.CubicTo(U(r, .10f, .44f), U(r, .44f, .14f), U(r, .5f, 0f));
        p.Close();
    }

    static void AddRoundedPolygon(SKPath p, SKPoint[] pts, float radius)
    {
        var n = pts.Length;
        var start = new SKPoint((pts[n - 1].X + pts[0].X) / 2, (pts[n - 1].Y + pts[0].Y) / 2);
        p.MoveTo(start);
        for (var i = 0; i < n; i++)
        {
            p.ArcTo(pts[i], pts[(i + 1) % n], radius);
        }

        p.Close();
    }

    /// <summary>Visual centre of mass of each silhouette — where effects orbit and emanate from.</summary>
    public static SKPoint Focus(ElementKind kind, SKRect r) => kind switch
    {
        ElementKind.Fire => U(r, .5f, .62f),
        ElementKind.Wind => new SKPoint(r.MidX, r.Bottom - r.Width / 2f),
        ElementKind.Water => U(r, .5f, .64f),
        _ => U(r, .5f, .5f),
    };

    public static float IconY(ElementKind kind) => kind switch
    {
        ElementKind.Fire => .52f,
        ElementKind.Wind => .50f,
        ElementKind.Water => .54f,
        _ => .40f,
    };

    public static void DrawIcon(SKCanvas c, ElementKind kind, SKPoint center, float size, SKPaint stroke)
    {
        var r = new SKRect(center.X - size / 2, center.Y - size / 2, center.X + size / 2, center.Y + size / 2);
        using var p = new SKPath();
        switch (kind)
        {
            case ElementKind.Fire:
                p.MoveTo(U(r, .50f, .04f));
                p.CubicTo(U(r, .62f, .22f), U(r, .78f, .34f), U(r, .78f, .54f));
                p.CubicTo(U(r, .78f, .70f), U(r, .66f, .80f), U(r, .50f, .80f));
                p.CubicTo(U(r, .34f, .80f), U(r, .22f, .70f), U(r, .22f, .54f));
                p.CubicTo(U(r, .22f, .42f), U(r, .30f, .34f), U(r, .37f, .27f));
                p.CubicTo(U(r, .37f, .38f), U(r, .42f, .45f), U(r, .48f, .47f));
                p.CubicTo(U(r, .44f, .34f), U(r, .46f, .18f), U(r, .50f, .04f));
                p.MoveTo(U(r, .50f, .56f));
                p.CubicTo(U(r, .58f, .63f), U(r, .60f, .68f), U(r, .58f, .73f));
                p.MoveTo(U(r, .18f, .97f));
                p.LineTo(U(r, .82f, .86f));
                p.MoveTo(U(r, .18f, .86f));
                p.LineTo(U(r, .82f, .97f));
                break;

            case ElementKind.Wind:
                p.MoveTo(U(r, .06f, .36f));
                p.LineTo(U(r, .60f, .36f));
                p.CubicTo(U(r, .74f, .36f), U(r, .80f, .28f), U(r, .78f, .19f));
                p.CubicTo(U(r, .76f, .10f), U(r, .64f, .07f), U(r, .58f, .14f));
                p.MoveTo(U(r, .06f, .52f));
                p.LineTo(U(r, .80f, .52f));
                p.CubicTo(U(r, .94f, .52f), U(r, .98f, .64f), U(r, .94f, .73f));
                p.CubicTo(U(r, .90f, .82f), U(r, .78f, .84f), U(r, .72f, .76f));
                p.MoveTo(U(r, .06f, .68f));
                p.LineTo(U(r, .44f, .68f));
                break;

            case ElementKind.Water:
                for (var k = 0; k < 3; k++)
                {
                    var y = .30f + k * .2f;
                    for (var i = 0; i <= 24; i++)
                    {
                        var x = .06f + i / 24f * .88f;
                        var pt = U(r, x, y + MathF.Sin(i / 24f * MathF.Tau * 1.5f) * .06f);
                        if (i == 0)
                        {
                            p.MoveTo(pt);
                        }
                        else
                        {
                            p.LineTo(pt);
                        }
                    }
                }

                break;

            case ElementKind.Earth:
                p.MoveTo(U(r, .04f, .84f));
                p.LineTo(U(r, .36f, .30f));
                p.LineTo(U(r, .56f, .62f));
                p.LineTo(U(r, .68f, .46f));
                p.LineTo(U(r, .96f, .84f));
                p.Close();
                p.MoveTo(U(r, .27f, .45f));
                p.LineTo(U(r, .36f, .52f));
                p.LineTo(U(r, .45f, .45f));
                p.AddCircle(r.Left + .76f * r.Width, r.Top + .18f * r.Height, .07f * r.Width);
                break;
        }

        c.DrawPath(p, stroke);
    }
}
