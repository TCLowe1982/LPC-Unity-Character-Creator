# Art Pipeline & Licensing — LPC Unity Character Creator

How LPC art gets from the upstream generator into a running Unity character, and what the licenses
require in return. Runtime internals (clip playback, layer stack, builder) are in `architecture.md`;
test/commit/MCP workflow is in `workflow-and-conventions.md`.

## The 15-animation set (measured, canonical)

`LpcClips.All` registers the 15 standard ULPC animations in universal-sheet row order. Names equal
the on-disk PNG file names, so **import == registry == playback** — an imported `walk.png` resolves
straight to its layout. Frame index is always `dir * framesPerDir + frame`; direction rows are
0=up/N, 1=left/W, 2=down/S, 3=right/E. Standard cell size is `LpcClips.Cell = 64` px.

| clip | framesPerDir × directions | fps | loop |
|---|---|---|---|
| spellcast | 7 × 4 | 8 | once |
| thrust | 8 × 4 | 8 | once |
| walk | 9 × 4 | 8 | loop |
| slash | 6 × 4 | 12 | once |
| shoot | 13 × 4 | 8 | once |
| hurt | 6 × 1 | 8 | once |
| climb | 6 × 1 | 8 | loop |
| idle | 2 × 4 | 2 | loop |
| jump | 5 × 4 | 8 | once |
| run | 8 × 4 | 12 | loop |
| sit | 3 × 4 | 4 | loop |
| emote | 3 × 4 | 6 | once |
| combat_idle | 2 × 4 | 6 | loop |
| backslash | 13 × 4 | 12 | once |
| halfslash | 6 × 4 | 12 | once |

Notes:

- `hurt` and `climb` are **single-direction** (south only); `LpcClipMath.PoseIndex` forces dir 0
  for 1-direction clips. Never assume a single 36-frame walk grid — every animation has its own.
- `backslash`/`halfslash` are the 1h backslash/halfslash sheets.
- There is **no** `watering` or `1h_slash` universal body sheet. `watering` is a tool overlay,
  deliberately excluded from `LpcClips.All` (the epic notes were wrong; the registry was reconciled
  against reality).
- `LpcClips.Get(name)` falls back to Walk for unknown/empty names; `TryGet` reports failure.
- Playback speed is each clip's own fps × the animator's `speedScale`, so clips of different
  lengths play at their intended speed.

## Oversize sheets & custom animations

Some parts (big weapons) ship **oversize** sheets in 128 or 192 px cells instead of 64. These are
handled by **derivation, not enumeration**: `LpcSliceMath.TryCellSize` computes
`cell = sheetWidth/cols × sheetHeight/rows` from the clip's grid, and `IsOversize` flags any cell
larger than `BaseCell = 64`. If the sheet doesn't divide evenly by the grid, `TryCellSize` returns
false and the caller falls back to a fixed grid rather than slicing garbage.

The ULPC generator composes an oversize cell by **centering the 64 px body frame** in the larger
cell (offset `(cell − 64) / 2` on both axes), so the body baseline sits `(cellH − 64)/2` px above
the cell bottom. `LpcSliceMath.PivotY` returns `(cellH − baseCell) / (2·cellH)` for oversize cells
and `0` (bottom pivot) for standard ones; pivot X is always 0.5. This makes an oversize weapon line
up with bottom-pivoted 64 px layers at the same transform position — the alignment mechanism for
mixed cell sizes. The Catalog Window's live preview (`LpcPreviewMath.DestRect`) anchors every
layer's pivot at the same point, exactly like the runtime, so the pre-import preview matches
in-game composition.

**Custom animations** (`custom_animation` declared per layer in `sheet_definitions`) come in two
kinds, handled by `LpcCustomAnims`:

- **Grid-compatible** — the same frame order and grid as a base clip, just bigger cells. These are
  imported *as* that base clip. `BaseClip` mappings: `walk_128 → walk`, `slash_128 → slash`,
  `slash_oversize → slash`, `thrust_128 → thrust`, `thrust_oversize → thrust`,
  `halfslash_128 → halfslash`, `backslash_128 → backslash`. `IsGridCompatible` is true exactly for
  these.
- **Remixed** — `slash_reverse_oversize`, `tool_whip`, `tool_rod`, `wheelchair` re-sequence frames
  into layouts that match no clip in `LpcClips`. `BaseClip` returns null and the importer **skips**
  them (decision `od3`): representing remixed sequences would need a dynamic clip registry, and
  silently mis-slicing them is worse than not importing them.

## 21 categories + z-order

