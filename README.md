# OxOptimizer

[Imagem da logo centralizada]

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

## Using

Open the window from `Tools > OxOptimizer (WebGL)`. Each tab audits one part of your project — walk through them top to bottom.

### Export

[Imagem mostrando a tab Export overview mostrando a porcentagem]

The overview ring gives you a single optimization score at a glance. The **green** slice is how many checks already pass, the **gray** slice is the build-size grade that's still pending (it fills in once you run the **Build logs** tab), and the dark base is what's left to fix. Below it, each WebGL export setting (Brotli compression, name files as hashes, exception support, code stripping) is listed with a **Fix** button that applies the recommended value in one click.

### Memory

[Imagem mostrando a tab Memory]

Applies the mobile-safe WebGL memory preset validated on real devices. Each setting shows its current value next to the recommended one, with a green ✔ when it already matches and a **Fix** button when it doesn't. Use **Apply mobile-safe preset** to set all of them at once. Starting with a small heap that grows in small capped steps keeps mobile browsers from killing the tab.

### Textures

[Imagem mostrando a tab Textures mostrando as cores ideais]

Lists every texture that ends up in the build with its import settings (max size, compression, crunch). Each value is color-coded against the ideal: **white** = ideal, **orange** = worth attention, **red** = far from ideal. The legend and the ideal target for each column are shown right under the table, so you can spot the heavy textures at a glance and click one to select it in the Project view.

### Audio

[Imagem mostrando a tab Audio mostrando]

Same color-coded audit for audio clips: **Load type** (Compressed in memory / Streaming is preferred over Decompress on load) and **Quality** (lower is smaller). White/orange/red tell you how far each clip is from the recommended setting, with the ideal values listed under the table.

### Fonts

[Imagem mostrando a tab Fonts mostrando os checklists]

Lists every `.ttf`/`.otf` font with a checklist showing whether **Include Font Data** is enabled. Embedding the whole font file bloats the build, so for TextMesh Pro (SDF) projects you can disable it per font or all at once with a single button. Keep it enabled only for fonts used by legacy UI Text / Text Mesh.

### Build logs

[Imagem mostrando a tab Build logs]

Parses the Unity build report and lists every file in the final build with its size. Press **Analyze build logs** (the project must have been built at least once on this machine) to fill the table. Colors follow the **absolute size** — which actually drops as you optimize — and any file taking more than 6% of the whole build is also flagged for dominating it. This is where you catch forgotten Resources, uncompressed audio and oversized textures.

## License

[MIT](LICENSE.md). Based on the [unity-optimizations-package](https://github.com/CrazyGamesCom/unity-optimizations-package) by CrazyGames.
