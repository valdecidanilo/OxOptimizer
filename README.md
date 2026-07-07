# OxOptimizer

WebGL optimization toolkit for Unity, by [OxenteGames](https://github.com/OxenteGames).

Adds a `Tools > OxOptimizer (WebGL)` window that audits your project and helps you ship WebGL builds that are smaller, load faster and run reliably on mobile browsers.

## Features

- **Export** — checks WebGL export settings (Brotli, name files as hashes, exception support, code stripping) with one-click fixes.
- **Memory** — applies a mobile-safe WebGL memory preset validated on real devices.
- **Textures / Models / Audio clips** — audits import settings of every asset in the build so you can find the heavy ones.
- **Fonts** — lists every .ttf/.otf with its "Include Font Data" state and disables it per font or all at once (safe for TextMesh Pro projects).
- **Build logs** — parses the Unity build log and shows which assets contribute most to the build size.
- **Localization** — English by default, switchable to Português (BR) from the header.

## Mobile-safe memory preset

| Setting | Value |
| --- | --- |
| Initial Memory Size | 128 MB |
| Maximum Memory Size | 768 MB |
| Memory Growth Mode | Geometric |
| Geometric Growth Step | 0.2 |
| Geometric Growth Cap | 32 MB |

Start small (mobile browsers kill tabs that allocate too much upfront), grow geometrically in small capped steps, and stay under a ceiling that mobile browsers can handle.

## Installation

`Window > Package Manager` → `+` → `Add package from git URL...`:

```
https://github.com/OxenteGames/OxOptimizer.git
```

Requires Unity **2021.2+**. Editor-only — adds nothing to your builds.

## License

[MIT](LICENSE.md). Based on the [unity-optimizations-package](https://github.com/CrazyGamesCom/unity-optimizations-package) by CrazyGames.
