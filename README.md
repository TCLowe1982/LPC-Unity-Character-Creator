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

- **Runtime** — a layered character on a shared LPC walk rig (9 frames × 4 directions):
  - `LpcCharacter` — stacked layer renderers driven in lockstep by `SetPose(dir, frame)`; live `SetLayer` / `RemoveLayer` (equip / appearance swap).
  - `LpcLayerSet` / `LpcRecipe` — a layer (slot + zOrder + 36 frames) and an ordered set of them (= a character).
  - `LpcCharacterBuilder` / `LpcCharacterSpawner` — build a character from a recipe, in editor or at runtime.
- **Editor** *(in progress)* — a selective importer:
  - a **manifest** of which slots/options you want (walk-only by default),
  - a **copy step** that pulls just those files from your LPC clone,
  - an **AssetPostprocessor** that auto-slices each (9×4 → 36 frames) and generates the matching `LpcLayerSet`,
  - palette **recolor** (e.g. hair color) from the LPC `palette_definitions`.

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

- [ ] Catalog importer (manifest + copy + AssetPostprocessor)
- [ ] Palette recolor (runtime palette-swap so style × color is independent)
- [ ] Editor window: point at the LPC repo, browse + pick layers
- [ ] OpenGameArt scraping for LPC assets missing from the base generator
- [ ] Samples: demo character + mini character-creation scene

## License

Package code: **MIT** (see `LICENSE`). LPC **art** you import is **CC-BY-SA / GPL / OGA-BY** —
yours to use with attribution; this package does not relicense or redistribute it.
