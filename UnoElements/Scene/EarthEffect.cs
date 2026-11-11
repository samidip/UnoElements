using SkiaSharp;

namespace UnoElements.Scene;

/// <summary>
/// Earth: mass and gravity — rock fragments held in tilted orbits (passing behind and in front of the
/// crystal), the ground cracking open with molten light, seismic pulses and drifting dust.
/// </summary>
internal sealed class EarthEffect : IElementEffect
{
    sealed class Rock
    {
        public int Ring;
        public float Angle, Size, Spin, SpinSpeed;
        public required SKPoint[] Verts;
    }

    // Orbit rings: radius (× card width), flattening, tilt (radians), angular speed
    static readonly (float Radius, float Flat, float Tilt, float Speed)[] Rings =
    [
        (0.78f, 0.26f, -0.16f, 0.62f),
        (1.02f, 0.22f, 0.10f, 0.45f),
        (1.28f, 0.18f, -0.05f, 0.32f),
    ];

    readonly Rock[] _rocks;
    readonly SKPoint[][] _cracks;
    readonly List<Particle> _dust = new();
    readonly List<float> _pulses = new();
    readonly GlowBatch _batch = new();
    readonly SKPaint _fill = new() { IsAntialias = true };
    readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    float _dustAcc, _pulseTimer, _orbitBoost;

    public EarthEffect()
    {
        _rocks = new Rock[13];
        for (var i = 0; i < _rocks.Length; i++)
        {
            var n = (int)Rand.Range(5, 8.99f);
            var verts = new SKPoint[n];
            for (var k = 0; k < n; k++)
            {
                var ang = MathF.Tau * k / n + Rand.Range(-0.3f, 0.3f);
                var rad = Rand.Range(0.62f, 1f);
                verts[k] = new SKPoint(MathF.Cos(ang) * rad, MathF.Sin(ang) * rad);
            }

            _rocks[i] = new Rock
            {
                Ring = i % 3,
                Angle = MathF.Tau * i / _rocks.Length * 3 + Rand.Range(-0.4f, 0.4f),
                Size = Rand.Range(9, 22),
                Spin = Rand.Range(0, MathF.Tau),
                SpinSpeed = Rand.Range(-1.2f, 1.2f),
                Verts = verts,
            };
        }

        _cracks = new SKPoint[9][];
        for (var i = 0; i < _cracks.Length; i++)
        {
            var dir = MathF.PI * (i + 0.5f) / _cracks.Length + Rand.Range(-0.12f, 0.12f) - (i % 2 == 0 ? 0 : MathF.PI);
            var length = Rand.Range(0.8f, 1.7f);
            var pts = new List<SKPoint> { SKPoint.Empty };
            var pos = SKPoint.Empty;
            var travelled = 0f;
            while (travelled < length)
            {
                var step = Rand.Range(0.06f, 0.13f);
                var ang = dir + Rand.Range(-0.45f, 0.45f);
                pos = new SKPoint(pos.X + MathF.Cos(ang) * step, pos.Y + MathF.Sin(ang) * step);
                pts.Add(pos);
                travelled += step;
            }

            _cracks[i] = pts.ToArray();
        }
    }

    public void Update(ElementState e, float dt, float time, float floorY)
    {
        var a = e.ActiveT;
        var s = e.Scale;
        var r = e.Rect;

        _orbitBoost = Math.Max(0, _orbitBoost - dt * 0.8f) + e.Pulse * dt * 4;
        foreach (var rock in _rocks)
        {
            rock.Angle += Rings[rock.Ring].Speed * dt * (1 + _orbitBoost * 3);
            rock.Spin += rock.SpinSpeed * dt;
        }

        if (e.Pulse > 0.95f)
        {
            _pulses.Add(0);
        }

        if (e.IsActive)
        {
            _pulseTimer -= dt;
            if (_pulseTimer <= 0)
            {
                _pulseTimer = 1.8f;
                _pulses.Add(0);
            }
        }

        for (var i = _pulses.Count - 1; i >= 0; i--)
        {
            _pulses[i] += dt;
            if (_pulses[i] > 1.6f)
            {
                _pulses.RemoveAt(i);
            }
        }

        _dustAcc += dt * 34 * a;
        while (_dustAcc >= 1)
        {
            _dustAcc -= 1;
            _dust.Add(new Particle
            {
                X = r.MidX + Rand.Gauss() * r.Width * 0.8f,
                Y = floorY - Rand.Range(0, 20) * s,
                VY = -Rand.Range(14, 46) * s,
                Life = Rand.Range(3, 5.5f),
                Size = Rand.Range(1, 2.6f) * s,
                Seed = Rand.Range(0, 100),
            });
        }

        ParticleList.Update(_dust, dt, p =>
        {
            p.X += Noise.Get(p.Seed, p.Age * 0.6f) * 22 * s * dt;
            p.Y += p.VY * dt;
        });
    }

