# Architecture — LPC Unity Character Creator

A layered LPC character is a stack of child `SpriteRenderer`s driven to the same `(clip, dir,
frame)` each tick. Animations are **code-driven — no `.anim` assets**. Bug-prone arithmetic lives
in pure Runtime statics that unit-test offline. Sibling docs: `art-pipeline-and-licensing.md`
(source art, bundle, credits), `workflow-and-conventions.md` (beads, MCP bridge, gotchas).

## Data flow

```text
LPC source PNGs                 spritesheets/<cat>/<part>/<bodytype>/<anim>.png
  └─ LpcCatalogImporter         copies SELECTED parts × bodytypes × anims
       → Catalog/<slot>/<vid>[__<anim>].png + catalog_index.json + CREDITS.txt
  └─ LpcCatalogPostprocessor    (AssetPostprocessor, automatic)
       → slices each PNG on its clip's grid → LpcLayerSet assets in <root>/LayerSets/
  └─ LpcRecipe (bodyType + LpcLayerSet[] pool)
       └─ LpcCharacterBuilder.Build(recipe, go) → LpcCharacter (one SpriteRenderer per slot)
            └─ LpcAnimator (gameplay) / LpcClipPlayer (preview) → SetPose(dir, frame) per tick
```

## Runtime clip system (`Runtime/`, asmdef `TCLowe.Lpc.Runtime`, namespace `Lpc`)

### LpcClip.cs — layouts and the 15-clip registry

`LpcClip` (serializable struct): `name`, `framesPerDir`, `directions` (4 = N/W/S/E rows, 1 =
single-direction), `fps`, `loop`, `frameSize` (64 standard; oversize larger). `TotalFrames =
framesPerDir * max(1, directions)`; `IsValid` = non-empty name && framesPerDir > 0. Every animation
has its **own grid** — never assume a 36-frame walk layout.

`LpcClips`: `Cell = 64`; `All` = exactly 15 clips in ULPC sheet order, names equal the on-disk ULPC
PNG file names ("watering" is a tool overlay, deliberately excluded):

| clip | fpd×dirs | fps | loop | clip | fpd×dirs | fps | loop |
| --- | --- | --- | --- | --- | --- | --- | --- |
| spellcast | 7×4 | 8 | no | jump | 5×4 | 8 | no |
| thrust | 8×4 | 8 | no | run | 8×4 | 12 | yes |
| walk | 9×4 | 8 | yes | sit | 3×4 | 4 | yes |
| slash | 6×4 | 12 | no | emote | 3×4 | 6 | no |
| shoot | 13×4 | 8 | no | combat_idle | 2×4 | 6 | yes |
| hurt | 6×1 (south only) | 8 | no | backslash | 13×4 | 12 | no |
| climb | 6×1 | 8 | yes | halfslash | 6×4 | 12 | no |
| idle | 2×4 | 2 | yes | | | | |

`Get(name)` is case-sensitive and falls back to **Walk** for unknown/null; `TryGet` returns false
(clip = Walk) for null/empty.

`LpcClipFrames`: `clip` name + `frames` (Sprite[], flat index `dir*framesPerDir + frame`); a layer
carries one per supported animation. Static `Resolve(clips, legacyWalk, clipName)` returns the
first matching non-empty entry; falls back to the legacy flat walk array **only when clipName ==
"walk"** (pre-2g8.8 imports); returns **null** when no frames exist. Null/empty entries are
skipped (an empty "walk" entry doesn't shadow the legacy fallback).
`ResolveWithFallback(clips, legacyWalk, clipName, out usedFallback)` layers the stand-in rule on
top: a missing clip resolves to the layer's **walk** frames (`usedFallback = true`, pose on walk's
grid at frame 0 — the standing pose) so partial ULPC coverage (a longsword with combat sheets
only) doesn't pop equipment in and out; null only when walk is missing too — the hide signal.

### LpcCharacter.cs

