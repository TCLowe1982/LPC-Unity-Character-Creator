# Architecture

## Data flow (source art → animated character)

```
LPC source PNGs                 (spritesheets/<cat>/<part>/<bodytype>/<anim>.png)
  └─ LpcCatalogImporter         (Editor menu) copies SELECTED parts × bodytypes × anims
       → Catalog/<slot>/<id>[__<anim>].png  +  catalog_index.json  +  CREDITS.txt
  └─ LpcCatalogPostprocessor    (AssetPostprocessor, automatic on import)
       → slices each PNG via LpcSliceMath        (grid from LpcClips + PNG size)
       → builds LpcLayerSet assets (clips[] per animation, bodyType, zOrder)
  └─ LpcRecipe (LpcLayerSet[] + bodyType)
       └─ LpcCharacterBuilder.Build(recipe, go)  → LpcCharacter (one SpriteRenderer per slot)
            └─ LpcAnimator / LpcClipPlayer  drive SetPose(dir, frame) each tick
```

## Runtime (`Runtime/`, MIT, asmdef `TCLowe.Lpc.Runtime`)

**Clip system (pure + data):**
- `LpcClip.cs` — `LpcClip` (name, framesPerDir, directions, fps, loop, frameSize); `LpcClips` (the
  **15** canonical ULPC animations, names == on-disk PNG file names: spellcast, thrust, walk, slash,
  shoot, hurt, climb, idle, jump, run, sit, emote, combat_idle, backslash, halfslash; layouts measured
  from the generator); `LpcClipFrames` (clip name → Sprite[], with `Resolve` + legacy-walk fallback).
- `LpcClipMath.cs` — pure: `PoseIndex` (`dir*framesPerDir+frame`, clamped, 1-dir clips force dir 0),
  `CycleFrame` (walk/run, skips standing frame 0), `LoopFrame`, `OneShotComplete`, non-negative `Mod`.
- `LpcSliceMath.cs` — pure: derive cell size from sheet dims vs grid (`TryCellSize`), `Slice` (bottom-up
  rects, index = dir*cols+frame), `IsOversize`. Oversize handled by derivation, not enumeration.

**Character + playback:**
- `LpcCharacter.cs` — `Layer` (slot, zOrder, renderer, `clips`, legacy `frames`, cached active);
  `Play(clip)`, `SetPose(dir,frame)` (**hides** a layer with no frames for the clip), `SetLayer`/
  `SetLayerFrames`/`SetLayerClips`/`RemoveLayer`, `HasClip`, `SlotsMissingClip` (coverage).
- `LpcAnimator.cs` — gameplay driver: locomotion (walk-while-moving / idle-or-stand) + `PlayOnce`
  one-shot queue. Reads `ILpcMotion` (Facing/Walking) from self/parent, or `facing`/`walking` fields.
  Deterministic `Tick(float dt)` (Update calls it); lazy `EnsureInit` so it works pre-play-lifecycle.
- `LpcClipPlayer.cs` — PREVIEW driver: loops any chosen clip (even play-once ones) for a mirror/browser.
  Also has `Tick(dt)`.

**Data + build:**
- `LpcLayerSet.cs` — ScriptableObject: slot, bodyType, zOrder, `clips` (LpcClipFrames[]), legacy
  `frames`; `FramesFor`, `SupportedClips`, `MissingClips`.
- `LpcRecipe.cs` — bodyType + `LpcLayerSet[]` pool. `LpcCharacterBuilder.cs` — `ResolveLayers` (group by
  slot, pick best body-type variant, drop unsupported, order by zOrder) + `Build`. `LpcCharacterSpawner`
  builds from a recipe at Awake.
- `LpcBodyType.cs` — pure: 8 types + per-type fallback chains (muscular→male, pregnant→female,
  teen→female→male, …) + `Resolve`/`Supports`/`Normalize` (null/empty == male).
- `LpcCategory.cs` — pure: 21 categories + `DefaultZ` back-to-front (behind-body negatives:
  shadow/backpack/cape/quiver) + `IsBehindBody`.
- `LpcRecolor.cs` — palette swap: `RecolorFrames` (recolour one sheet texture, re-slice), `RecolorClips`
  (recolour EVERY clip — use this), `RecolorTexture` (cached). Needs readable textures (postprocessor
  marks catalog textures readable).
- `LpcPalette.cs` — ramp asset. `LpcCredits.cs` — pure: `LpcCreditEntry` + aggregate/`Format` (also
  usable for an in-game credits screen).

## Editor (`Editor/`, asmdef `TCLowe.Lpc.Editor` → references Runtime)

- `LpcCatalogImporter.cs` — reads a manifest (lpcSourcePath, bodyTypes, animations, entries[slot+source
  +zPos+credit overrides]); copies selected parts; per-body-type `<bodytype>/` subfolders (legacy
  fallback); z from `entry.zPos` else `LpcCategory.DefaultZ`; writes `catalog_index.json` + `CREDITS.txt`
  via `LpcCreditsReader`/`LpcCredits`.
- `LpcCatalogPostprocessor.cs` — `AssetPostprocessor`: slices each catalog PNG per its animation grid
  (`LpcSliceMath`, fixed-64 fallback), assembles ALL animations into `LpcLayerSet.clips`, stamps
  bodyType + zOrder.
- `LpcCreditsReader.cs` — parses the LPC `CREDITS.csv` (quote-aware, header-detected columns), matches
  rows under a part, merges manifest overrides.
- `LpcPaletteImporter.cs` — bakes `LpcPalette` from `palette_definitions`.
- `LpcArtImporter.cs` — `Tools/LPC/Import Bundled Art`: downloads the Release zip, extracts to
  `LpcArtSource/` OUTSIDE Assets, repoints the manifest.

## Samples (`Samples/`, asmdef `TCLowe.Lpc.Samples` → Runtime + UnityEngine.UI)

- `LpcAnimationPreview.cs` (was LpcAnimationMenu; hidden by default, `startHidden`) — builds a button per available clip on a target character; flags clips some
  worn part can't draw (amber `*`) and reports hidden parts on click; auto-rebuilds when coverage
  changes. Pairs with `LpcClipPlayer`.

## The offline-test pattern (why pure statics matter)

`Tests~/LpcLogic.Offline.csproj` links the pure Runtime files (`LpcClip`, `LpcClipMath`, `LpcSliceMath`,
`LpcBodyType`, `LpcCategory`, `LpcCredits`) + a minimal `Tests~/Shim/UnityEngine.cs` (just `Mathf`,
`Sprite`, `[Tooltip]`) and the shared `Tests/*.cs`, so they run under `dotnet test` with no editor.
**Anything MonoBehaviour/asset-bound can't go offline** — it lives in `Tests/Integration/` (Unity-only).
So: extract the arithmetic into a Runtime static, link it offline, and keep the glue in Integration.