`LpcCategory` is the single source of truth for the canonical category set and default draw order;
the runtime itself is category-agnostic (a slot is just a string). Lower z draws further back;
behind-body categories carry **negative z** so they render behind `body` (z 10). Gaps between
values are intentional room for sheet-definition overrides.

| category | z | | category | z | | category | z |
|---|---|---|---|---|---|---|---|
| shadow | −100 | | dress | 35 | | facial | 66 |
| backpack | −20 | | torso | 40 | | beards | 68 |
| cape | −15 | | arms | 45 | | hair | 72 |
| quiver | −10 | | neck | 50 | | hat | 80 |
| body | 10 | | shoulders | 55 | | tools | 90 |
| legs | 20 | | head | 60 | | weapon | 100 |
| feet | 30 | | eyes | 64 | | shield | 100 |

`IsBehindBody(cat)` is true iff `DefaultZ(cat) < DefaultZ("body")` — shadow, backpack, cape, quiver
only. Unknown/null categories default to z=100 (frontmost, tied with weapon/shield).

**Resolution priority:** explicit manifest `zPos` > the part's `sheet_definitions/*.json` zPos
(auto-read via `LpcSheetDefParser` → `LpcSheetDefIndex`, source→zPos) > `LpcCategory.DefaultZ`.

**Multi-layer parts:** a sheet_definition with several layers (weapon fg/bg, oversize attacks, cape
fg/bg) expands definition-driven — one importer entry per layer at that layer's zPos, secondary
layers on sub-slots (`weapon_l2`, …). The runtime orders any z values, so multi-layer and
behind-body layering need no runtime change. **There is no per-direction z in LPC** — the "cape
behind one way, in front the other" effect is two layers with baked per-direction art
(`cape_solid.json`: `layer_1` foreground zPos 85, `layer_2` background zPos 5, vs body 10), which
multi-layer support already handles.

## Body types & fallback

`LpcBodyType` defines 8 requestable types (plain strings matching the generator's sheet_definitions
keys, so the set stays open): `male`, `muscular`, `female`, `pregnant`, `teen`, `child`,
`skeleton`, `zombie`. `Normalize(null/empty)` → `male` (the LPC base body). Fallback chains, most
specific first:

| requested | chain |
|---|---|
| male / female / child | (no fallback) |
| muscular | muscular → male |
| pregnant | pregnant → female |
| teen | teen → female → male |
| skeleton | skeleton → male |
| zombie | zombie → male |

`Resolve(requested, available)` walks the chain and returns the first available match; if none
match it falls back to the **`any`** variant if present, else null (part unsupported for that
body). `FallbackChain` of an unknown type is just that type itself. A recipe carries exactly one
`bodyType`; `LpcCharacterBuilder.ResolveLayers` picks each slot's best variant and **drops the
slot** when nothing resolves.

**`LpcBodyType.Any` ("any")** tags body-agnostic parts — adult hair, hats, most weapons: sources
with no body-type subfolders, or definition layers where every requested body maps to one sheet. It
is a *variant tag*, not a requestable body: it is excluded from `LpcBodyType.All`, which also feeds
folder-name detection. `LpcSourceLayout` uses `IsKnown` on the second-to-last path segment to split
`body/bodies/male/walk.png` into part `body/bodies` + bodyType `male` + anim `walk`, while
`hair/afro/adult/walk.png` stays part `hair/afro/adult`, body-agnostic ("adult" is not a known
body type). LPC stores variants in `<part>/<bodytype>/` subfolders; the importer copies each
requested type and stamps `LpcLayerSet.bodyType`.

## Recolor (palette swap)

Flow: game UI Color mode → `LpcRecolor.RecolorClips(layerSet.clips, ramp)` →
`LpcCharacter.SetLayerClips(slot, recoloredClips)`.

**Recolor EVERY clip, never just walk.** Each animation is a separate sheet texture; recoloring
only the walk frames means the color reverts on every non-walk animation (and a seam appears at the
hairline). `RecolorClips` recolors all clips; `SetLayerClips` swaps a slot's entire per-animation
frame set (versus `SetLayerFrames`, which touches only the *active* clip and is not for recolor).