MonoBehaviour; `const Directions = 4` (0=up, 1=left, 2=down, 3=right). `Layer[] layers`,
`baseSortingOrder = 100`; defaults curClip=Walk, curDir=2 (down), curFrame=0; `CurrentClip`
exposed. Nested `Layer`: `name` (slot), `zOrder` (higher = front), `renderer`, `clips`, legacy
`frames`, plus a NonSerialized per-clip resolve cache (`Activate` memoizes, `Invalidate` clears).

- `Play(clip | name)` — no-op if invalid; activates every layer, re-applies the pose.
- `SetPose(dir, frame)` — `LpcClipMath.PoseIndex` clamps and persists dir/frame; a layer with no
  frames for the clip **holds walk frame 0 of the requested direction** (the standing pose, via
  `ResolveWithFallback` — `Layer.ActiveIsFallback` marks the cache) so equipment doesn't pop as
  the character starts/stops; a layer lacking walk too gets **null** — hidden, never a stale
  pose (the core invariant).
- `SetLayer(LpcLayerSet)` — replace in place by slot, or create child `"LPC_"+slot` with a
  SpriteRenderer; then `ReSort()` + re-pose.
- `SetLayerFrames(slot, frames)` — replaces the **active clip only** (legacy `frames` if active is
  walk and no per-clip entry matched). `SetLayerClips(slot, clips, legacyWalk=null)` — replaces
  **all** clips; recolor must use this so color holds on every animation, not just walk.
- `RemoveLayer(slot)` — destroys the renderer GameObject (Destroy in play, DestroyImmediate else).
- Coverage: `HasClip(clip)` (any layer has frames), `SlotsMissingClip(clip)` (slots with no frames
  for the clip — they hold the walk standing frame, or hide if walk is missing too).
- `ReSort()` — sorts by zOrder ascending, `sortingOrder = baseSortingOrder + i`.

### LpcAnimator.cs — gameplay driver

`[RequireComponent(typeof(LpcCharacter))]`. Locomotion + one-shot queue. Motion source: any
`ILpcMotion` (`Vector2Int Facing`, `bool Walking` — the decoupling seam from movement code) on self
or parent via `GetComponentInParent`, else public fallback fields `facing` (default `(0,-1)` down)
and `walking`. `DirRow`: y>0→0, y<0→2, x<0→1, x>0→3, zero→2. Fields: `speedScale=1f`,
`walkClipName="walk"`, `idleClipName="idle"`. `EnsureInit()` is idempotent and works outside play
mode (EditMode tests tick without Awake). `Update()` calls public `Tick(dt)`; `t` accumulates in
frame units (`t += dt * active.fps * speedScale`, `fr = FloorToInt(t)`).

- `PlayOnce(clip, queueIt=false)` silently no-ops if invalid / no character / `!HasClip`. queueIt
  chains one-shots while one plays; completion via `OneShotComplete`; the queue chains with no
  locomotion frame in between; when done it falls through to locomotion in the same Tick. `Stop()`
  aborts one-shot + queue.
- Locomotion: moving → walk clip with `CycleFrame` (skips standing frame 0); idle → idle clip with
  `LoopFrame`; neither has frames → stand on walk frame 0 — a walk-only import behaves exactly like
  a walk-or-stand rig, richer clips light up as their frames appear.
- `SetLocomotionClip(name)` swaps the moving clip live (walk↔run), idle unchanged; immediate unless
  a one-shot is playing. `SetActive` only resets `t`/`Play`s when the clip name actually changes.

### LpcClipPlayer.cs — preview driver

