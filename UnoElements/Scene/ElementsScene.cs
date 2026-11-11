using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace UnoElements.Scene;

/// <summary>
/// Full-window Skia stage: backdrop, glossy floor with reflections, element silhouettes and their effects.
/// Card positions come from the XAML layout via <see cref="SetSlot"/>, so XAML owns layout and input
/// while Skia owns the pixels.
/// </summary>
public sealed class ElementsScene : SKCanvasElement
{
    readonly ElementState[] _elements =
    [
        new(ElementKind.Fire, Palettes.Fire, new FireEffect()),
        new(ElementKind.Wind, Palettes.Wind, new WindEffect()),
        new(ElementKind.Water, Palettes.Water, new WaterEffect()),
        new(ElementKind.Earth, Palettes.Earth, new EarthEffect()),
    ];

    readonly AmbientMotes _motes = new();
    readonly SKPaint _paint = new() { IsAntialias = true };
    SKShader? _grain;
    float _time;
    float _floorY;

    public ElementsScene()
    {
        IsHitTestVisible = false;
    }

    public int ActiveIndex { get; set; } = -1;

    public void SetSlot(int index, Rect r)
        => _elements[index].SetRect(new SKRect((float)r.X, (float)r.Y, (float)r.Right, (float)r.Bottom));

    public void SetHovered(int index, bool value) => _elements[index].IsHovered = value;

    public void Ignite(int index) => _elements[index].Pulse = 1f;

    /// <summary>How "awake" an element is (0..1), for syncing XAML text emphasis.</summary>
    public float GetEmphasis(int index) => Math.Max(_elements[index].ActiveT, _elements[index].HoverT * 0.6f);

    public void Step(float dt)
    {
        _time += dt;

        var floor = 0f;
        foreach (var e in _elements)
        {
            floor = Math.Max(floor, e.Rect.Bottom + 34 * e.Scale);
        }

        _floorY = floor;

        for (var i = 0; i < _elements.Length; i++)
        {
            var e = _elements[i];
            e.IsActive = i == ActiveIndex;
            e.ActiveT = Approach(e.ActiveT, e.IsActive ? 1f : 0f, e.IsActive ? 2.6f : 3.4f, dt);
            e.HoverT = Approach(e.HoverT, e.IsHovered ? 1f : 0f, 9f, dt);
            e.Pulse = Math.Max(0, e.Pulse - dt * 1.8f);
            if (e.HasLayout)
            {
                e.Effect.Update(e, dt, _time, _floorY);
            }
        }

        _motes.Update(dt, _time);
    }

    static float Approach(float value, float target, float rate, float dt)
        => value + (target - value) * (1f - MathF.Exp(-rate * dt));

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        var w = (float)area.Width;
        var h = (float)area.Height;

        DrawBackdrop(canvas, w, h);

        if (_elements[0].HasLayout)
        {
            DrawReflection(canvas, w, h);
            DrawFloor(canvas, w, h);
            DrawStage(canvas);
        }