How it works (`LpcRecolor`): a layer's frames share one texture, so the texture is recolored once
and re-sliced — cheap enough to run live as the player cycles colors. Detection: histogram the
opaque pixels (alpha < 16 = skip/pass-through), take the N most-common colors (N = target ramp
length) as the source shades; sort both source shades and target ramp ascending by luminance
(BT.601: 0.299r + 0.587g + 0.114b) so dark maps to dark; pad a short target ramp by repeating its
lightest color. Each opaque pixel snaps to the nearest detected shade (squared RGB distance) and
takes the corresponding target color, **preserving original alpha** — anti-aliased edges survive.
Output is a point-filtered RGBA32 texture, no mips, cached by `(texture instance ID, ramp hash)`;
the cache is never evicted for the app-domain lifetime. Unreadable textures log
`[LPC] Recolor needs a readable texture: <name>` and fall back to the originals unchanged — the
catalog postprocessor marks catalog textures Read/Write enabled to satisfy this.

`LpcPalette` (ScriptableObject, menu LPC/Palette) holds named ramps for one category (hair, cloth,
skin, eyes, …), **ordered dark → light** — the ordering LpcRecolor's luminance mapping assumes.
Ramps are baked from the LPC `palette_definitions` by `LpcPaletteImporter` so they ship as assets
and load at runtime. Design rationale: appearance = style (which layer) × color (which ramp) as
independent dimensions — N styles + M ramps, not N×M pre-baked variants.

## Coverage transparency (surface, don't restrict)

A part may lack animations — the formal shirt has only 6/15 (no jump/climb/run/…). Policy: **keep
every animation available and surface the gaps.**

- Runtime rule: a layer with no frames for the active clip **falls back to its walk standing
  frame** (walk frame 0, same direction — `LpcClipFrames.ResolveWithFallback`), so gear with
  combat-only sheets doesn't pop in and out; a layer lacking walk too **hides** (`sprite = null`)
  instead of showing a stale pose from the previous clip.
- `LpcLayerSet.SupportedClips()` / `MissingClips()` and `LpcCharacter.HasClip` /
  `SlotsMissingClip(clip)` expose per-slot coverage.
- `LpcAnimationPreview` (sample; hidden by default via `startHidden`, opened with
  `Show()`/`Toggle()`) flags clips some worn part can't draw with an amber `*` and reports on
  click, e.g. `"jump": no jump art (holds standing frame): torso`. It auto-rebuilds when
  coverage changes.
- The demo `AppearanceSelector` appends `(6/15 anims)` to an incomplete part's label.

## Selective importer, manifest & catalog layout

The full LPC source tree is **~483 MB / 144,699 PNGs** (~20 animations × hundreds of layers ×
variants). Never dump it into `Assets/` — it chokes Unity's import pipeline. The importer exists to
pull only what you pick.

- **Source layout:** `spritesheets/<category>/<part>/<bodytype>/<anim>.png` (body-type segment
  absent for body-agnostic parts).
- **Manifest:** `Assets/Characters/LPC/catalog_manifest.json` — fields `lpcSourcePath` (a local
  LPC clone or the extracted art bundle), `bodyTypes`, `animations`, and `entries[]`
  (slot + source + variant + zPos + credit overrides). Walk-only by default; adding an animation is
  a one-line edit.
- **Import:** `LpcCatalogImporter` (menu `Tools/LPC/Import Starter Catalog`) copies the selected
  parts × bodyTypes × animations into `Assets/Characters/LPC/Catalog/<slot>/<id>[__<anim>].png`
  (per-body-type `<bodytype>/` subfolders; body-agnostic sources tagged `any`), and writes
  `catalog_index.json` + `CREDITS.txt`.
- **Slice:** `LpcCatalogPostprocessor` (AssetPostprocessor, automatic on import) slices each PNG by
  its animation's grid via `LpcSliceMath` (fixed-64 fallback when the grid doesn't divide; oversize
  cells pivot at the embedded body baseline), assembles ALL animations into `LpcLayerSet.clips`,
  and stamps `bodyType` + `zOrder`. The legacy `LpcLayerSet.frames` field gets **walk only** —
  never another clip, or it would play as walk.
- **Browse:** `LpcCatalogWindow` (menu `Tools/LPC/Catalog Window`) browses the source tree and
  live-previews layered parts *pre-import*, drawn from source sheets at their definition zPos.
  `Tools/LPC/Create Demo Scene` builds a mini character-creation scene from the imported catalog
  (parts grouped by source so multi-layer weapons equip whole).

## Art bundle: build & consume

The art ships as a **GitHub Release artifact**, not in git (too large).

