using System.Diagnostics;
using UnoElements.Controls;
using Uno.WinUI.Graphics2DSK;
using UnoElements.Scene;
using Windows.Foundation;
using Windows.System;

namespace UnoElements;

public sealed partial class MainPage : Page
{
    static readonly (string Title, string Body)[] Captions =
    [
        ("FIRE — COMBUSTION", "Fuel, oxygen and heat meet, and matter lets go of itself as light. Hot air rises and the flame chases it upward."),
        ("WIND — FLOW", "Air never pushes straight through. It parts around whatever stands in its way, speeds up over the curve, and tumbles into eddies behind."),
        ("WATER — RIPPLE", "One drop lands and the whole surface answers. Energy travels outward in rings while the water itself barely moves."),
        ("EARTH — GRAVITY", "Mass pulls on mass. Stone holds fragments in orbit, the ground shifts and cracks, and dust slowly settles."),
    ];

    const string IdleTitle = "CHOOSE AN ELEMENT";
    const string IdleBody = "Click any element to wake it up. Press Esc or click the backdrop to let it rest.";

    readonly ElementCard[] _cards;
    readonly ElementsScene? _scene;
    readonly Stopwatch _clock = new();
    double _lastSeconds;
    int _captionIndex = -2;

    public MainPage()
    {
        InitializeComponent();

        _cards = [FireCard, WindCard, WaterCard, EarthCard];

        if (SKCanvasElement.IsSupportedOnCurrentPlatform())
        {
            _scene = new ElementsScene();
            SceneHost.Children.Add(_scene);
        }

        for (var i = 0; i < _cards.Length; i++)
        {
            var index = i;
            _cards[i].Invoked += (_, _) => Toggle(index);
            _cards[i].HoverChanged += (_, hovered) => _scene?.SetHovered(index, hovered);
        }

        KeyDown += OnKeyDown;
        Loaded += (_, _) =>
        {
            _clock.Start();
            CompositionTarget.Rendering += OnRendering;
        };
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnRendering;

        UpdateCaption(-1);
    }

    void Toggle(int index)
    {
        if (_scene is null)
        {
            return;
        }

        var next = _scene.ActiveIndex == index ? -1 : index;
        _scene.ActiveIndex = next;
        if (next >= 0)
        {
            _scene.Ignite(next);
        }

        UpdateCaption(next);
    }

    void UpdateCaption(int index)
    {
        if (index == _captionIndex)
        {
            return;
        }

        _captionIndex = index;
        (CaptionTitle.Text, CaptionBody.Text) = index >= 0 ? Captions[index] : (IdleTitle, IdleBody);
        Caption.Opacity = 0;
    }

    void OnRendering(object? sender, object e)
    {
        if (_scene is null)
        {
            return;
        }

        var now = _clock.Elapsed.TotalSeconds;
        var dt = (float)Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        for (var i = 0; i < _cards.Length; i++)
        {
            var area = _cards[i].ShapeArea;
            if (area.ActualWidth > 0)
            {
                var bounds = area.TransformToVisual(_scene).TransformBounds(new Rect(0, 0, area.ActualWidth, area.ActualHeight));
                _scene.SetSlot(i, bounds);
            }

            _cards[i].SetEmphasis(_scene.ActiveIndex < 0 ? 0.6f + 0.4f * _scene.GetEmphasis(i) : _scene.GetEmphasis(i));
        }

        _scene.Step(dt);
        _scene.Invalidate();

        Caption.Opacity += (1 - Caption.Opacity) * (1 - Math.Exp(-4 * dt));
    }

    void OnRootTapped(object sender, TappedRoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement source && FindCard(source) is null && _scene?.ActiveIndex >= 0)
        {
            Toggle(_scene.ActiveIndex);
        }
    }

    static ElementCard? FindCard(DependencyObject? element)
    {
        while (element is not null and not ElementCard)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        return element as ElementCard;
    }

    void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && _scene?.ActiveIndex >= 0)
        {
            Toggle(_scene.ActiveIndex);
            e.Handled = true;
        }
    }
}