        _motes.Draw(canvas, w, h, CurrentTint());
        DrawVignette(canvas, w, h);
        DrawGrain(canvas, w, h);
    }

    SKColor CurrentTint()
    {
        var tint = SKColors.White;
        foreach (var e in _elements)
        {
            tint = Col.Lerp(tint, e.P.Bright, e.ActiveT);
        }

        return tint;
    }

    void DrawStage(SKCanvas c)
    {
        foreach (var e in _elements)
        {
            e.Effect.DrawBack(c, e, _time);
        }

        foreach (var e in _elements)
        {
            CardRenderer.Draw(c, e, _time);
        }

        foreach (var e in _elements)
        {
            e.Effect.DrawFront(c, e, _time);
        }
    }

    void DrawBackdrop(SKCanvas c, float w, float h)
    {
        var horizon = _floorY > 0 ? _floorY / h : 0.75f;
        _paint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0), new SKPoint(0, h),
            [new SKColor(12, 12, 16), new SKColor(8, 8, 11), new SKColor(5, 5, 7), new SKColor(3, 3, 4)],
            [0f, horizon * 0.7f, horizon, 1f], SKShaderTileMode.Clamp);
        c.DrawRect(0, 0, w, h, _paint);

        foreach (var e in _elements)
        {
            if (!e.HasLayout)
            {
                continue;
            }

            // Every element breathes a faint halo of its color; the active one floods the room.
            var idle = 0.10f + 0.08f * e.HoverT;
            _paint.Shader = SKShader.CreateRadialGradient(
                e.Focus, e.Rect.Width * 1.3f,
                [e.P.Deep.A(idle), e.P.Deep.A(0)], SKShaderTileMode.Clamp);
            c.DrawRect(0, 0, w, h, _paint);

            if (e.ActiveT > 0.005f)
            {
                _paint.Shader = SKShader.CreateRadialGradient(
                    e.Focus, Math.Max(w, h) * 0.8f,
                    [e.P.Deep.A(0.95f * e.ActiveT), e.P.Deep.A(0.35f * e.ActiveT), e.P.Deep.A(0)],
                    [0f, 0.45f, 1f], SKShaderTileMode.Clamp);
                c.DrawRect(0, 0, w, h, _paint);

                _paint.Shader = SKShader.CreateRadialGradient(
                    e.Focus, e.Rect.Width * 1.6f,
                    [e.P.Mid.A(0.22f * e.ActiveT), e.P.Mid.A(0)], SKShaderTileMode.Clamp);
                c.DrawRect(0, 0, w, h, _paint);
            }
        }

        _paint.Shader = null;
    }

    void DrawReflection(SKCanvas c, float w, float h)
    {
        var s = _elements[0].Scale;
        var bounds = new SKRect(0, _floorY, w, h);

        c.Save();
        c.ClipRect(bounds);
        c.SaveLayer(bounds, null);

        using (var blur = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(2.5f * s, 2.5f * s), Color = SKColors.White.A(0.42f) })
        {
            c.SaveLayer(bounds, blur);
            c.Translate(0, 2 * _floorY);
            c.Scale(1, -1);
            DrawStage(c);
            c.Restore();
        }

        using (var mask = new SKPaint
        {
            BlendMode = SKBlendMode.DstIn,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, _floorY), new SKPoint(0, _floorY + 220 * s),
                [SKColors.White, SKColors.White.A(0.35f), SKColors.White.A(0)],
                [0f, 0.35f, 1f], SKShaderTileMode.Clamp),
        })
        {
            c.DrawRect(bounds, mask);
        }

        c.Restore();
        c.Restore();
    }

    void DrawFloor(SKCanvas c, float w, float h)
    {
        // Horizon sheen
        _paint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0), new SKPoint(w, 0),
            [SKColors.White.A(0), SKColors.White.A(0.07f), SKColors.White.A(0)], SKShaderTileMode.Clamp);
        c.DrawRect(0, _floorY - 0.5f, w, 1f, _paint);

        foreach (var e in _elements)
        {
            // Pool of light beneath each element
            var strength = 0.16f + 0.5f * e.ActiveT + 0.14f * e.HoverT;
            var rx = e.Rect.Width * (0.75f + 0.5f * e.ActiveT);
            c.Save();
            c.Translate(e.Rect.MidX, _floorY);
            c.Scale(1f, 0.12f);
            _paint.Shader = SKShader.CreateRadialGradient(
                SKPoint.Empty, rx,
                [e.P.Bright.A(strength), e.P.Mid.A(strength * 0.4f), e.P.Mid.A(0)],
                [0f, 0.4f, 1f], SKShaderTileMode.Clamp);
            c.DrawCircle(0, 0, rx, _paint);
            c.Restore();

            e.Effect.DrawFloor(c, e, _floorY, _time);
        }

        _paint.Shader = null;
    }

    void DrawVignette(SKCanvas c, float w, float h)
    {
        _paint.Shader = SKShader.CreateRadialGradient(
            new SKPoint(w / 2, h * 0.45f), Math.Max(w, h) * 0.78f,
            [SKColors.Black.A(0), SKColors.Black.A(0), SKColors.Black.A(0.78f)],
            [0f, 0.45f, 1f], SKShaderTileMode.Clamp);
        c.DrawRect(0, 0, w, h, _paint);
        _paint.Shader = null;
    }

    void DrawGrain(SKCanvas c, float w, float h)
    {
        if (_grain is null)
        {
            const int size = 192;
            using var bmp = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            var rnd = new Random(3);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var v = (byte)rnd.Next(256);
                    bmp.SetPixel(x, y, new SKColor(v, v, v, 255));
                }
            }

            using var img = SKImage.FromBitmap(bmp);
            _grain = img.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
        }

        using var p = new SKPaint { Shader = _grain, Color = SKColors.White.A(0.035f), BlendMode = SKBlendMode.Screen };
        c.DrawRect(0, 0, w, h, p);
    }
}

/// <summary>Faint dust drifting through the whole room, tinted by the active element.</summary>
internal sealed class AmbientMotes
{
    readonly (float X, float Y, float Size, float Seed)[] _motes = new (float, float, float, float)[80];
    readonly GlowBatch _batch = new();

    public AmbientMotes()
    {
        for (var i = 0; i < _motes.Length; i++)
        {
            _motes[i] = (Rand.Next(), Rand.Next(), Rand.Range(0.8f, 2.2f), Rand.Range(0, 100));
        }
    }

    public void Update(float dt, float time)
    {
        for (var i = 0; i < _motes.Length; i++)
        {
            ref var m = ref _motes[i];
            m.Y -= dt * 0.006f * m.Size;
            m.X += Noise.Get(m.Seed, time * 0.1f) * dt * 0.004f;
            if (m.Y < -0.02f)
            {
                m.Y = 1.02f;
                m.X = Rand.Next();
            }
        }
    }

    public void Draw(SKCanvas c, float w, float h, SKColor tint)
    {
        foreach (var m in _motes)
        {
            var twinkle = 0.5f + 0.5f * MathF.Sin(m.Seed * 7 + m.Y * 40);
            _batch.Add(m.X * w, m.Y * h, m.Size * 3f, tint.A(0.10f + 0.16f * twinkle));
        }

        _batch.Flush(c);
    }
}
