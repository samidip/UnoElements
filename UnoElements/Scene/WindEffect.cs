using SkiaSharp;

namespace UnoElements.Scene;

/// <summary>
/// Moving air: streamlines advected through an ideal flow around the orb (uniform stream + doublet +
/// circulation, i.e. lift), with a turbulent wake behind it and leaves caught in the current.
/// </summary>
internal sealed class WindEffect : IElementEffect
{
    sealed class Streak
    {
        public float X, Y, Speed, Age, Width;
        public bool Front;
        public required SKPoint[] Trail;
        public int Count, Head;

        public SKPoint this[int i] => Trail[(Head - Count + i + Trail.Length) % Trail.Length];

        public void Push(SKPoint p)
        {
            Trail[Head] = p;
            Head = (Head + 1) % Trail.Length;
            Count = Math.Min(Count + 1, Trail.Length);
        }
    }

    readonly List<Streak> _streaks = new();
    readonly List<Particle> _leaves = new();
    readonly SKPaint _line = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Butt, BlendMode = SKBlendMode.Plus };
    readonly SKPaint _fill = new() { IsAntialias = true };
    float _streakAcc, _leafAcc;
    float _time;

    float Radius(ElementState e) => e.Rect.Width * 0.5f * 1.12f;

    (float vx, float vy) Field(ElementState e, float x, float y)
    {
        var s = e.Scale;
        var c = e.Focus;
        var R = Radius(e);
        var U = 230 * s;

        var dx = x - c.X;
        var dy = y - c.Y;
        var r2 = Math.Max(dx * dx + dy * dy, R * R * 1.04f);
        var r = MathF.Sqrt(r2);
        var cos = dx / r;
        var sin = dy / r;
        var k = R * R / r2;

        // Potential flow past a cylinder with circulation
        var ur = U * (1 - k) * cos;
        var ut = -U * (1 + k) * sin - U * 0.25f * R / r;
        var vx = ur * cos - ut * sin;
        var vy = ur * sin + ut * cos;

        vy += Noise.Get(x * 0.006f / s + _time * 0.4f, y * 0.006f / s) * 40 * s;
        if (dx > 0)
        {
            // Turbulent wake downstream
            var wake = Math.Min(1f, dx / (2 * R)) * MathF.Exp(-dy * dy / (R * R * 2.2f));
            vy += Noise.Get(x * 0.01f / s - _time * 1.4f, y * 0.01f / s) * U * 0.6f * wake;
        }

        if (dx * dx + dy * dy < R * R)
        {
            vx += cos * U;
            vy += sin * U;
        }

        return (vx, vy);
    }

    public void Update(ElementState e, float dt, float time, float floorY)
    {
        _time = time;
        var a = e.ActiveT;
        var c = e.Focus;
        var R = Radius(e);
        var s = e.Scale;

        _streakAcc += dt * (110 * a + 300 * e.Pulse);
        while (_streakAcc >= 1)
        {
            _streakAcc -= 1;
            var gust = Rand.Next() < 0.18f;
            _streaks.Add(new Streak
            {
                X = c.X - R * Rand.Range(3.2f, 3.6f),
                Y = c.Y + Math.Clamp(Rand.Gauss() * R * 0.8f, -R * 2f, R * 2f),
                Speed = gust ? Rand.Range(1.3f, 1.7f) : Rand.Range(0.75f, 1.25f),
                Width = gust ? 1.5f : Rand.Range(0.6f, 1.1f),
                Trail = new SKPoint[gust ? 36 : Rand.Next() < 0.5f ? 16 : 24],
                Front = Rand.Next() < 0.2f,
            });
        }

        for (var i = _streaks.Count - 1; i >= 0; i--)
        {
            var p = _streaks[i];
            p.Age += dt;
            var (vx, vy) = Field(e, p.X, p.Y);
            p.X += vx * p.Speed * dt;
            p.Y += vy * p.Speed * dt;
            p.Push(new SKPoint(p.X, p.Y));
            if (p.X > c.X + R * 3.8f || p.Age > 8)
            {
                _streaks[i] = _streaks[^1];
                _streaks.RemoveAt(_streaks.Count - 1);
            }
        }

        _leafAcc += dt * 2.2f * a;
        while (_leafAcc >= 1)
        {
            _leafAcc -= 1;
            _leaves.Add(new Particle
            {
                X = c.X - R * 3.4f,
                Y = c.Y + Rand.Gauss() * R * 1.1f,
                Life = 9,
                Size = Rand.Range(6, 10) * s,
                Rot = Rand.Range(0, MathF.Tau),
                VRot = Rand.Range(-5, 5),
                Seed = Rand.Range(0, 100),
            });
        }

        ParticleList.Update(_leaves, dt, p =>
        {
            var (vx, vy) = Field(e, p.X, p.Y);
            p.X += vx * 0.7f * dt;
            p.Y += (vy * 0.7f + MathF.Sin(p.Age * 3 + p.Seed) * 30 * s) * dt;
            p.Rot += p.VRot * dt;
            if (p.X > c.X + R * 3.8f)
            {
                p.Age = p.Life;
            }
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
            _fill.BlendMode = SKBlendMode.Plus;
            _fill.Shader = SKShader.CreateRadialGradient(
                e.Focus, Radius(e) * 2.6f,
                [e.P.Mid.A(0.28f * a), e.P.Mid.A(0.08f * a), e.P.Mid.A(0)],
                [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
            c.DrawCircle(e.Focus, Radius(e) * 2.6f, _fill);
            _fill.Shader = null;
            _fill.BlendMode = SKBlendMode.SrcOver;
        }

        DrawStreaks(c, e, false);
    }

    public void DrawFront(SKCanvas c, ElementState e, float time)
    {
        DrawStreaks(c, e, true);

        foreach (var p in _leaves)
        {
            var fade = Math.Min(1f, p.Age * 2) * Math.Min(1f, (p.Life - p.Age) * 2);
            c.Save();
            c.Translate(p.X, p.Y);
            c.RotateRadians(p.Rot);
            c.Scale(1f, 0.55f + 0.45f * MathF.Abs(MathF.Sin(p.Age * 2.4f + p.Seed)));
            using var leaf = new SKPath();
            leaf.MoveTo(-p.Size, 0);
            leaf.QuadTo(0, -p.Size * 0.75f, p.Size, 0);
            leaf.QuadTo(0, p.Size * 0.75f, -p.Size, 0);
            _fill.Color = Col.Lerp(e.P.Accent, e.P.Bright, 0.5f + 0.5f * MathF.Sin(p.Seed)).A(0.85f * fade * e.ActiveT);
            c.DrawPath(leaf, _fill);
            c.Restore();
        }
    }

    void DrawStreaks(SKCanvas c, ElementState e, bool front)
    {
        var a = e.ActiveT;
        if (a < 0.005f && _streaks.Count == 0)
        {
            return;
        }

        var center = e.Focus;
        var R = Radius(e);
        foreach (var p in _streaks)
        {
            if (p.Front != front || p.Count < 2)
            {
                continue;
            }

            var ageFade = Math.Min(1f, p.Age * 3);
            for (var i = 1; i < p.Count; i++)
            {
                var p0 = p[i - 1];
                var p1 = p[i];
                var f = i / (float)(p.Count - 1);
                var ex = (p1.X - center.X) / (2.2f * R);
                var ey = (p1.Y - center.Y) / (1.8f * R);
                var env = MathF.Exp(-(ex * ex) - (ey * ey));
                var alpha = f * f * env * ageFade * (0.25f + 0.75f * a) * (front ? 0.55f : 0.9f);
                if (alpha < 0.01f)
                {
                    continue;
                }

                _line.Color = Col.Lerp(e.P.Bright, e.P.Core, f).A(alpha);
                _line.StrokeWidth = (0.5f + 1.6f * f) * e.Scale * p.Width;
                c.DrawLine(p0, p1, _line);
            }
        }
    }
}
