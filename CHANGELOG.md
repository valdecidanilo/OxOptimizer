# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.0.0] - 2026-07-07

### Added

- Initial release of **OxOptimizer**.
- **Export** tab: audits WebGL export settings (Brotli compression, name files as hashes,
  exception support, engine code stripping, preloaded shaders) with one-click fixes.
- One-click strip of URP post-processing shaders (~1 MB) when the project doesn't use
  post-processing, with a confirmation dialog that warns if Volume Profiles exist in the
  project. Works on both the 3D and 2D URP renderers, no URP assembly dependency.
- **Memory** tab: mobile-safe WebGL memory preset validated on real devices
  (Initial 128 MB, Maximum 768 MB, Geometric growth, step 0.2, cap 32 MB), with per-setting
  status, one-click fixes, and an "apply all" button.
- **Textures**, **Models** and **Audio clips** tabs: audit the import settings of every
  asset that ends up in the build.
- **Fonts** tab: lists every .ttf/.otf in the project with its size and "Include Font Data"
  state, with per-font and disable-all fixes (safe for TextMesh Pro projects, where the SDF
  atlas is used instead of the embedded font).
- **Build logs** tab: parses the Unity build log and shows which assets contribute most
  to the build size.
- `OxGui` UI kit: OxenteGames-branded header with logo, accent separator, ✔/✖ audit badges,
  and cached GUIStyles shared by all tabs.
- Localization: English (default) and Português (BR), switchable from the header dropdown
  and persisted per machine via EditorPrefs.
- UPM package layout (`package.json`, Editor-only assembly definition) installable via git URL.