**Build** (maintainers): `tools/Build_Art_Release.ps1` — e.g.
`pwsh tools/Build_Art_Release.ps1 -LpcSource "D:/Projects/Universal-LPC-Spritesheet-Character-Generator" -OutDir "$env:TEMP/lpc-art-release"`
(both values are the defaults). Inputs (fatal error if missing): `<LpcSource>/spritesheets`,
`<LpcSource>/CREDITS.csv`, and this repo's `LICENSE-ART.txt`. It generates a `CREDITS.md` (records
the PNG count, restates the tri-license, points at CREDITS.csv as canonical, links the source
repo), then zips `spritesheets/` + `CREDITS.csv` + `CREDITS.md` + `LICENSE-ART.txt` into
**`LpcArt-full.zip` (~212 MB)** using `tar.exe` (bsdtar, built into Windows 10+ — far faster than
`Compress-Archive` for ~145k files), with multiple `-C` switches placing everything at archive
root. It reports the zip size in MB and prints the publish template:
`gh release create art-vYYYYMMDD "<zip>" -R TCLowe1982/LPC-Unity-Character-Creator -t 'LPC Art' -n 'Full LPC character art bundle'`.
Release tag convention `art-vYYYYMMDD`; **current tag: `art-v1`**.

**Consume:** `Editor/LpcArtImporter` (menu `Tools/LPC → Import Bundled Art`) downloads the pinned
release zip and extracts it to **`LpcArtSource/` outside `Assets/`** (so Unity never imports 144k
textures), then repoints the manifest's `lpcSourcePath`. Run the selective importer afterwards to
slice the parts you want. Alternatively, point `lpcSourcePath` at your own local LPC clone and skip
the bundle entirely.

**Caveat:** the unauthenticated download only works while the GitHub repo is **public** — on a
private repo the release asset returns 404.

## Attribution / credits pipeline

Crediting artists is **mandatory** (see Licensing). The chain:

1. **`CREDITS.csv`** (from the upstream generator; shipped inside the art bundle) is the canonical
   per-file record: author(s), license(s), source URL(s) per sprite.
2. **`LpcCreditsReader`** (Editor) parses it — quote-aware CSV, header-detected columns — matches
   rows under each imported part, and merges any manifest credit overrides.
3. **`LpcCredits`** (pure Runtime static, offline-testable) aggregates `LpcCreditEntry` records
   (`part`, `authors[]`, `licenses[]`, `urls[]`, `notes`) — dedupe is case-insensitive but
   preserves first-seen order/casing — and `Format()` renders a complete attribution document
   (default title `LPC ART ATTRIBUTION  (auto-generated by LPC Unity Character Creator)`, license
   boilerplate, then per-part authors/license/urls/notes). Coverage is **the parts actually
   imported**, not the whole library.
4. The importer writes the result as `CREDITS.txt` next to the catalog. A consuming game can reuse
   the same `LpcCreditEntry` + `Format()` to render an in-game credits screen.

## Licensing

- **Package code (everything under `Runtime/`, `Editor/`, `Samples/`): MIT** — see `LICENSE`
  (copyright 2026 TC Lowe). A note appended to the MIT text clarifies it covers *code only*; the
  package neither bundles the art into the git repo nor relicenses it.
- **Art: multi-licensed CC-BY-SA 3.0 / GPL 3.0 / OGA-BY 3.0** — see `LICENSE-ART.txt`. Unless
  CREDITS.csv says otherwise for a specific asset, you may use it under **any** of the three. All
  three permit use in any project **including commercial**. Duties:
  - **Attribution (all three):** reproduce the relevant CREDITS.csv entries (author, license,
    source URL) in your game's credits.
  - **Share-alike (CC-BY-SA and GPL only):** redistributed art — including modified art — stays
    under the same license. OGA-BY 3.0 has no share-alike clause.
- Provenance: the art is the Liberated Pixel Cup set as aggregated by the Universal LPC
  Spritesheet Character Generator —
  <https://github.com/sanderfrenken/Universal-LPC-Spritesheet-Character-Generator>; LPC project
  home: <https://lpc.opengameart.org/>.

## Sourcing extra art (2g8.6 findings, July 2026)

Investigated scraping OpenGameArt for ULPC-compatible layers missing from the generator clone.
**Conclusion: don't.** The upstream generator repo *is* the curated aggregation — all major
ULPC-format series (JaidynReiman's "Expanded ULPC" heads/hair/pants/socks-shoes, bluecarrot16's
packs, extended/oversize weapons) are already integrated, citing 143 credited OGA sources. What
remains on OGA is creature sheets with their own layouts, LPC-Revised-layout art (different base
grid), or tiles — none consumable as character layers by the slicer. To get "missing" assets:
**`git pull` the LPC clone** (or rebuild + re-pin the art bundle); any ULPC-format sheet dropped
into the source tree is already consumable by the Catalog Window/importer. Re-check OGA only if a
new pack explicitly says "ULPC layout" and its content is absent from `spritesheets/`.
