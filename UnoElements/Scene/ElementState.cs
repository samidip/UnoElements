using SkiaSharp;

namespace UnoElements.Scene;

internal interface IElementEffect
{
    void Update(ElementState e, float dt, float time, float floorY);

    /// <summary>Marks on the floor plane (ripples, cracks) — not mirrored in the reflection.</summary>
    void DrawFloor(SKCanvas c, ElementState e, float floorY, float time);

    /// <summary>Behind every card.</summary>
    void DrawBack(SKCanvas c, ElementState e, float time);

    /// <summary>In front of every card.</summary>
    void DrawFront(SKCanvas c, ElementState e, float time);
}

internal sealed class ElementState(ElementKind kind, Palette palette, IElementEffect effect)
{
    /// <summary>Design width of a card slot; effect sizes are authored against it.</summary>
    public const float DesignWidth = 240f;

    public ElementKind Kind { get; } = kind;
    public Palette P { get; } = palette;
    public IElementEffect Effect { get; } = effect;

    public SKRect Rect { get; private set; }
    public SKPath Path { get; private set; } = new();
    public SKRect Bounds { get; private set; }
    public SKPoint Focus { get; private set; }
    public float Scale { get; private set; } = 1f;

    public bool IsHovered { get; set; }
    public bool IsActive { get; set; }
    public float HoverT { get; set; }
    public float ActiveT { get; set; }

    /// <summary>A brief flash when the element is (re)activated.</summary>
    public float Pulse { get; set; }

    public bool HasLayout => Rect.Width > 1;

    public void SetRect(SKRect r)
    {
        if (r == Rect)
        {
            return;
        }

        Rect = r;
        Scale = r.Width / DesignWidth;
        Path.Dispose();
        Path = Shapes.Create(Kind, r);
        Bounds = Path.Bounds;
        Focus = Shapes.Focus(Kind, r);
    }
}
