using SkiaSharp;

namespace UnoElements.Scene;

/// <summary>
/// Water: drips fall and strike the drop, each impact sending echoes of its shape outward and a splash
/// upward; travelling waves pass behind it, ripples spread across the floor and bubbles rise.
/// </summary>
internal sealed class WaterEffect : IElementEffect
{
    sealed class Ring
    {
        public float Age, Life;
    }

    readonly List<Ring> _floorRings = new();
    readonly List<Ring> _echoes = new();
    readonly List<Particle> _bubbles = new();
    readonly List<Particle> _drips = new();
    readonly List<Particle> _splash = new();
    readonly GlowBatch _batch = new();
    readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    readonly SKPaint _fill = new() { IsAntialias = true };
    float _floorTimer, _dripTimer, _bubbleAcc;

    public void Update(ElementState e, float dt, float time, float floorY)
    {
        var a = e.ActiveT;
        var s = e.Scale;
        var r = e.Rect;
        var b = e.Bounds;

        if (e.Pulse > 0.95f)
        {
            _echoes.Add(new Ring { Life = 2.2f });
            _floorRings.Add(new Ring { Life = 3f });
        }

        if (e.IsActive)
        {
            _floorTimer -= dt;
            if (_floorTimer <= 0)
            {
                _floorTimer = 1.1f;
                _floorRings.Add(new Ring { Life = 3f });
            }

            _dripTimer -= dt;
            if (_dripTimer <= 0)
            {
                _dripTimer = Rand.Range(1.2f, 1.8f);
                _drips.Add(new Particle { X = b.MidX, Y = b.Top - r.Height * 0.5f, Life = 3, Size = 10 * s });
            }
        }

        _bubbleAcc += dt * 16 * a;
        while (_bubbleAcc >= 1)
        {
            _bubbleAcc -= 1;
            _bubbles.Add(new Particle
            {
                X = r.MidX + Rand.Range(-1.3f, 1.3f) * r.Width,
                Y = floorY - Rand.Range(0, 0.3f) * r.Height,
                VY = -Rand.Range(35, 85) * s,
                Life = Rand.Range(2.6f, 4.2f),
                Size = Rand.Range(2.5f, 8f) * s,
                Seed = Rand.Range(0, 100),
                Front = Rand.Next() < 0.45f,
            });
        }

        Age(_floorRings, dt);
        Age(_echoes, dt);

        ParticleList.Update(_drips, dt, p =>
        {
            p.VY += 1500 * s * dt;
            p.Y += p.VY * dt;
            if (p.Y >= b.Top + 6 * s)
            {
                p.Age = p.Life;
                _echoes.Add(new Ring { Life = 2.2f });
                for (var i = 0; i < 9; i++)
                {
                    var ang = -MathF.PI / 2 + Rand.Range(-1.1f, 1.1f);
                    var speed = Rand.Range(120, 260) * s;
                    _splash.Add(new Particle
                    {
                        X = p.X,
                        Y = b.Top + 4 * s,
                        VX = MathF.Cos(ang) * speed,
                        VY = MathF.Sin(ang) * speed,
                        Life = Rand.Range(0.5f, 0.9f),
                        Size = Rand.Range(1.5f, 3f) * s,
                    });
                }
            }
        });

        ParticleList.Update(_splash, dt, p =>
        {
            p.VY += 900 * s * dt;
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
        });

        ParticleList.Update(_bubbles, dt, p =>
        {
            p.X += MathF.Sin(p.Age * 2.6f + p.Seed) * 18 * s * dt;
            p.Y += p.VY * dt;
        });
    }

    static void Age(List<Ring> rings, float dt)
    {
        for (var i = rings.Count - 1; i >= 0; i--)
        {
            rings[i].Age += dt;
            if (rings[i].Age >= rings[i].Life)
            {
                rings.RemoveAt(i);
            }
        }
    }

    public void DrawFloor(SKCanvas c, ElementState e, float floorY, float time)
    {
        var s = e.Scale;
        var w = e.Rect.Width;
        var fade = Math.Max(e.ActiveT, 0.4f);
        foreach (var ring in _floorRings)
        {
            var t = ring.Age / ring.Life;
            var ease = 1 - MathF.Pow(1 - t, 2.2f);
            var rx = w * (0.35f + ease * 1.9f);
            var rect = new SKRect(e.Rect.MidX - rx, floorY + 4 * s - rx * 0.15f, e.Rect.MidX + rx, floorY + 4 * s + rx * 0.15f);
            var alpha = MathF.Pow(1 - t, 1.6f) * fade;

            _stroke.BlendMode = SKBlendMode.Plus;
            _stroke.StrokeWidth = 5 * s * (1 - t * 0.5f);
            _stroke.Color = e.P.Mid.A(0.25f * alpha);
            c.DrawOval(rect, _stroke);
            _stroke.StrokeWidth = 1.4f * s;
            _stroke.Color = e.P.Core.A(0.65f * alpha);
            c.DrawOval(rect, _stroke);
            _stroke.BlendMode = SKBlendMode.SrcOver;
        }
    }

