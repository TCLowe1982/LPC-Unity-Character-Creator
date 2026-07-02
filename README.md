# LPC Unity Character Creator

Use **Liberated Pixel Cup (LPC)** layered character art in Unity — import only what you
need, build animated characters at runtime from stacked layers, swap equipment/appearance
live, and recolor via palettes.

> **Art is bundled — one-click import.** The full LPC art set (144,699 PNGs, all 15
> animations × 4 directions) ships as a GitHub **Release** artifact (too large for git), and
> `Tools/LPC → Import Bundled Art` downloads + extracts it. LPC sprites are
> **CC-BY-SA 3.0 / GPL 3.0 / OGA-BY 3.0** — you must **credit the artists** (per-file
> attribution is in the bundled `CREDITS.csv`; see `LICENSE-ART.txt`). Package **code** is MIT.
> You can still point at your own local LPC clone instead.

## What's in the box

- **Runtime** — code-driven layered characters (no `.anim` assets):
  - **All 15 LPC animations × 4 directions**, indexed `dir*framesPerDir + frame` per clip (`LpcClip`/
    `LpcClips`/`LpcClipMath`). `LpcCharacter` drives every layer in lockstep; live `SetLayer`/`RemoveLayer`.
  - `LpcAnimator` (gameplay: locomotion + one-shots) and `LpcClipPlayer` (preview: loop any clip).
  - **Body types** (male/female/muscular/child/skeleton…) with variant fallback (`LpcBodyType`);
    **z-order** for 21 categories (`LpcCategory`); **palette recolor** across all clips (`LpcRecolor`).
  - `LpcLayerSet`/`LpcRecipe`/`LpcCharacterBuilder`/`LpcCharacterSpawner` — data → built character.
- **Editor** — selective importer + auto-slicer: a **manifest** picks slots/body-types/animations, a
  **copy step** pulls just those from your LPC source, an **AssetPostprocessor** slices each animation by
  its grid into `LpcLayerSet.clips`, and an **attribution file** is generated from the LPC `CREDITS.csv`.
- **Samples** — `LpcAnimationPreview`: a self-populating animation preview panel that flags incomplete
  coverage. A package-dev tool: hidden by default (`startHidden`); open it with `Show()`/`Toggle()`.
  `LpcDemoCreator` + **Tools/LPC/Create Demo Scene**: generates a mini character-creation scene from
  the imported catalog (slot cycling, body type, hair recolor, live animation preview).

## Why selective import

The full LPC package is tens of thousands of files (≈20 animations × hundreds of layers ×
variants). Dumping it all into Unity chokes the import pipeline. This package imports
**only the layers in your manifest, walk-only** — so it stays trivial, and adding an option
is a one-line edit.

## Art bundle (drop-in)

The whole LPC spritesheet tree is **≈483 MB / 144,699 files** — too much for a git repo — so
it's distributed as a GitHub **Release** artifact (`LpcArt-full.zip`, ~212 MB) and pulled on
demand:

1. **`Tools/LPC → Import Bundled Art`** downloads the Release zip and extracts it to
   `LpcArtSource/` **outside `Assets/`** (so Unity doesn't try to import 144k textures), and
   points the catalog manifest at it.
2. **`Tools/LPC → Import Starter Catalog`** (or your own manifest) then slices **only the
   parts you select** into `Assets/Characters/LPC/Catalog/` — keeping your project lean.

Attribution travels with the art: `CREDITS.csv` (per-file authors / licenses / URLs) and
`LICENSE-ART.txt` are inside the zip. Maintainers rebuild the artifact with
`tools/Build_Art_Release.ps1`.

> The Release download is public only when the **repository is public**. On a private repo
> the unauthenticated importer download will 404.

## Install

Add via Unity Package Manager → *Add package from git URL* (once published), or reference a
local clone in `Packages/manifest.json`:

```json
"com.tclowe.lpc-character-creator": "file:D:/Projects/LPC-Unity-Character-Creator"
```

## Roadmap

- [x] Catalog importer (manifest + copy + AssetPostprocessor) + per-animation slicing
- [x] Palette recolor across all animations (style × color independent)
- [x] Body-type system; full 21-category coverage + z-order; attribution from `CREDITS.csv`
- [x] All 15 LPC animations × 4 directions; animation preview/selector menu
- [x] Bundled art via GitHub Release + one-click `Import Bundled Art`
- [ ] Editor window: point at the LPC repo, browse + pick layers
- [ ] OpenGameArt scraping for LPC assets missing from the base generator
- [ ] Oversize pivot offsets + per-direction z-order (cape flip)

## License

Package code: **MIT** (see `LICENSE`). LPC **art** you import is **CC-BY-SA / GPL / OGA-BY** —
yours to use with attribution; this package does not relicense or redistribute it.
