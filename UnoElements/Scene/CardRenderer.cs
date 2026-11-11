using SkiaSharp;

namespace UnoElements.Scene;

/// <summary>Draws an element's glass silhouette: dark idle glass that floods with living color when active.</summary>
internal static class CardRenderer
{
    static readonly GlowBatch Blobs = new();

    static readonly SKPaint Fill = new() { IsAntialias = true };
    static readonly SKPaint Stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    static readonly SKPaint Glow = new() { IsAntialias = true, BlendMode = SKBlendMode.Plus };

    public static void Draw(SKCanvas c, ElementState e, float time)
    {
        var s = e.Scale;
        var b = e.Bounds;
        var a = e.ActiveT;
        var h = e.HoverT;
        var p = e.P;

        // Outer glow
        var glow = 0.6f * a + 0.22f * h + 0.5f * e.Pulse;
        if (glow > 0.01f)
        {
            using var blur = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, 26 * s);
            Glow.MaskFilter = blur;
            Glow.Color = p.Bright.A(Math.Min(1f, glow));
            c.DrawPath(e.Path, Glow);
            Glow.MaskFilter = null;
        }

        c.Save();
        c.ClipPath(e.Path, antialias: true);

        // Idle smoked glass: near-black falling into a deep tinted base
        Fill.Shader = SKShader.CreateLinearGradient(
            new SKPoint(b.MidX, b.Top), new SKPoint(b.MidX, b.Bottom),
            [new SKColor(13, 13, 16), new SKColor(10, 10, 13), Col.Scale(p.Deep, 0.95f)],
            [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
        c.DrawRect(b, Fill);

        Fill.Shader = SKShader.CreateRadialGradient(
            new SKPoint(b.MidX, b.Bottom), b.Width * 0.75f,
            [p.Mid.A(0.30f + 0.15f * h), p.Mid.A(0)], SKShaderTileMode.Clamp);
        c.DrawRect(b, Fill);

        // Active: liquid gradient with drifting blobs of light
        if (a > 0.002f)
        {
            using var layer = new SKPaint { Color = SKColors.White.A(a) };
            c.SaveLayer(b, layer);

            Fill.Shader = SKShader.CreateLinearGradient(
                new SKPoint(b.Left, b.Top), new SKPoint(b.Right, b.Bottom),
                [p.Bright, p.Mid, p.Deep],
                [0f, 0.55f, 1f], SKShaderTileMode.Clamp);
            c.DrawRect(b, Fill);

            var speed = e.Kind == ElementKind.Fire ? 1.6f : e.Kind == ElementKind.Earth ? 0.45f : 0.9f;
            ReadOnlySpan<SKColor> colors = [p.Core.A(0.75f), p.Accent.A(0.65f), p.Mid.A(0.8f), p.Bright.A(0.7f)];
            for (var i = 0; i < 4; i++)
            {
                var t = time * speed * (0.6f + i * 0.17f) + i * 2.1f;
                var x = b.MidX + MathF.Cos(t) * b.Width * 0.32f;
                var y = b.MidY + MathF.Sin(t * 1.3f + i) * b.Height * 0.3f;
                Blobs.Add(x, y, b.Width * (0.5f + 0.08f * i), colors[i]);
            }

            Blobs.Flush(c, SKBlendMode.SrcOver);
            c.Restore();
        }

        DrawInnerDetail(c, e, time);

        // Glassy top highlight
        Fill.Shader = SKShader.CreateLinearGradient(
            new SKPoint(b.MidX, b.Top), new SKPoint(b.MidX, b.Top + b.Height * 0.45f),
            [SKColors.White.A(0.10f + 0.12f * a), SKColors.White.A(0)], SKShaderTileMode.Clamp);
        c.DrawRect(b, Fill);
        Fill.Shader = null;

        c.Restore();

        // Rim light
        Stroke.StrokeWidth = 1.3f * s;
        Stroke.Shader = SKShader.CreateLinearGradient(
            new SKPoint(b.MidX, b.Top), new SKPoint(b.MidX, b.Bottom),
            [SKColors.White.A(0.22f + 0.45f * a + 0.25f * h), SKColors.White.A(0.05f + 0.2f * a), p.Bright.A(0.12f + 0.5f * a)],
            [0f, 0.6f, 1f], SKShaderTileMode.Clamp);
        c.DrawPath(e.Path, Stroke);
        Stroke.Shader = null;

        // Icon
        var iconCenter = new SKPoint(e.Rect.MidX, e.Rect.Top + e.Rect.Height * Shapes.IconY(e.Kind));
        var iconSize = 64 * s;
        if (a > 0.01f)
        {
            using var blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6 * s);
            Stroke.MaskFilter = blur;
            Stroke.StrokeWidth = 4 * s;
            Stroke.Color = p.Core.A(0.6f * a);
            Shapes.DrawIcon(c, e.Kind, iconCenter, iconSize, Stroke);
            Stroke.MaskFilter = null;
        }

        Stroke.StrokeWidth = 2.4f * s;
        Stroke.Color = SKColors.White.A(0.78f + 0.2f * Math.Max(a, h));
        Shapes.DrawIcon(c, e.Kind, iconCenter, iconSize, Stroke);
    }

    static void DrawInnerDetail(SKCanvas c, ElementState e, float time)
    {
        var s = e.Scale;
        var b = e.Bounds;
        Stroke.StrokeWidth = 1f * s;

        switch (e.Kind)
        {
            case ElementKind.Earth:
                // Crystal facets
                Stroke.Color = SKColors.White.A(0.05f + 0.10f * e.ActiveT);
                var hub = new SKPoint(b.MidX, b.Top + b.Height * 0.47f);
                ReadOnlySpan<SKPoint> v =
                [
                    new(b.MidX, b.Top), new(b.Right, b.Top + b.Height * .22f), new(b.Right, b.Top + b.Height * .78f),
                    new(b.MidX, b.Bottom), new(b.Left, b.Top + b.Height * .78f), new(b.Left, b.Top + b.Height * .22f),
                ];
                foreach (var pt in v)
                {
                    c.DrawLine(hub, pt, Stroke);
                }

                break;

            case ElementKind.Wind:
                // Slowly turning inner currents
                Stroke.Color = SKColors.White.A(0.05f + 0.10f * e.ActiveT);
                var spin = time * (0.15f + 0.6f * e.ActiveT);
                for (var i = 0; i < 3; i++)
                {
                    var rad = b.Width * (0.22f + i * 0.1f);
                    var rect = new SKRect(e.Focus.X - rad, e.Focus.Y - rad, e.Focus.X + rad, e.Focus.Y + rad);
                    var start = (spin * (i % 2 == 0 ? 1 : -1.3f) + i) * 57.3f;
                    using var arc = new SKPath();
                    arc.AddArc(rect, start, 200);
                    c.DrawPath(arc, Stroke);
                }

                break;

            case ElementKind.Water:
                // Gentle surface line that sloshes inside the drop
                Stroke.Color = SKColors.White.A(0.06f + 0.14f * e.ActiveT);
                using (var wave = new SKPath())
                {
                    var y0 = b.Top + b.Height * 0.74f;
                    for (var i = 0; i <= 30; i++)
                    {
                        var x = b.Left + b.Width * i / 30f;
                        var y = y0 + MathF.Sin(i / 30f * MathF.Tau + time * 1.6f) * 5 * s;
                        if (i == 0)
                        {
                            wave.MoveTo(x, y);
                        }
                        else
                        {
                            wave.LineTo(x, y);
                        }
                    }

                    c.DrawPath(wave, Stroke);
                }

                break;
        }
    }
}
