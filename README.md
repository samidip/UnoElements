# UnoElements

A sample [Uno Platform](https://platform.uno) app that brings the planet's classical elements — **Fire, Wind, Water and Earth** — to life as animated, interactive scenes.

Each element sits on a glossy stage with its own real-time effect, drawn with SkiaSharp:

| Element | Effect |
| --- | --- |
| 🔥 **Fire** — Combustion | Flames and embers rise as hot air carries them upward. |
| 🌬️ **Wind** — Flow | Air parts around an obstacle, speeds up over the curve and tumbles into eddies. |
| 💧 **Water** — Ripple | A falling drop sends rings of energy across the surface. |
| 🪨 **Earth** — Gravity | Stone holds fragments in orbit while the ground shifts, cracks and settles. |

Click an element to wake it up: its color floods the room and a caption explains the science behind it. Press **Esc** or click the backdrop to let it rest.

## What it shows

- **XAML + Skia together** — XAML owns layout, input and text (`ElementCard`, `MainPage`), while a full-window `SKCanvasElement` (`Scene/ElementsScene.cs`) owns the pixels: backdrop, floor reflections, silhouettes and particle effects.
- **Frame-driven animation** — `CompositionTarget.Rendering` advances the scene each frame, with smooth easing between idle, hover and active states.
- **Single-project Uno app** — one `UnoElements.csproj` using the Uno.Sdk with the Material theme and Skia renderer.

## Project layout

```
UnoElements/
├── Controls/      ElementCard — the XAML card for each element
├── Scene/         Skia stage plus one effect class per element
├── Styles/        Color palette and text styles
└── MainPage.xaml  Layout, captions and interaction
```

## Getting started

**Prerequisites:** the [.NET 10 SDK](https://dotnet.microsoft.com/download) and an environment set up for Uno Platform — run [`uno-check`](https://aka.platform.uno/uno-check) to verify.

```bash
git clone https://github.com/samidip/UnoElements.git
cd UnoElements
dotnet run --project UnoElements/UnoElements.csproj -f net10.0-desktop
```

The app targets **Desktop** (`net10.0-desktop`, Windows/macOS/Linux) and **Windows App SDK** (`net10.0-windows10.0.26100`). You can also open `UnoElements.sln` in Visual Studio, VS Code or Rider.

## Learn more

- [Uno Platform documentation](https://platform.uno/docs/)
- [Using the Uno.Sdk](https://aka.platform.uno/using-uno-sdk)
- [SKCanvasElement in Uno Platform](https://platform.uno/docs/articles/controls/SKCanvasElement.html)