Same RequireComponent. Loops ANY clip — including play-once slash/hurt — for mirrors/browsers,
never gameplay. `speedScale=1f`, `direction=2` (down). `Play` no-ops without frames; starts at
frame 0. `Tick` clamps fps to min **0.01f** (LpcAnimator doesn't) and always uses `LoopFrame`.
`SetDirection` re-poses at the current frame without restarting. `Current` (empty name when
stopped) and `IsPlaying` exposed.

### Recolor: LpcRecolor.cs + LpcPalette.cs

`LpcRecolor` (static): detects the source ramp as the most-common opaque colors (alpha < 16 =
skip/pass-through), sorts detected shades and the target ramp by BT.601 luminance
(0.299r+0.587g+0.114b), maps each opaque pixel to its nearest shade (squared RGB distance) and
replaces RGB from the target, preserving alpha — anti-aliased edges snap to the nearest shade. A
layer's frames share one texture, so `RecolorTexture` recolors once (RGBA32, Point filter) and
caches by `src.GetInstanceID()*397 ^ RampKey(target)` (never evicted); unreadable textures warn and
return null → callers fall back to the originals. `RecolorFrames` makes fresh sprites
(`<name>_recolor`, FullRect); `RecolorClips` maps every clip — **always RecolorClips +
SetLayerClips**, or hair keeps its original colour on every non-walk animation. Short target ramps
are padded with their lightest color.

`LpcPalette` (ScriptableObject, menu `LPC/Palette`): `category` (default "hair") + `Ramp[]`
(`name`, `Color[] colors` **dark→light** — the order LpcRecolor assumes). `Names()`, `Get(name)`
(exact match or null), `GetAt(i)` (true-modulo wrap). Design: N styles × M ramps as independent
dimensions, never N×M baked variants.

### LpcCredits.cs

Pure. `LpcCreditEntry`: `part`, `authors[]`, `licenses[]`, `urls[]`, `notes`. `UniqueAuthors` /
`UniqueLicenses` dedupe case-insensitively, keep first-seen order/casing, trim, tolerate nulls.
`Format(entries, title=null)` renders the attribution document (default title "LPC ART ATTRIBUTION
(auto-generated…)", boilerplate naming CC-BY-SA 3.0 / GPL 3.0 / OGA-BY 3.0, `Licenses:`/`Authors:`
aggregate lines with "(none recorded)" fallbacks, then `Parts (<count>):` with per-part detail
lines). Reusable for an in-game credits screen.

## Builder / recipe / body types

- **LpcLayerSet** (ScriptableObject, menu `LPC/Layer Set`): `slot` (default "body"), `bodyType`
  (default male — one LayerSet per supported body type), `zOrder`, `clips`, legacy `frames`
  (36 = 9×4 walk). `FramesFor` via Resolve; `SupportedClips()`/`MissingClips()` against
  `LpcClips.All` in registry order.
- **LpcRecipe** (ScriptableObject, menu `LPC/Recipe`): `bodyType` + `layers` pool (may hold several
  body-type variants per slot) + `colors` (`SlotColor[]`: slot → target ramp; `RampFor(slot)`
  returns the ramp or null, skipping empty entries). A character is data; swapping layers
  re-skins it, and the builder recolors each slot with an entry across **all** clips + the legacy
  walk array (`LpcRecolor.RecolorClips`/`RecolorFrames`) so recipe-built NPCs keep their palette
  on every animation.
- **LpcCharacterBuilder** (static, editor + runtime). `ResolveLayers(recipe)`: normalize body type,
  group pool by slot (first-seen slot order), pick each slot's variant via `LpcBodyType.Resolve`
  over the variants' normalized types (first variant matching the pick wins), **drop unsupported
  slots**, order by zOrder then ordinal slot name — public and stateless so UI can preview
  resolution without building. `Build(recipe, target, baseSortingOrder=100)`: destroy stale direct
  children named `LPC_*`, get-or-add LpcCharacter, create one `"LPC_"+slot` child SpriteRenderer
  per resolved set (`sortingOrder = base + i`), assign layers, then `Play(Walk)` + `SetPose(2, 0)`
  (face down, standing). Returns the LpcCharacter; null on null args.
- **LpcCharacterSpawner**: builds from `recipe` in Awake (base 100); null recipe = silent no-op;
  runtime swap = call Build again.
- **LpcBodyType** (pure): `All` = { male, muscular, female, pregnant, teen, child, skeleton,
  zombie }. `Any = "any"` is a body-agnostic **variant tag** (adult hair, hats, most weapons) —
  never in All, never requestable, lower priority than a real match. `Normalize(null/empty) =
  male`. Fallback chains (directional — female never falls back to male art): muscular→male,
  pregnant→female, teen→female→male, skeleton→male, zombie→male; male/female/child chain to
  themselves; unknown types get a single-element chain. `Resolve(requested, available)` walks the
  chain, then tries Any, else null; `Supports` = Resolve != null. Types are plain strings matching
  the generator's sheet_definitions keys, so the set stays open.

## Pure-math statics (the offline-testable core)

- **LpcClipMath** — only Unity dependency is `Mathf` (shimmed offline); helpers take a
  pre-floored integer step. `PoseIndex(clip, dir, frame, out d, out f)` clamps to the layout
  (1-direction clips force dir 0) and returns `dir*framesPerDir + frame`. `CycleFrame(fpd, step) =
  1 + Mod(step, fpd-1)` — walk/run never show the standing contact frame 0 mid-stride.
  `LoopFrame(fpd, step) = Mod(step, fpd)` (full range incl. 0).
  `OneShotComplete(fpd, step) = step >= fpd`. `Mod` is non-negative (m<=0 → 0) so negative steps
  wrap cleanly.
- **LpcSliceMath** (2g8.8) — `LpcCell`: pixel rect with **bottom-up y** (row 0 = TOP image row →
  highest y), dir/frame, `index = dir*cols + frame`. `BaseCell = 64`. `TryCellSize` **derives**
  cell size (`width/cols × height/rows`) so 128/192px oversize sheets slice automatically; false on
  non-positive input or uneven division (caller falls back to a fixed grid rather than slicing
  garbage). `Slice` fills row-major, `y = sheetH - (r+1)*cellH`; `TrySlice` combines both.
  `IsOversize` = either dimension > 64. `PivotY(cellH)` (2g8.14): the generator centers the 64px
  body in an oversize cell with offset `(cell-64)/2`, so `PivotY = cellH > 64 ?
  (cellH-64)/(2*cellH) : 0` (128→0.25, 192→1/3); pivot X is always 0.5. These pivots keep 64px and
  oversize layers aligned at one transform position.
- **LpcCategory** — the 21 canonical categories + default z (lower = further back); the runtime
  itself is slot-string-agnostic. `DefaultZTable`: shadow=-100, backpack=-20, cape=-15, quiver=-10,
  body=10, legs=20, feet=30, dress=35, torso=40, arms=45, neck=50, shoulders=55, head=60, eyes=64,
  facial=66, beards=68, hair=72, hat=80, tools=90, weapon=100, shield=100. Behind-body categories
  are negative; gaps are intentional room for sheet_definition overrides; unknown/null → 100
  (frontmost). `IsBehindBody` = z < 10 (shadow/backpack/cape/quiver only). Per-direction z (cape
  flip) is tracked elsewhere.
- **LpcCustomAnims** — ULPC `custom_animation` layouts. Grid-compatible variants import AS their
  base clip: walk_128→walk, slash_128/slash_oversize→slash, thrust_128/thrust_oversize→thrust,
  halfslash_128→halfslash, backslash_128→backslash. `BaseClip` returns **null** for remixed
  sequences (slash_reverse_oversize, tool_whip, tool_rod, wheelchair) — decision od3: skip rather
  than silently mis-slice. `IsGridCompatible` = BaseClip != null.
- **LpcPreviewMath** (2g8.22) — keeps the Catalog Window a thin shell. `FrameUV(cols, rows, dir,
  frame)` → GL-convention UV rect `(frame/cols, (rows-1-dir)/rows, 1/cols, 1/rows)`, clamped
  (row 0 = top = highest v). `DestRect(cellW, cellH, scale, anchorX, anchorY)` → y-down GUI rect
  anchoring the pivot (bottom-center, or oversize baseline via PivotY) at one shared point — the
  same composition rule as the runtime. `FrameAt(time, fps, fpd) = (int)(time*fps) % fpd`, 0 on
  degenerate input.
- **LpcSourceLayout** — pure grouping of relative PNG paths into `LpcPartOption` (`category` =
  first segment; `source` = part path WITHOUT body-type/animation segments; `bodyTypes`, empty =
  body-agnostic; `animations`). The body type is the second-to-last segment iff
  `LpcBodyType.IsKnown` ("adult" stays part of the source). `GroupParts` dedupes by source in
  first-seen order, skipping non-.png/short/empty paths.

## Editor import pipeline (`Editor/`, namespace `Lpc.Editor`)

### LpcCatalogImporter — manifest → copy

`Tools/LPC/Import Starter Catalog` → `Import("Assets/Characters/LPC/catalog_manifest.json")`.
Selective and walk-first: the LPC tree is ~144k files; only manifest entries × listed animations
are copied. Manifest: `lpcSourcePath` (must contain `spritesheets/`), `destFolder` (default
`Assets/Characters/LPC/Catalog`), `bodyTypes` (falls back to legacy single `bodyType`,
normalized), `animations` (default `["walk"]`), `entries[]` = `{slot, source, variant, zPos
(int.MinValue = unset), authors/licenses/urls/notes}` (credit overrides).

Once per run it builds `LpcSheetDefIndex.BuildZIndex` and `BuildDefIndex` and calls
`LpcCreditsReader.ResetCache()`. **z priority: explicit entry zPos > sheet_definition zPos >
`LpcCategory.DefaultZ(slot)`.** Entries whose def `NeedsLayerExpansion` route to `ImportDefLayers`;
otherwise the plain flow imports each requested body type's `<bodytype>/` subfolder (no subfolders
= legacy source, imported once tagged `LpcBodyType.Any`). **File naming: walk → `<vid>.png`, other
anims → `<vid>__<anim>.png`** (double underscore); `vid = Sanitize(source)` ('/'→'_') plus
`_<bodytype>` unless legacy. `FindAnimSheet(dir, anim, variant)` probes `dir/<anim>.png` →
`dir/<anim>/<variant>.png` → first PNG in the anim folder (deterministic ordinal sort). Variants
with zero files warn and get no index entry. Output: `catalog_index.json` (entries of {slot, id,
bodyType, zOrder, source, animations, files}) + `CREDITS.txt` (one deduped credit per source via
`LpcCreditsReader.ReadFor` + `LpcCredits.Format`), then `AssetDatabase.Refresh()`.

