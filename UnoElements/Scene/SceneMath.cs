using SkiaSharp;

namespace UnoElements.Scene;

public enum ElementKind
{
    Fire,
    Wind,
    Water,
    Earth,
}

/// <summary>Five-stop palette: hot core → bright → mid → deep shadow, plus an accent.</summary>
public sealed record Palette(SKColor Core, SKColor Bright, SKColor Mid, SKColor Deep, SKColor Accent);

internal static class Palettes
{
    public static readonly Palette Fire = new(new(255, 240, 196), new(255, 150, 58), new(236, 62, 96), new(112, 16, 46), new(255, 204, 80));
    public static readonly Palette Wind = new(new(234, 255, 246), new(132, 246, 204), new(32, 186, 148), new(6, 66, 56), new(196, 236, 120));
    public static readonly Palette Water = new(new(226, 250, 255), new(88, 208, 255), new(40, 108, 255), new(10, 28, 104), new(120, 255, 236));
    public static readonly Palette Earth = new(new(255, 238, 198), new(240, 176, 86), new(168, 106, 46), new(64, 38, 18), new(150, 176, 84));
}

internal static class Col
{
    public static SKColor A(this SKColor c, float alpha)
        => c.WithAlpha((byte)Math.Clamp(alpha * 255f, 0f, 255f));

    public static SKColor Lerp(SKColor a, SKColor b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue + (b.Blue - a.Blue) * t),
            (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
    }

    public static SKColor Scale(SKColor c, float k)
        => new((byte)Math.Clamp(c.Red * k, 0, 255), (byte)Math.Clamp(c.Green * k, 0, 255), (byte)Math.Clamp(c.Blue * k, 0, 255), c.Alpha);

    /// <summary>Samples a multi-stop gradient at t.</summary>
    public static SKColor Ramp(float t, ReadOnlySpan<SKColor> colors, ReadOnlySpan<float> stops)
    {
        if (t <= stops[0])
        {
            return colors[0];
        }

        for (var i = 1; i < stops.Length; i++)
        {
            if (t <= stops[i])
            {
                return Lerp(colors[i - 1], colors[i], (t - stops[i - 1]) / (stops[i] - stops[i - 1]));
            }
        }

        return colors[^1];
    }
}

internal static class Rand
{
    static readonly Random R = new(7);

    public static float Next() => (float)R.NextDouble();

    public static float Range(float min, float max) => min + (max - min) * Next();

    public static float Gauss()
    {
        var u1 = 1f - Next();
        var u2 = Next();
        return MathF.Sqrt(-2f * MathF.Log(u1)) * MathF.Cos(MathF.Tau * u2);
    }
}

/// <summary>Smooth 2D value noise in [-1, 1].</summary>
internal static class Noise
{
    static float Hash(int x, int y)
    {
        unchecked
        {
            var h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 32767.5f - 1f;
        }
    }

    public static float Get(float x, float y)
    {
        var xi = (int)MathF.Floor(x);
        var yi = (int)MathF.Floor(y);
        var xf = x - xi;
        var yf = y - yi;
        var u = xf * xf * (3 - 2 * xf);
        var v = yf * yf * (3 - 2 * yf);
        var a = Hash(xi, yi) + (Hash(xi + 1, yi) - Hash(xi, yi)) * u;
        var b = Hash(xi, yi + 1) + (Hash(xi + 1, yi + 1) - Hash(xi, yi + 1)) * u;
        return a + (b - a) * v;
    }
}

internal sealed class Particle
{
    public float X, Y, VX, VY, Age, Life, Size, Seed, Rot, VRot;
    public bool Front;

    public float T => Age / Life;
}

internal static class ParticleList
{
    /// <summary>Ages particles, swap-removing the dead ones, and calls <paramref name="step"/> on survivors.</summary>
    public static void Update(List<Particle> list, float dt, Action<Particle> step)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var p = list[i];
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                list[i] = list[^1];
                list.RemoveAt(list.Count - 1);
                continue;
            }

            step(p);
        }
    }
}

/// <summary>
/// Batches soft additive glow sprites into a single DrawAtlas call — far cheaper than
/// blurring hundreds of individual circles every frame.
/// </summary>
internal sealed class GlowBatch
{
    const int S = 128;
    static SKImage? _sprite;
    static SKRect[] _rects = [];

    readonly List<SKRotationScaleMatrix> _xf = new();
    readonly List<SKColor> _colors = new();
    readonly SKPaint _paint = new() { IsAntialias = true };

    static SKImage Sprite => _sprite ??= CreateSprite();

    static SKImage CreateSprite()
    {
        using var surface = SKSurface.Create(new SKImageInfo(S, S, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        var c = surface.Canvas;
        c.Clear(SKColors.Transparent);
        using var p = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(S / 2f, S / 2f), S / 2f,
                [SKColors.White, SKColors.White.WithAlpha(150), SKColors.White.WithAlpha(52), SKColors.White.WithAlpha(12), SKColors.White.WithAlpha(0)],
                [0f, 0.18f, 0.45f, 0.75f, 1f],
                SKShaderTileMode.Clamp),
        };
        c.DrawRect(0, 0, S, S, p);
        return surface.Snapshot();
    }

    public void Add(float x, float y, float radius, SKColor color)
    {
        if (radius <= 0.1f || color.Alpha == 0)
        {
            return;
        }

        var k = radius * 2f / S;
        _xf.Add(new SKRotationScaleMatrix(k, 0, x - radius, y - radius));
        _colors.Add(color);
    }

    public void Flush(SKCanvas canvas, SKBlendMode blend = SKBlendMode.Plus)
    {
        var n = _xf.Count;
        if (n == 0)
        {
            return;
        }

        if (_rects.Length != n)
        {
            _rects = new SKRect[n];
            Array.Fill(_rects, new SKRect(0, 0, S, S));
        }

        _paint.BlendMode = blend;
        canvas.DrawAtlas(Sprite, _rects, _xf.ToArray(), _colors.ToArray(), SKBlendMode.Modulate, new SKSamplingOptions(SKFilterMode.Linear), _paint);
        _xf.Clear();
        _colors.Clear();
    }
}
