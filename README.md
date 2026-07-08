# OxOptimizer

<p align="center">
  <img width="1024" height="512" alt="ox_logo" src="https://github.com/user-attachments/assets/e7cde165-c185-4d68-aa88-ea9df88db431" />
</p>


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
https://github.com/OxenteGames/OxOptimizer.git#1.0.0
```

Requires Unity **2021.2+**. Editor-only — adds nothing to your builds.

## Using

Open the window from `Tools > OxOptimizer (WebGL)`. Each tab audits one part of your project — walk through them top to bottom.

### Export

<img width="997" height="756" alt="image" src="https://github.com/user-attachments/assets/efe86c2c-d5d2-466e-9bb6-a138e50038f8" />


The overview ring gives you a single optimization score at a glance. The **green** slice is how many checks already pass, the **gray** slice is the build-size grade that's still pending (it fills in once you run the **Build logs** tab), and the dark base is what's left to fix. Below it, each WebGL export setting (Brotli compression, name files as hashes, exception support, code stripping) is listed with a **Fix** button that applies the recommended value in one click.

### Memory

<img width="993" height="749" alt="image" src="https://github.com/user-attachments/assets/e5434f1c-bac8-4afb-a288-0d8845ade7c7" />


Applies the mobile-safe WebGL memory preset validated on real devices. Each setting shows its current value next to the recommended one, with a green ✔ when it already matches and a **Fix** button when it doesn't. Use **Apply mobile-safe preset** to set all of them at once. Starting with a small heap that grows in small capped steps keeps mobile browsers from killing the tab.

### Textures

<img width="997" height="738" alt="image" src="https://github.com/user-attachments/assets/38f838dd-2a3d-47fd-baef-6b07e71a1961" />


Lists every texture that ends up in the build with its import settings (max size, compression, crunch). Each value is color-coded against the ideal: **white** = ideal, **orange** = worth attention, **red** = far from ideal. The legend and the ideal target for each column are shown right under the table, so you can spot the heavy textures at a glance and click one to select it in the Project view.

### Audio

<img width="991" height="748" alt="image" src="https://github.com/user-attachments/assets/e05b4931-04d8-419f-88b3-4c820cbe9082" />


Same color-coded audit for audio clips: **Load type** (Compressed in memory / Streaming is preferred over Decompress on load) and **Quality** (lower is smaller). White/orange/red tell you how far each clip is from the recommended setting, with the ideal values listed under the table.

### Fonts

<img width="996" height="753" alt="image" src="https://github.com/user-attachments/assets/4f29fc97-584b-4ef8-8e41-53d332040101" />


Lists every `.ttf`/`.otf` font with a checklist showing whether **Include Font Data** is enabled. Embedding the whole font file bloats the build, so for TextMesh Pro (SDF) projects you can disable it per font or all at once with a single button. Keep it enabled only for fonts used by legacy UI Text / Text Mesh.

### Build logs

<img width="997" height="751" alt="image" src="https://github.com/user-attachments/assets/c867e994-1e87-4261-8f44-709645f14a52" />


Parses the Unity build report and lists every file in the final build with its size. Press **Analyze build logs** (the project must have been built at least once on this machine) to fill the table. Colors follow the **absolute size** — which actually drops as you optimize — and any file taking more than 6% of the whole build is also flagged for dominating it. This is where you catch forgotten Resources, uncompressed audio and oversized textures.

## License

[MIT](LICENSE.md). Based on the [unity-optimizations-package](https://github.com/CrazyGamesCom/unity-optimizations-package) by CrazyGames.