**Def-driven multi-layer expansion** (`ImportDefLayers`): each sheet_definition layer becomes its
own catalog entry at the LAYER's own zPos, so a weapon's fg/behind-body/oversize-attack sheets and
a cape's fg/bg each draw at correct depth. Secondary layers get **sub-slot names `slot_l2`,
`slot_l3`…** (e.g. `weapon_l2`) because the runtime keys layers by slot; a manifest zPos overrides
only the primary layer. Variant pick: manifest `variant` > `def.variants[0]` > last path segment.
Body types group by `layer.PathFor(bt)`: all resolving to one path collapses to `Any`; partial
coverage keeps per-body tags; zero paths falls back to `layer.sources[0]` as Any. Custom-animation
layers resolve `LpcCustomAnims.BaseClip`: null → skipped with a log; base clip not in the
manifest's animations → skipped; else the single whole sheet (`FindCustomSheet`: `dir/<variant>.png`
→ flat `dir.png` → first PNG) is copied as `<vid>__<baseClip>.png`. Oversize attack sheets only
enter via def layers, never `FindAnimSheet`.

### sheet_definitions parsing

`LpcSheetDefParser` uses regex, NOT JsonUtility (dynamic `layer_N` / body-type keys): matches each
flat `"layer_N": {…}` block, extracts (possibly negative) `zPos` (default 0), and harvests
key→path pairs — `zPos` key skipped, `custom_animation` captured on the layer rather than as a
source, paths normalized/deduped into `sources` + ordered `typedSources`
(`PathFor(bodyType)` scans them; null when unlisted). Also extracts `name` and `variants` (order
preserved). `Parse(null/empty)` returns an empty non-null def. `NeedsLayerExpansion` =
layers.Count > 1 OR any layer has a customAnimation. Embedded `credits` blocks are deliberately not
parsed — attribution comes from CREDITS.csv. Pure string logic, offline-tested with synthetic JSON.