    public void DrawBack(SKCanvas c, ElementState e, float time)
    {
        var a = e.ActiveT;
        var s = e.Scale;
        var r = e.Rect;

        if (a > 0.01f)
        {
            // Travelling waves crossing behind the drop
            _stroke.BlendMode = SKBlendMode.Plus;
            for (var k = 0; k < 4; k++)
            {
                var span = r.Width * 2.4f;
                var yBase = r.Top + r.Height * (0.42f + 0.11f * k);
                var amp = (10 + 5 * k) * s * a;
                var len = (150 + 34 * k) * s;
                var speed = 1.4f + 0.35f * k;
                using var wave = new SKPath();
                for (var i = 0; i <= 90; i++)
                {
                    var x = r.MidX - span + span * 2 * i / 90f;
                    var y = yBase + amp * MathF.Sin((x - r.MidX) / len * MathF.Tau - time * speed + k)
                                  + amp * 0.35f * MathF.Sin((x - r.MidX) / len * 2.3f - time * speed * 1.7f);
                    if (i == 0)
                    {
                        wave.MoveTo(x, y);
                    }
                    else
                    {
                        wave.LineTo(x, y);
                    }
                }

                var col = Col.Lerp(e.P.Bright, e.P.Accent, k / 3f);
                _stroke.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(r.MidX - span, 0), new SKPoint(r.MidX + span, 0),
                    [col.A(0), col.A(0.55f * a), col.A(0.75f * a), col.A(0.55f * a), col.A(0)],
                    [0f, 0.3f, 0.5f, 0.7f, 1f], SKShaderTileMode.Clamp);
                _stroke.StrokeWidth = (2.2f - k * 0.35f) * s;
                c.DrawPath(wave, _stroke);
            }

            _stroke.Shader = null;
            _stroke.BlendMode = SKBlendMode.SrcOver;
        }

        // Echoes of the drop's outline expanding after each impact
        foreach (var ring in _echoes)
        {
            var t = ring.Age / ring.Life;
            var ease = 1 - MathF.Pow(1 - t, 3);
            var k = 1 + ease * 0.85f;
            c.Save();
            c.Translate(e.Focus.X, e.Focus.Y);
            c.Scale(k, k);
            c.Translate(-e.Focus.X, -e.Focus.Y);
            _stroke.StrokeWidth = 2f * s / k;
            _stroke.Color = Col.Lerp(e.P.Core, e.P.Bright, t).A(0.7f * MathF.Pow(1 - t, 1.8f) * Math.Max(a, 0.4f));
            c.DrawPath(e.Path, _stroke);
            c.Restore();
        }

        DrawBubbles(c, e, false);
    }

    public void DrawFront(SKCanvas c, ElementState e, float time)
    {
        var s = e.Scale;
        foreach (var p in _drips)
        {
            var rect = new SKRect(p.X - p.Size * 0.5f, p.Y - p.Size * 1.4f, p.X + p.Size * 0.5f, p.Y);
            using var drop = new SKPath();
            Shapes.AddDroplet(drop, rect);
            _fill.Color = e.P.Core.A(0.9f);
            c.DrawPath(drop, _fill);
            _batch.Add(p.X, p.Y - p.Size * 0.5f, p.Size * 2, e.P.Bright.A(0.5f));
        }

        foreach (var p in _splash)
        {
            _batch.Add(p.X, p.Y, p.Size * 3, e.P.Bright.A(0.5f * (1 - p.T)));
            _batch.Add(p.X, p.Y, p.Size, e.P.Core.A(1 - p.T));
        }

        _batch.Flush(c);
        DrawBubbles(c, e, true);
    }

    void DrawBubbles(SKCanvas c, ElementState e, bool front)
    {
        var s = e.Scale;
        foreach (var p in _bubbles)
        {
            if (p.Front != front)
            {
                continue;
            }

            var t = p.T;
            var fade = Math.Min(1f, t / 0.1f) * (1 - t) * (front ? 0.8f : 0.55f);
            _fill.Color = e.P.Bright.A(0.10f * fade);
            c.DrawCircle(p.X, p.Y, p.Size, _fill);
            _stroke.StrokeWidth = 1.1f * s;
            _stroke.Color = e.P.Core.A(0.7f * fade);
            c.DrawCircle(p.X, p.Y, p.Size, _stroke);
            _fill.Color = SKColors.White.A(0.85f * fade);
            c.DrawCircle(p.X - p.Size * 0.35f, p.Y - p.Size * 0.35f, Math.Max(0.8f, p.Size * 0.18f), _fill);
        }
    }
}
