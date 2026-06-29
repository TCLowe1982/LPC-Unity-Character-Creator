# LPC Unity Character Creator

Use **Liberated Pixel Cup (LPC)** layered character art in Unity — import only what you
need, build animated characters at runtime from stacked layers, swap equipment/appearance
live, and recolor via palettes.

> **You supply the art.** LPC sprites are licensed **CC-BY-SA 3.0 / GPL 3.0 / OGA-BY 3.0**
> and are **not** bundled with this package. Point it at a local clone of the
> [LPC Spritesheet Character Generator](https://github.com/liberatedpixelcup/Universal-LPC-Spritesheet-Character-Generator),
> and **credit the artists** for whatever layers you use. The importer generates an
> attribution file for the layers in your manifest.

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
