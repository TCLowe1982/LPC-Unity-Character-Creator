---
name: lpc-character-creator-expert
description: >-
  Expert on the LPC Unity Character Creator (this repo, com.tclowe.lpc-character-creator) — a Unity
  package for drop-in Liberated Pixel Cup (LPC) layered characters: a code-driven animation clip
  system (all 15 ULPC animations × 4 directions, no .anim assets), runtime layered characters with
  live equip/swap, body types (incl. body-agnostic "any" variants), palette recolor, a selective
  def-driven importer + auto-slicer (per-animation grids, oversize pivots, multi-layer weapon
  expansion), a Catalog Window with live pre-import preview, a generated demo scene, and a
  bundled-art pipeline shipped via GitHub Release. Use when working on the runtime
  (LpcCharacter/LpcAnimator/LpcClipPlayer, LpcClip/LpcClips/LpcClipMath, LpcSliceMath,
  LpcPreviewMath, LpcBodyType, LpcCategory, LpcCustomAnims, LpcRecolor, LpcCredits,
  LpcLayerSet/LpcRecipe/LpcCharacterBuilder), the editor importer/postprocessor/sheet-def parser,
  the Catalog Window, the Samples (LpcAnimationPreview, LpcDemoCreator), the tests, or the art
  bundle. Knows the core patterns (pure logic in Runtime so it unit-tests offline; per-animation
  slicing with derived cell size; hide-on-missing-clip; recolor-across-all-clips; oversize
  baseline pivots) and the workflow (beads, dual-runner tests, the Unity MCP bridge in Ultima4_2d,
  commit/push). Triggers on: LPC, LpcCharacter, clip system, LpcAnimator, LpcLayerSet, body type,
  recolor, catalog importer, auto-slice, oversize, sheet_definitions, character creator, art
  bundle, CREDITS attribution.
---

# LPC Unity Character Creator — Expert

A Unity package (`com.tclowe.lpc-character-creator`, Unity 2022.3+) for **drop-in LPC layered
characters**. A character is a stack of `SpriteRenderer` layers (body, legs, feet, torso, head,
hair, gear…) all driven to the **same (clip, direction, frame)** every frame. Animations are
**code-driven** — there are **no Unity `.anim` AnimationClips**; the runtime flips
`SpriteRenderer.sprite` via a frame index.

> **The deep reference lives in [Documentation~/expert-guide/](../../../Documentation~/expert-guide/README.md)** —
> model-agnostic and single-sourced (generated from a full-repo Ledger Pattern sweep; regenerate
> or hand-update it together with code changes). This file is the Claude-side wrapper: core
> patterns + where to look.

## Core patterns — read first

1. **Pure logic lives in Runtime so it unit-tests offline.** Bug-prone math is in Unity-light
   statics — `LpcClipMath` (pose index, walk-cycle, loop/one-shot), `LpcSliceMath` (sheet →
   cells, oversize pivot `PivotY`), `LpcPreviewMath` (editor preview UV/rect/frame),
   `LpcBodyType` (variant fallback + `Any`), `LpcCategory` (z-order), `LpcCustomAnims`
   (custom-animation → base clip), `LpcCredits`, `LpcSourceLayout` — linked into the offline
   `dotnet` project (`Tests~/`) via a shim. **When adding logic, put the pure part in a Runtime
   static and test it offline** (`cd Tests~ && dotnet test`); add a `<Compile Include>` line to
   `Tests~/LpcLogic.Offline.csproj` for a new linked Runtime file.
2. **Frame indexing is `dir * framesPerDir + frame`** for the active clip. Each animation has
   its own grid (walk 9×4, hurt 6×1, shoot 13×4) — see `LpcClips` (15 ULPC animations, names ==
   the on-disk PNG files). Cell size is **derived** from the sheet, so oversize 128/192px
   weapon sheets slice on the same path and pivot at the embedded 64px body baseline
   `(0.5, (cellH−64)/(2·cellH))`.
3. **Per-animation frames per layer.** `LpcLayerSet.clips` is `LpcClipFrames[]`; legacy
   `frames` is the walk-only fallback — **never fill it with a non-walk clip** (Resolve plays
   legacy frames AS walk).
4. **Hide-on-missing.** A layer with no frames for the active clip hides (sprite = null); the
   UI surfaces coverage gaps (amber `*`, status line) instead of restricting.
5. **Recolor applies across ALL clips** (`LpcRecolor.RecolorClips` + `SetLayerClips`), or the
   color only holds on walk.
6. **Def-driven multi-layer import.** A part whose sheet_definition has several layers (weapon
   fg/bg/oversize attacks, cape fg/bg) expands to one catalog entry per layer at the LAYER's
   zPos; secondary layers get sub-slots (`weapon_l2`…) because the runtime keys layers by slot.
   Body-agnostic parts (adult hair, hats, most weapons) tag `LpcBodyType.Any`.

## Where things are

- `Runtime/` (MIT) — clip system, character, animator, builder, body types, categories,
  recolor, credits, slice/preview math. `Editor/` — importer, postprocessor, sheet-def
  parser/index, Catalog Window (live pre-import preview), art importer, demo scene builder.
  `Samples/` — `LpcAnimationPreview` (dev tool, hidden by default via `startHidden`),
  `LpcDemoCreator` (+ Tools/LPC/Create Demo Scene).
- `Tests/` — shared NUnit (offline + Unity); `Tests/Integration/` — Unity-only;
  `Tests~/` — the offline `dotnet` project (trailing `~` hides it from Unity).
- Full maps: **[Documentation~/expert-guide/architecture.md](../../../Documentation~/expert-guide/architecture.md)**.

## How to run / verify

- **Offline (fast, no Unity):** `cd Tests~ && dotnet test`.
- **In Unity:** via the MCP-for-Unity bridge against the test bed **Ultima4_2d**
  (`C:/UnityProjects/Ultima4_2d`). `run_tests` assembly `TCLowe.Lpc.EditMode.Tests`;
  `refresh_unity` (scope `all`) → `read_console`; `manage_camera screenshot` / `execute_code`
  for visual checks. Gotchas (stale array refs after re-import, ForceUpdate to trigger the
  postprocessor, unsaved-scene rule): **[Documentation~/expert-guide/workflow-and-conventions.md](../../../Documentation~/expert-guide/workflow-and-conventions.md)**.

## Workflow (project rules)

- **Beads (`bd`)** for ALL task tracking — `bd ready` / `bd show` / `bd update --claim` /
  `bd close`; `bd remember` for cross-session notes. Session ends with quality gates + commit +
  **push** (work is not done until `git push` succeeds).
- Multi-line commit messages: **write to a file and `git commit -F`** (PowerShell here-strings
  break on quotes/`*`). `.meta` files are committed (UPM).
- Art pipeline, licensing, attribution, art bundle, OGA sourcing findings:
  **[Documentation~/expert-guide/art-pipeline-and-licensing.md](../../../Documentation~/expert-guide/art-pipeline-and-licensing.md)**.

## Consult the deep guide when

- Touching runtime clip/layer/animator internals or adding a Runtime static → `architecture.md`.
- Working on the importer/slicer, body types, recolor, coverage, credits, or the art bundle →
  `art-pipeline-and-licensing.md`.
- Running/adding tests, driving Unity via MCP, commit/scene gotchas → `workflow-and-conventions.md`.