    SKPoint OrbitPoint(ElementState e, int ring, float angle, float spread, out float depth)
    {
        var (radius, flat, tilt, _) = Rings[ring];
        var rx = e.Rect.Width * radius * spread;
        var ry = rx * flat;
        var lx = MathF.Cos(angle) * rx;
        var ly = MathF.Sin(angle) * ry;
        depth = MathF.Sin(angle);
        return new SKPoint(
            e.Focus.X + lx * MathF.Cos(tilt) - ly * MathF.Sin(tilt),
            e.Focus.Y + lx * MathF.Sin(tilt) + ly * MathF.Cos(tilt));
    }

    public void DrawFloor(SKCanvas c, ElementState e, float floorY, float time)
    {
        var a = e.ActiveT;
        var s = e.Scale;
        var w = e.Rect.Width;
        if (a < 0.01f)
        {
            return;
        }

        var origin = new SKPoint(e.Rect.MidX, floorY);
        var grow = 1 - MathF.Pow(1 - a, 3);

        using var glow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5 * s);
        using var path = new SKPath();
        for (var i = 0; i < _cracks.Length; i++)
        {
            var pts = _cracks[i];
            var visible = (int)MathF.Ceiling((pts.Length - 1) * grow);
            path.MoveTo(origin);
            for (var k = 1; k <= visible; k++)
            {
                path.LineTo(origin.X + pts[k].X * w, origin.Y + pts[k].Y * w * 0.2f);
            }
        }

        var flicker = 0.75f + 0.25f * Noise.Get(time * 3, 11);
        _stroke.BlendMode = SKBlendMode.Plus;
        _stroke.MaskFilter = glow;
        _stroke.StrokeWidth = 5 * s;
        _stroke.Color = e.P.Bright.A(0.55f * a * flicker);
        c.DrawPath(path, _stroke);
        _stroke.MaskFilter = null;
        _stroke.StrokeWidth = 1.3f * s;
        _stroke.Color = e.P.Core.A(0.85f * a);
        c.DrawPath(path, _stroke);

        foreach (var age in _pulses)
        {
            var t = age / 1.6f;
            var ease = 1 - MathF.Pow(1 - t, 2.5f);
            var rx = w * (0.3f + ease * 2.1f);
            var rect = new SKRect(origin.X - rx, origin.Y - rx * 0.13f, origin.X + rx, origin.Y + rx * 0.13f);
            _stroke.StrokeWidth = 3.5f * s * (1 - t);
            _stroke.Color = e.P.Bright.A(0.6f * MathF.Pow(1 - t, 2) * a);
            c.DrawOval(rect, _stroke);
        }

