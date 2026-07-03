# Expert Guide — LPC Unity Character Creator

A complete, tool-agnostic knowledge base for maintaining `com.tclowe.lpc-character-creator`
(Unity 2022.3+): drop-in Liberated Pixel Cup (LPC) layered characters with a code-driven
animation clip system (all 15 ULPC animations × 4 directions, **no `.anim` assets**), live
equip/swap, body types, palette recolor, a selective def-driven importer + auto-slicer, and
a bundled-art pipeline shipped via GitHub Release.

Written for any maintainer — a human with an editor or any AI coding agent. It was produced
by a Ledger Pattern sweep of **every tracked file in the repo** (3 independent extraction
passes per file, ≥2-of-3 consensus, per-file coverage verified), so it is complete as of the
commit that last regenerated it. When the code changes, update these docs in the same commit.

## The five ideas that explain everything else

1. **Pure logic lives in Runtime statics so it unit-tests offline.** The bug-prone math
   (`LpcClipMath`, `LpcSliceMath`, `LpcPreviewMath`, `LpcBodyType`, `LpcCategory`,
   `LpcCustomAnims`, `LpcCredits`, `LpcSourceLayout`) is Unity-light and linked into an
   offline `dotnet test` project (`Tests~/`) through a tiny shim. New logic follows the same
   split: pure part → Runtime static + offline test; MonoBehaviour/asset glue → `Tests/Integration/`.
2. **Each animation has its own grid.** Frame index = `dir * framesPerDir + frame`; walk is
   9×4, hurt 6×1, shoot 13×4… Never assume a single 36-frame layout. Cell size is *derived*
   from the sheet (`width/cols × height/rows`), which is how oversize 128/192px weapon
   sheets slice on the same code path.
3. **Layers missing the active clip fall back to the walk standing frame** (walk frame 0 of
   the same direction) so partial ULPC coverage doesn't pop equipment in and out; layers
   lacking walk too hide (sprite = null) rather than show a stale pose. The UI *surfaces*
   coverage gaps rather than restricting animation choice.
4. **Oversize art centers the 64px body in the bigger cell**, so oversize sprites pivot at
   the embedded body baseline `(0.5, (cellH−64)/(2·cellH))` and line up with bottom-pivoted
   64px layers at the same transform position.
5. **Recolor applies across ALL clips**, or the color silently reverts on non-walk animations.

## Contents

| Doc | Covers |
| --- | --- |
| [architecture.md](architecture.md) | Runtime clip system, layered character, builder/recipes/body types, the pure-math statics, the full editor import pipeline (manifest → copy → slice → LayerSets, def-driven multi-layer expansion), Catalog Window live preview, demo scene builder, assemblies, and the dual test architecture. |
| [art-pipeline-and-licensing.md](art-pipeline-and-licensing.md) | The 15-animation set and grids, oversize/custom animations, 21 categories + z-order, body types and the `any` variant, recolor rules, coverage policy, the selective importer and art bundle, attribution, and licensing (MIT code / CC-BY-SA·GPL·OGA-BY art). |
| [workflow-and-conventions.md](workflow-and-conventions.md) | Issue tracking (bd/beads) and the session-close protocol, the two test runners and when each applies, commit conventions, Unity editor/bridge gotchas, and repo conventions. |

## Fast orientation

- Runtime: `Runtime/` (MIT) — clip system, `LpcCharacter`, animator/player, builder, recolor, credits.
- Editor: `Editor/` — selective importer, auto-slicing postprocessor, sheet-definition parser,
  Catalog Window (with live pre-import preview), art-bundle importer, demo scene builder.
- Samples: `Samples/` — `LpcAnimationPreview` (dev tool, hidden by default) and `LpcDemoCreator`.
- Tests: `Tests/` shared NUnit (offline + Unity), `Tests/Integration/` Unity-only, `Tests~/`
  the offline dotnet project (trailing `~` hides it from Unity).
- Verify changes: `cd Tests~ && dotnet test` (offline) and the Unity EditMode suite
  (`TCLowe.Lpc.EditMode.Tests`) — see workflow-and-conventions.md.
