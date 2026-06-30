# Art, Import, Body Types, Recolor, Coverage & Licensing

## The LPC animation set (15, measured)

Names match the on-disk PNG file names (so import == registry == playback). `framesPerDir × directions`:

| clip | grid | | clip | grid | | clip | grid |
|---|---|---|---|---|---|---|---|
| spellcast | 7×4 | | hurt | 6×1 | | sit | 3×4 |
| thrust | 8×4 | | climb | 6×1 | | emote | 3×4 |
| walk | 9×4 | | idle | 2×4 | | combat_idle | 2×4 |
| slash | 6×4 | | jump | 5×4 | | backslash | 13×4 |
| shoot | 13×4 | | run | 8×4 | | halfslash | 6×4 |

`hurt`/`climb` are single-direction (south). There is **no** `watering` or `1h_slash` body sheet (the
epic notes were wrong; the registry was reconciled to reality). All standard sheets are 64px cells;
oversize weapon sheets (128/192px) are handled by deriving the cell size, not enumerating variants.

## 21 categories + z-order

`LpcCategory.All` (back→front): shadow, backpack, cape, quiver (behind-body, negative z), body, legs,
feet, dress, torso, arms, neck, shoulders, head, eyes, facial, beards, hair, hat, tools, weapon, shield.
`DefaultZ` is the default; a `sheet_definition`'s zPos overrides it via the manifest entry's `zPos`.
Multi-layer parts = several entries on one slot at different z (the runtime orders any z, so multi-layer
and behind-body work with no runtime change). The importer reads each part's real zPos from the LPC
`sheet_definitions/*.json` (`LpcSheetDefParser` → `LpcSheetDefIndex`, source→zPos; priority: explicit
manifest `zPos` > sheet_def zPos > `LpcCategory.DefaultZ`). **There is no per-direction z in LPC** — the
"cape behind one way, in front the other" effect is two layers (`cape_solid.json`: `layer_1` fg zPos 85,
`layer_2` bg zPos 5, vs body 10) with baked per-direction art, which the multi-layer support handles.

## Body types

8 types with fallback chains (`LpcBodyType`): male, muscular(→male), female, pregnant(→female),
teen(→female→male), child, skeleton(→male), zombie(→male). A recipe carries one `bodyType`;
`LpcCharacterBuilder.ResolveLayers` picks each slot's best variant (drops a slot no variant supports).
LPC stores variants in `<part>/<bodytype>/` subfolders; the importer copies each requested type and tags
`LpcLayerSet.bodyType`.

## Recolor (palette swap)

`AppearanceSelector` (game) Color mode → `LpcRecolor.RecolorClips(layerSet.clips, ramp)` →
`LpcCharacter.SetLayerClips`. **Recolor EVERY clip**, not just walk, or the colour reverts on non-walk
animations and a seam appears at the hairline. The recolor detects the N most-common opaque source
shades, maps by luminance to the target ramp, and re-slices; textures must be readable.

## Coverage transparency (instead of restricting)

A part may lack some animations (the formal shirt has only 6/15 — no jump/climb/run/…). Policy:
**keep all animations available, surface the gaps.**
- `LpcLayerSet.SupportedClips/MissingClips`, `LpcCharacter.SlotsMissingClip(clip)` expose coverage.
- `LpcAnimationMenu` flags clips some worn part can't draw (amber `*`) and, on click, reports
  `"jump": hidden (no jump art): torso`.
- `AppearanceSelector` appends `(6/15 anims)` to an incomplete part's label.

## Selective importer (the lean path)

The full LPC tree is **~483 MB / 144,699 PNGs** — never dump it into Assets. `LpcCatalogImporter`
(Tools/LPC/Import Starter Catalog) reads a manifest selecting parts × bodyTypes × animations and copies
only those into `Catalog/`, where the postprocessor slices them. Manifest lives at
`Assets/Characters/LPC/catalog_manifest.json`; `lpcSourcePath` points at a local LPC clone (or the
extracted art bundle).

## Art bundle (drop-in distribution)

Art is shipped as a **GitHub Release artifact**, NOT in git:
- **Build:** `tools/Build_Art_Release.ps1` zips `spritesheets/` + `CREDITS.csv` + generated `CREDITS.md`
  + `LICENSE-ART.txt` into `LpcArt-full.zip` (~212 MB) using built-in `tar`. Publish:
  `gh release create art-vN <zip> -R TCLowe1982/LPC-Unity-Character-Creator …`.
- **Consume:** `Editor/LpcArtImporter` (Tools/LPC/Import Bundled Art) downloads the pinned release zip,
  extracts to `LpcArtSource/` OUTSIDE Assets (so Unity won't import 144k textures), and repoints the
  manifest. The selective importer then slices chosen parts.
- ⚠️ **The unauthenticated download works only when the repo is PUBLIC.** On a private repo the release
  asset 404s. Current release tag: `art-v1`.

## Licensing

- **Package code: MIT** (`LICENSE`).
- **Art: CC-BY-SA 3.0 / GPL 3.0 / OGA-BY 3.0** (`LICENSE-ART.txt`). All permit commercial use **with
  attribution**; CC-BY-SA/GPL add **share-alike**. Per-file authors/licenses/URLs are in the LPC
  `CREDITS.csv` (shipped in the bundle). `LpcCreditsReader` + `LpcCredits` aggregate it; the importer
  emits an attribution file for the parts you actually use. Always credit the artists.