`LpcSheetDefIndex` enumerates `sheet_definitions/**/*.json` (per-file errors silently skipped).
`BuildZIndex` maps each layer source → zPos keyed by BOTH the full path (`neck/capeclip/male`) and,
when the last segment is a known body type, the stripped path (`neck/capeclip`) — the importer's
source strings omit the body segment. BuildZIndex: last writer wins; `BuildDefIndex` (source →
owning def): first wins.

### LpcCatalogPostprocessor — slicing + LayerSet generation (2g8.3)

A texture is a catalog texture when its **grandparent** directory contains `catalog_index.json`
(slot = parent folder name). `OnPreprocessTexture` then configures: Sprite/Multiple,
`PixelsPerUnit = 32`, Point filter, Uncompressed, no mipmaps, alphaIsTransparency, **isReadable =
true** (runtime recolor uses GetPixels32). PNG size is read from the IHDR chunk directly
(big-endian uint32 at offsets 16/20). The animation comes from the filename: substring after the
LAST `__`, else "walk". Grid: `LpcClips.TryGet(anim)` gives cols/rows; `LpcSliceMath.TrySlice`
derives cell size (oversize sheets slice correctly); on failure, fixed 64px grid (`FrameSize =
64`). Each sprite: name `<baseName>_<index>`, custom alignment, **pivot `(0.5,
LpcSliceMath.PivotY(cellH))`** — feet for standard cells, embedded body baseline for oversize.

