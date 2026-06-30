---
name: lpc-character-creator-expert
description: >-
  Expert on the LPC Unity Character Creator (this repo, com.tclowe.lpc-character-creator) — a Unity
  package for drop-in Liberated Pixel Cup (LPC) layered characters: a code-driven animation clip
  system (all 15 ULPC animations × 4 directions, no .anim assets), runtime layered characters with
  live equip/swap, body types, palette recolor, a selective importer + auto-slicer, and a bundled-art
  pipeline shipped via GitHub Release. Use when working on the runtime (LpcCharacter/LpcAnimator/
  LpcClipPlayer, LpcClip/LpcClips/LpcClipMath, LpcSliceMath, LpcBodyType, LpcCategory, LpcRecolor,
  LpcCredits, LpcLayerSet/LpcRecipe/LpcCharacterBuilder), the editor importer/postprocessor, the
  Samples menu, the tests, or the art bundle. Knows the core patterns (pure logic in Runtime so it
  unit-tests offline; per-animation slicing; hide-on-missing-clip; recolor-across-all-clips) and the
  workflow (beads, dual-runner tests, the Unity MCP bridge in Ultima4_2d, commit/push). Triggers on:
  LPC, LpcCharacter, clip system, LpcAnimator, LpcLayerSet, body type, recolor, catalog importer,
  auto-slice, character creator, art bundle, CREDITS attribution.
---

# LPC Unity Character Creator — Expert

A Unity package (`com.tclowe.lpc-character-creator`) for **drop-in LPC layered characters**. A
character is a stack of `SpriteRenderer` layers (body, legs, feet, torso, head, hair, gear…) all
driven to the **same (clip, direction, frame)** every frame. Animations are **code-driven** — there
are **no Unity `.anim` AnimationClips**; the runtime flips `SpriteRenderer.sprite` via a frame index.

## Core patterns — read first

1. **Pure logic lives in Runtime so it unit-tests offline.** The bug-prone math is in plain,
   Unity-light static classes — `LpcClipMath` (pose index, walk-cycle, loop, one-shot), `LpcSliceMath`
   (sheet → frame rects), `LpcBodyType` (variant fallback), `LpcCategory` (z-order), `LpcCredits`
   (attribution), and the `LpcClips`/`LpcClipFrames` data. These are linked into an **offline `dotnet`
   test project** (`Tests~/`) via a tiny `UnityEngine` shim, so they run with `dotnet test` — no editor.
   **When adding logic, put the pure part in a Runtime static and test it offline.**
2. **Frame indexing is `dir * framesPerDir + frame`** for the active clip. Each animation has its own
   layout (walk 9×4, hurt 6×1, shoot 13×4) — see `LpcClips` (15 ULPC animations, names == the on-disk
   PNG file names so import == registry == playback). Don't assume a 36-frame walk grid.
3. **Per-animation frames per layer.** `LpcLayerSet.clips` is `LpcClipFrames[]` (clip name → sprites);
   legacy `frames` is the walk-only fallback. The importer's auto-slicer fills `clips`.
4. **Hide-on-missing.** A layer with no frames for the active clip **hides** (sprite = null) rather
   than showing a stale pose — so a shirt with no `jump` sheet cleanly disappears. The UI **surfaces**
   this (coverage flags + a status line) instead of restricting.
5. **Recolor applies across ALL clips** (`LpcRecolor.RecolorClips` + `LpcCharacter.SetLayerClips`), or
   the color only holds on walk.

## Where things are

- `Runtime/` (MIT) — the clip system, character, animator, builder, body types, categories, recolor,
  credits, slice math. `Editor/` — importer, auto-slicing postprocessor, credits reader, art importer.
  `Samples/` — `LpcAnimationMenu` (UI, needs `UnityEngine.UI`).
- `Tests/` — **shared** NUnit tests (offline + Unity) at the root; `Tests/Integration/` is **Unity-only**
  (real GameObjects/Sprites); `Tests~/` is the **offline `dotnet`** project (hidden from Unity by the `~`).
- Full file/architecture map: **`references/architecture.md`**.

## How to run / verify

- **Offline (fast, no Unity):** `cd Tests~ && dotnet test` — runs the shared pure-logic tests.
- **In Unity:** via the **MCP-for-Unity bridge** against the test bed project **Ultima4_2d**
  (`C:/UnityProjects/Ultima4_2d`, which references this package). `run_tests` assembly
  `TCLowe.Lpc.EditMode.Tests`; `refresh_unity` (scope `all` to import new files) → `read_console` for
  errors; `manage_camera screenshot` / `execute_code` to drive the `CharacterCreation` scene.
- Bridge + test-bed details and gotchas: **`references/workflow-and-conventions.md`**.

## Workflow (project rules)

- **Beads (`bd`)** for ALL task tracking — `bd ready` / `bd show` / `bd update --claim` / `bd close`.
  Epic is `2g8`. Use `bd remember` for cross-session notes, not MEMORY.md.
- **Commit + push** at session end; PRs/commits end with the Co-Authored-By line. Multi-line commit
  messages: **write to a file and `git commit -F`** (PowerShell here-strings break on embedded quotes/`*`).
- Art pipeline, slicing, body types, recolor, coverage, licensing: **`references/art-and-licensing.md`**.

## Consult the references when

- Touching the runtime clip/layer/animator internals or adding a Runtime static → `architecture.md`.
- Running/adding tests, driving Unity via MCP, or you hit a scene/commit gotcha → `workflow-and-conventions.md`.
- Working on the importer/slicer, body types, recolor, coverage, or the art bundle/CREDITS →
  `art-and-licensing.md`.