        _stroke.BlendMode = SKBlendMode.SrcOver;
    }

    public void DrawBack(SKCanvas c, ElementState e, float time)
    {
        var a = e.ActiveT;
        if (a < 0.005f)
        {
            return;
        }

        _fill.BlendMode = SKBlendMode.Plus;
        _fill.Shader = SKShader.CreateRadialGradient(
            e.Focus, e.Rect.Width * 1.5f,
            [e.P.Bright.A(0.18f * a), e.P.Mid.A(0.08f * a), e.P.Mid.A(0)],
            [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
        c.DrawCircle(e.Focus, e.Rect.Width * 1.5f, _fill);
        _fill.Shader = null;
        _fill.BlendMode = SKBlendMode.SrcOver;

        DrawOrbits(c, e, back: true);
        DrawRocks(c, e, back: true);

        foreach (var p in _dust)
        {
            var t = p.T;
            var fade = Math.Min(1f, t / 0.15f) * (1 - t);
            var sparkle = 0.5f + 0.5f * MathF.Sin(p.Age * 5 + p.Seed);
            _batch.Add(p.X, p.Y, p.Size * 3.5f, e.P.Bright.A(0.35f * fade * sparkle));
            _batch.Add(p.X, p.Y, p.Size, e.P.Core.A(0.8f * fade));
        }

        _batch.Flush(c);
    }

    public void DrawFront(SKCanvas c, ElementState e, float time)
    {
        if (e.ActiveT < 0.005f)
        {
            return;
        }

        DrawOrbits(c, e, back: false);
        DrawRocks(c, e, back: false);
    }

    void DrawOrbits(SKCanvas c, ElementState e, bool back)
    {
        var a = e.ActiveT;
        var spread = 0.35f + 0.65f * a;
        _stroke.StrokeWidth = 1f * e.Scale;
        for (var ring = 0; ring < Rings.Length; ring++)
        {
            using var path = new SKPath();
            var start = back ? MathF.PI : 0f;
            for (var i = 0; i <= 48; i++)
            {
                var pt = OrbitPoint(e, ring, start + MathF.PI * i / 48f, spread, out _);
                if (i == 0)
                {
                    path.MoveTo(pt);
                }
                else
                {
                    path.LineTo(pt);
                }
            }

            _stroke.Color = e.P.Bright.A((back ? 0.10f : 0.16f) * a);
            c.DrawPath(path, _stroke);
        }
    }

    void DrawRocks(SKCanvas c, ElementState e, bool back)
    {
        var a = e.ActiveT;
        var s = e.Scale;
        var spread = 0.35f + 0.65f * a;
        var grow = 1 - MathF.Pow(1 - a, 3);

        foreach (var rock in _rocks)
        {
            var pos = OrbitPoint(e, rock.Ring, rock.Angle, spread, out var depth);
            if (depth < 0 != back)
            {
                continue;
            }

            var size = rock.Size * s * (0.8f + 0.3f * (depth + 1) / 2) * grow;
            var light = 0.85f + 0.4f * (depth + 1) / 2;
            using var path = new SKPath();
            for (var k = 0; k < rock.Verts.Length; k++)
            {
                var v = rock.Verts[k];
                var cos = MathF.Cos(rock.Spin);
                var sin = MathF.Sin(rock.Spin);
                var pt = new SKPoint(pos.X + (v.X * cos - v.Y * sin) * size, pos.Y + (v.X * sin + v.Y * cos) * size);
                if (k == 0)
                {
                    path.MoveTo(pt);
                }
                else
                {
                    path.LineTo(pt);
                }
            }

            path.Close();

            _batch.Add(pos.X, pos.Y, size * 2.6f, e.P.Mid.A(0.35f * a * light));
            _batch.Flush(c);

            // Lit from the glowing crystal: the side facing it is brighter
            var toward = new SKPoint(e.Focus.X - pos.X, e.Focus.Y - pos.Y);
            var len = Math.Max(1, MathF.Sqrt(toward.X * toward.X + toward.Y * toward.Y));
            _fill.Shader = SKShader.CreateLinearGradient(
                new SKPoint(pos.X + toward.X / len * size, pos.Y + toward.Y / len * size),
                new SKPoint(pos.X - toward.X / len * size, pos.Y - toward.Y / len * size),
                [Col.Scale(e.P.Bright, light).A(a), Col.Scale(e.P.Mid, light * 0.7f).A(a), Col.Scale(e.P.Deep, 0.6f).A(a)],
                [0f, 0.45f, 1f], SKShaderTileMode.Clamp);
            c.DrawPath(path, _fill);
            _fill.Shader = null;

            _stroke.StrokeWidth = 1f * s;
            _stroke.Color = e.P.Core.A(0.35f * a * light);
            c.DrawPath(path, _stroke);
        }
    }
}
