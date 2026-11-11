namespace UnoElements.Controls;

/// <summary>
/// Layout slot, labels and hit target for one element. The silhouette itself is painted by the
/// Skia scene behind it, positioned from <see cref="ShapeArea"/>.
/// </summary>
public sealed partial class ElementCard : UserControl
{
    public static readonly DependencyProperty NumberProperty =
        DependencyProperty.Register(nameof(Number), typeof(string), typeof(ElementCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ElementCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TaglineProperty =
        DependencyProperty.Register(nameof(Tagline), typeof(string), typeof(ElementCard), new PropertyMetadata(string.Empty));

    public ElementCard()
    {
        InitializeComponent();
    }

    public string Number
    {
        get => (string)GetValue(NumberProperty);
        set => SetValue(NumberProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Tagline
    {
        get => (string)GetValue(TaglineProperty);
        set => SetValue(TaglineProperty, value);
    }

    /// <summary>The element whose bounds the silhouette is drawn into.</summary>
    public FrameworkElement ShapeArea => HitTarget;

    public event EventHandler? Invoked;

    public event EventHandler<bool>? HoverChanged;

    /// <summary>0 = dormant, 1 = fully awake. Dims the labels of sleeping elements.</summary>
    public void SetEmphasis(float value) => Labels.Opacity = 0.62 + 0.38 * value;

    void OnClick(object sender, RoutedEventArgs e) => Invoked?.Invoke(this, EventArgs.Empty);

    void OnPointerEntered(object sender, PointerRoutedEventArgs e) => HoverChanged?.Invoke(this, true);

    void OnPointerExited(object sender, PointerRoutedEventArgs e) => HoverChanged?.Invoke(this, false);
}
