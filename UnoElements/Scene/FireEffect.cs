using SkiaSharp;

namespace UnoElements.Scene;

/// <summary>
/// Combustion: buoyant flame plumes that rise, narrow and cool from white-hot to red,
/// plus embers lofted far above on turbulent heat.
/// </summary>
internal sealed class FireEffect : IElementEffect
{
    static readonly SKColor[] FlameColors =
    [
        new(255, 248, 222), new(255, 196, 96), new(255, 112, 40), new(218, 38, 72), new(90, 8, 40),
    ];

    static readonly float[] FlameStops = [0f, 0.16f, 0.4f, 0.72f, 1f];

    readonly List<Particle> _flames = new();
    readonly List<Particle> _embers = new();
    readonly GlowBatch _batch = new();
    readonly SKPaint _paint = new() { IsAntialias = true };
    float _flameAcc, _emberAcc;

    public void Update(ElementState e, float dt, float time, float floorY)
    {
        var a = e.ActiveT;
        var s = e.Scale;
        var r = e.Rect;

        _flameAcc += dt * (520 * a + 900 * e.Pulse);
        while (_flameAcc >= 1)
        {
            _flameAcc -= 1;
            var spread = Rand.Gauss() * 0.26f;
            _flames.Add(new Particle
            {
                X = r.MidX + spread * r.Width,
                // Hug the silhouette: tall in the middle, lower toward the flanks
                Y = r.Top + r.Height * (0.08f + MathF.Abs(spread) * 1.2f + Rand.Range(0f, 0.22f)),
                VX = Rand.Range(-15, 15) * s,
                VY = -Rand.Range(110, 220) * s,
                Life = Rand.Range(0.6f, 1.35f),
                Size = Rand.Range(20, 42) * s,
                Seed = Rand.Range(0, 100),
            });
        }

        _emberAcc += dt * (24 * a + 80 * e.Pulse);
        while (_emberAcc >= 1)
        {
            _emberAcc -= 1;
            _embers.Add(new Particle
            {
                X = r.MidX + Rand.Gauss() * r.Width * 0.35f,
                Y = r.Top + r.Height * Rand.Range(0.05f, 0.55f),
                VX = Rand.Range(-40, 40) * s,
                VY = -Rand.Range(70, 170) * s,
                Life = Rand.Range(1.4f, 3.2f),
                Size = Rand.Range(1.6f, 3.6f) * s,
                Seed = Rand.Range(0, 100),
                Front = Rand.Next() < 0.6f,
            });
        }

        ParticleList.Update(_flames, dt, p =>
        {
            // Turbulence + pull towards the plume axis (hot air converges as it rises), + buoyancy.
            var turb = Noise.Get(p.Y * 0.012f / s, time * 1.8f + p.Seed) * 260 * s;
            p.VX += (turb - p.VX * 1.3f + (r.MidX - p.X) * 2.4f) * dt;
            p.VY -= 110 * s * dt;
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
        });

        ParticleList.Update(_embers, dt, p =>
        {
            p.VX += Noise.Get(p.Seed, time * 0.9f) * 160 * s * dt;
            p.VY -= 10 * s * dt;
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
        });
    }

    public void DrawFloor(SKCanvas c, ElementState e, float floorY, float time)
    {
    }

    public void DrawBack(SKCanvas c, ElementState e, float time)
    {
        var a = e.ActiveT;
        if (a > 0.01f)
        {
            // Heat bloom, flickering
            var flicker = 0.85f + 0.15f * Noise.Get(time * 6, 3);
            var center = new SKPoint(e.Rect.MidX, e.Rect.Top + e.Rect.Height * 0.3f);
            _paint.BlendMode = SKBlendMode.Plus;
            _paint.Shader = SKShader.CreateRadialGradient(
                center, e.Rect.Width * 1.5f,
                [e.P.Bright.A(0.32f * a * flicker), e.P.Mid.A(0.14f * a), e.P.Mid.A(0)],
                [0f, 0.45f, 1f], SKShaderTileMode.Clamp);
            c.DrawCircle(center, e.Rect.Width * 1.5f, _paint);
            _paint.Shader = null;
        }

        foreach (var p in _flames)
        {
            var t = p.T;
            var fadeIn = Math.Min(1f, t / 0.08f);
            var size = p.Size * (1f - t * 0.75f);
            var color = Col.Ramp(t, FlameColors, FlameStops);
            var alpha = 0.3f * fadeIn * MathF.Pow(1f - t, 1.1f);

            // A short vertical chain of sprites stretches each puff into a licking tongue
            _batch.Add(p.X, p.Y, size, color.A(alpha));
            _batch.Add(p.X - p.VX * 0.04f, p.Y + size * 0.7f, size * 0.8f, color.A(alpha * 0.8f));
            _batch.Add(p.X + p.VX * 0.03f, p.Y - size * 0.6f, size * 0.6f, Col.Ramp(Math.Min(1, t + 0.15f), FlameColors, FlameStops).A(alpha * 0.7f));
        }

        DrawEmbers(false);
        _batch.Flush(c);
    }

    public void DrawFront(SKCanvas c, ElementState e, float time)
    {
        DrawEmbers(true);
        _batch.Flush(c);
    }

    void DrawEmbers(bool front)
    {
        foreach (var p in _embers)
        {
            if (p.Front != front)
            {
                continue;
            }

            var t = p.T;
            var flicker = 0.55f + 0.45f * MathF.Sin(p.Age * 18 + p.Seed);
            var fade = Math.Min(1f, t / 0.1f) * (1f - t);
            var color = Col.Lerp(new SKColor(255, 236, 170), new SKColor(255, 90, 40), t);
            _batch.Add(p.X, p.Y, p.Size * 4f, color.A(0.5f * fade * flicker));
            _batch.Add(p.X, p.Y, p.Size, SKColors.White.A(0.9f * fade * flicker));
        }
    }
}