`OnPostprocessAllAssets` (re-entrancy-guarded by a `_busy` flag) collects touched catalog roots
and runs `GenerateLayerSets(root)`: per catalog_index entry, each file's sprites load via
`LoadOrderedSprites` (sorted by trailing `_<n>` index so frame order is 0,1,2… regardless of
Unity's sub-asset order; unsliced files warn and skip). Each file becomes an `LpcClipFrames`
(clip name from `entry.animations[i]`, default "walk"). Assets go to
`<root>/LayerSets/<slot>_<id>.asset`, updated in place when present (slot/bodyType/zOrder/clips
overwritten + SetDirty) else created; bodyType defaults to Any. **Critical invariant: legacy
`frames` is filled ONLY from a clip literally named "walk"** — filling it with, say, a slash-only
oversize weapon's frames would make Resolve play that clip AS walk instead of hiding the layer.

### Supporting importers

- **LpcArtImporter** (`Tools/LPC/Import Bundled Art`): after a size-warning dialog, downloads the
  pinned Release zip (`…/releases/download/art-v1/LpcArt-full.zip`, ~483 MB / ~144k PNGs — too big
  for git and for Assets/) to temp (TLS 1.2/1.3 forced), extracts to **`LpcArtSource/` beside
  Assets/** (never imported by Unity), then `PointManifestAt` rewrites the manifest's
  `lpcSourcePath` (silently skipped if absent or not ours). Failures are caught + dialogged;
  progress bar always cleared. Licensing: CC-BY-SA 3.0 / GPL 3.0 / OGA-BY 3.0 — see
  `art-pipeline-and-licensing.md`.
- **LpcCreditsReader**: best-effort CREDITS.csv reader (probes CREDITS.csv/credits.csv at root and
  under spritesheets/). Header-detected columns (substring match: file/path/filename, author,
  "licen", url/link, note; missing path column → column 0), hand-rolled quote-aware RFC4180-ish
  tokenizer. Rows under a part's normalized source are unioned (split on `,`/`;`); manifest
  overrides **append**, never replace; everything deduped case-insensitively. Unresolved parts
  degrade to a "credits not found in CREDITS.csv" note — an import never fails for want of credits.
  CSV cached per root; `ResetCache()` at the start of each run.
- **LpcPaletteImporter** (2g8.4): `Tools/LPC/Import Hair Palette` bakes
  `palette_definitions/hair/hair_lpcr.json` → `Assets/Characters/LPC/Palettes/Hair.asset`;
  `Import(category, relFile, destAsset)` is general-purpose. Regex-parses
  `"name": ["#rrggbb", …]` (typically 6 shades, not enforced), drops zero-color ramps, updates an
  existing asset in place. Reads `lpcSourcePath` from the same manifest.

## Catalog Window + live preview (2g8.5 / 2g8.22)

`LpcCatalogWindow` (`Tools/LPC/Catalog Window`): point-and-pick manifest builder. Set the LPC
source (`LpcSourceScanner.ResolveSpritesheets` accepts a repo root or the spritesheets folder
itself), toggle body types (`LpcBodyType.All`; default male) and animations (`LpcClips.All`;
default walk; "All"/"Walk only" shortcuts), expand categories (`LpcCategory.All`). Categories
**scan lazily on foldout expand** (`LpcSourceScanner.ScanCategory` enumerates only that category's
PNGs recursively and delegates grouping to pure `LpcSourceLayout.GroupParts`) precisely to avoid
walking ~144k files; results cache per category. "Write Manifest & Import" writes
`Assets/Characters/LPC/catalog_manifest.json` (Entry slot = the part's category) and runs
`LpcCatalogImporter.Import` — disabled unless parts, body types, and animations are all non-empty.

Live preview: every selected part's frame layered by zPos, all pivots anchored at one point — the
same composition rule the runtime uses — drawn straight from the source sheets **before anything is
imported**. `SelectionByZ` sorts by `BuildZIndex` zPos (fallback `LpcCategory.DefaultZ`, ordinal
tiebreak); `ResolveSheet` applies `LpcBodyType.Resolve` (falling back to the part's first variant)
and reuses `LpcCatalogImporter.FindAnimSheet`; parts with no art for the previewed clip simply skip
(hide). Frames come from `LpcPreviewMath.FrameAt/FrameUV/DestRect` at the clip's own fps; an
`EditorApplication.update` tick repaints only when the frame index advances. Single-direction clips
always draw dir 0. Preview textures load with `HideAndDontSave` and are cached + manually destroyed
(`ClearTexCache`) to avoid leaks.

## Demo scene builder + Samples (`Samples/`, namespace `Lpc.Samples`)

- **LpcDemoSceneBuilder** (`Tools/LPC/Create Demo Scene`, 2g8.12): the demo scene is *built*, not
  shipped, because the package carries no art; it requires an imported catalog (dialog otherwise).
  `CollectParts` scans all of Assets for `catalog_index.json` files and groups entries by
  `PartKey(source)` (normalized; truncated at `/attack_`, `/universal`, `/foreground`,
  `/background`, `/behind`) so a multi-layer weapon's LayerSets equip as **one** part; the shortest
  slot names the part (sub-slots look like `weapon_l4`); LayerSets load from
  `<root>/LayerSets/<slot>_<id>.asset`; partless entries and setless parts are dropped; body types
  dedupe in encounter order (default male). Builds a fresh scene at
  `Assets/LpcDemo/LpcCharacterCreationDemo.unity`: orthographic camera (size 2.2), an
  "LpcDemoCharacter" root, a 1280×720 overlay canvas + EventSystem, a left 300px "CreationPanel"
  with `LpcDemoCreator`, a right 240px "Preview" with `LpcAnimationPreview` (`startHidden = false`)
  wired to an added `LpcClipPlayer`.
- **LpcDemoCreator**: self-building uGUI panel (built in `Start()`, no prefab art) — a body-type
  cycle row, one cycle row per distinct slot (every slot except literal "body" can cycle to
  "(none)"), and a hair-color row with 4 fixed presets (natural = no recolor, blonde, red, blue).
  Every change builds a transient `LpcRecipe` from the picked parts' full LayerSet spans, runs
  `LpcCharacterBuilder.Build`, destroys the recipe, and restores the previously playing clip on the
  `LpcClipPlayer` (default walk). Hair recolor always starts from the resolved hair layer's
  ORIGINAL clips via `RecolorClips` + `SetLayerClips` so ramps don't stack.
- **LpcAnimationPreview** (formerly LpcAnimationMenu): one button per clip the character actually
  has (`Btn_<clipname>`, from `LpcClips.All` ∩ `HasClip`), driving a `LpcClipPlayer`. Coverage
  transparency: a clip some worn part can't draw still gets a button, flagged amber with a `*`
  suffix; clicking reports exactly which slots lack the clip's art and hold their standing frame
  (`SlotsMissingClip`) — nothing silently degrades. `Update()` rebuilds when a coverage `Signature` (per-clip missing-slot counts) changes,
  covering script-order races and live part swaps. **`startHidden` defaults true** — it is a
  package-dev tool and stays invisible in consuming projects until `Show()`/`Toggle()`.

## Assembly structure

| asmdef | rootNamespace | references | platforms |
| --- | --- | --- | --- |
| TCLowe.Lpc.Runtime | Lpc | *(none — fully self-contained)* | all |
| TCLowe.Lpc.Samples | Lpc.Samples | Runtime, UnityEngine.UI | all |
| TCLowe.Lpc.Editor | Lpc.Editor | Runtime, Samples | Editor only |
| TCLowe.Lpc.EditMode.Tests | Lpc.Tests | Runtime, Editor, Samples, UnityEngine.TestRunner, UnityEditor.TestRunner | Editor only |

The test asmdef sets `overrideReferences: true` with precompiled `nunit.framework.dll`,
`autoReferenced: false`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`. Editor referencing Samples
is what lets editor tooling and tests see `LpcAnimationPreview`.

## Dual test architecture

One set of NUnit test sources, two runners:

1. **Offline** — `cd Tests~ && dotnet test`. `Tests~/LpcLogic.Offline.csproj` targets net10.0
   (NUnit pinned to **3.14.0** to match Unity's bundled framework; Test.Sdk 17.11.1, adapter
   4.6.0). It **links** (not copies) the pure Runtime sources as the single source of truth:
   `LpcClip.cs`, `LpcClipMath.cs`, `LpcSliceMath.cs`, `LpcCustomAnims.cs`, `LpcPreviewMath.cs`,
   `LpcBodyType.cs`, `LpcCategory.cs`, `LpcCredits.cs`, `LpcSourceLayout.cs` — plus all
   `../Tests/*.cs` via a **non-recursive** glob. `Tests~/Shim/UnityEngine.cs` stubs only what the
   linked code touches: `Sprite` (empty marker — clip logic treats sprites as opaque), `[Tooltip]`,
   `Rect` (x/y/width/height + ctor), `Mathf` (Max, Min, Clamp, FloorToInt). If a linked file starts
   using a new UnityEngine symbol, add its minimal stub to the shim. The trailing `~` makes Unity
   ignore the folder, so the fake `UnityEngine` namespace never collides with the real one.
2. **Unity EditMode** — `Tests/TCLowe.Lpc.EditMode.Tests.asmdef`, run via the MCP bridge:
   `run_tests(mode="EditMode", assembly_names="TCLowe.Lpc.EditMode.Tests")` (see
   `workflow-and-conventions.md`).

Split rule: `Tests/*.cs` = shared pure-logic tests compiled by both runners;
`Tests/Integration/*.cs` = Unity-only (real GameObjects/Sprites/ScriptableObjects, filesystem,
`Lpc.Editor` namespace) — the offline glob skips Integration while Unity's asmdef compiles it
recursively. Integration coverage: the full animator pathway (built character +
`LpcAnimator.Tick` → correct sprite on a live SpriteRenderer; walk-standing fallback on missing
clips (hide only when walk is missing too); one-shot return-to-locomotion; direction rows),
builder body-type resolution on real ScriptableObjects, recipe `colors` recolor at build time,
`LpcClipPlayer` frame-range confinement, coverage APIs, `RecolorClips` on real pixel data,
`LpcCreditsReader` against a synthetic temp CSV, and sheet-def parsing/z-index against temp
directory trees. When adding a pure Runtime file a shared test needs, add a `<Compile Include>`
link to `Tests~/LpcLogic.Offline.csproj`.

**The core discipline:** put bug-prone arithmetic in a pure Runtime static, link it offline, test
it with `dotnet test`; keep MonoBehaviour/asset glue thin and cover it in `Tests/Integration/`.
