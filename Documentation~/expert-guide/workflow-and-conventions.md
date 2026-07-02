# Workflow & Conventions — LPC Unity Character Creator

This is the process reference for anyone — a human with an editor or an AI coding agent — working
on this repo. Runtime/editor internals live in `architecture.md`; the importer, slicing, body
types, recolor internals, and licensing rules live in `art-pipeline-and-licensing.md`.

## Package identity

- UPM package **`com.tclowe.lpc-character-creator`**, displayName "LPC Unity Character Creator",
  version **0.1.0** (unreleased per CHANGELOG), requires **Unity 2022.3+**. Author: TC Lowe.
- Package code is **MIT** (`LICENSE`); imported LPC art is separately licensed CC-BY-SA 3.0 /
  GPL 3.0 / OGA-BY 3.0 (`LICENSE-ART.txt`) — see `art-pipeline-and-licensing.md` before touching
  anything attribution-related.
- Consumers install via UPM git URL (once published) or a local reference in
  `Packages/manifest.json`: `"com.tclowe.lpc-character-creator": "file:D:/Projects/LPC-Unity-Character-Creator"`.
- This repo **is the package**, not a Unity project. Consequence: **`.meta` files MUST be
  committed**, and `.gitignore` is intentionally narrow (OS/editor cruft, nested test-project
  `Library/`/`Temp/`-style folders, beads/Dolt internals). `Tests~` is explicitly re-included by
  negation patterns because the blanket `*~` and `*.csproj` rules would otherwise drop it;
  `Tests~/bin/` and `Tests~/obj/` are ignored via a nested `Tests~/.gitignore`.

## Issue tracking — beads (`bd`)

- Use `bd` for **ALL** task tracking — do NOT use TodoWrite/TaskCreate/markdown TODO lists. Use
  `bd remember "…"` for persistent cross-session knowledge — do NOT create MEMORY.md files (there
  is already a memory about the Unity test bed).
- Run `bd prime` for full workflow context. The project's `.claude/settings.json` wires `bd prime`
  to run automatically at session start and before context compaction (SessionStart + PreCompact
  hooks, empty matcher = always), so agent tooling that honors those hooks keeps beads context
  alive; re-run it manually after a compaction otherwise.
- Core commands: `bd ready` (find available work), `bd show <id>`, `bd update <id> --claim`,
  `bd close <id> --reason="…"`, `bd dolt push` (push beads data). The project epic is **`2g8`**;
  children are numbered `2g8.N`. Beads state lives in `.beads/issues.jsonl` and is committed to
  git, so it travels with `git push` (no separate Dolt remote required).

### Session close protocol (mandatory)

Work is NOT complete until `git push` succeeds. At the end of any working session:

1. File issues for remaining/follow-up work.
2. Run quality gates if code changed (both test loops below).
3. Update issue status — close finished work, update in-progress items.
4. Push: `git pull --rebase`, `bd dolt push`, `git push`, then `git status` must show
   "up to date with origin" (`## main...origin/main`).
5. Clean up (clear stashes, prune remote branches) and verify everything is committed AND pushed.
6. Hand off context for the next session.

Never stop before pushing (it strands work locally) and never leave the push to someone else; if
the push fails, resolve and retry until it succeeds.

## Tests — one test set, two runners

Shared NUnit tests in `Tests/*.cs` compile under **both** runners; there is exactly one set of
test source files.

- **Offline (fast, no Unity):** `cd Tests~ && dotnet test`. `Tests~/LpcLogic.Offline.csproj`
  (net10.0) **links** — not copies — the pure Runtime sources (`LpcClip.cs`, `LpcClipMath.cs`,
  `LpcSliceMath.cs`, `LpcCustomAnims.cs`, `LpcPreviewMath.cs`, `LpcBodyType.cs`, `LpcCategory.cs`,
  `LpcCredits.cs`, `LpcSourceLayout.cs`) plus all `../Tests/*.cs` via a non-recursive glob, against
  a minimal `Tests~/Shim/UnityEngine.cs` (just `Mathf`, `Rect`, `Sprite`, `[Tooltip]`). The `~`
  suffix hides the folder from Unity's import pipeline so the fake `UnityEngine` namespace never
  collides with the real one.
- **In Unity:** EditMode assembly **`TCLowe.Lpc.EditMode.Tests`**, run in the test bed project
  **Ultima4_2d** (`C:/UnityProjects/Ultima4_2d`) — either through its Test Runner window or via the
  MCP-for-Unity bridge: `run_tests(mode="EditMode", assembly_names="TCLowe.Lpc.EditMode.Tests")`
  then `get_test_job(job_id, wait_timeout=30, include_failed_tests=true)`. Ultima4_2d references
  this package via `manifest.json` (`file:D:/Projects/LPC-Unity-Character-Creator`) and has
  `testables` set; the package's `Tests/` folder must be present for tests to appear.
- NUnit is pinned to **3.x** (3.14.0) to match Unity's bundled test framework so classic asserts
  (`Assert.AreEqual`, …) compile identically in both runners.

### Where new logic and tests go

- Put bug-prone logic in a **pure Runtime static** and test it in a shared `Tests/*.cs` test.
  Shared tests must stay pure: only `Lpc` types, NUnit, and `Sprite[]` as opaque arrays — never
  `new Sprite()`; use `new Sprite[n]` (the shim `Sprite` is an empty marker class; resolution logic
  only cares about array identity/length).
- When a shared test needs a **new** pure Runtime file, add a
  `<Compile Include="..\Runtime\X.cs" …/>` line to `Tests~/LpcLogic.Offline.csproj`. If a linked
  file starts using a new UnityEngine symbol, add the minimal stub to `Tests~/Shim/UnityEngine.cs`.
- Anything MonoBehaviour/asset/filesystem-bound (real GameObjects, Sprites, Textures,
  ScriptableObjects, `Lpc.Editor` types) goes in **`Tests/Integration/`** — the offline csproj's
  non-recursive `..\Tests\*.cs` glob skips that subfolder, while Unity's asmdef compiles it
  recursively. The EditMode asmdef references the Runtime, Editor, and Samples assemblies. Copy
  the pattern of the existing Integration suites (animation pathway, coverage, recolor, …).

## Unity MCP bridge (driving the editor from an agent)

- Server: **MCP-for-Unity** (CoplayDev) at `http://127.0.0.1:8080/mcp` (streamable-http),
  registered with the coding agent at user scope. Native tools (`run_tests`, `execute_code`,
  `manage_editor`, `manage_camera`, `refresh_unity`, `read_console`) only load after restarting
  the agent client; until then the server can be driven with raw JSON-RPC over `/mcp`
  (initialize → notify → `tools/call`).
- **Recompile loop** after editing scripts: `refresh_unity(mode=force, scope=all, compile=request,
  wait_for_ready=true)`, a second `refresh_unity(if_dirty, scripts, none, wait_for_ready)` to let
  the domain reload settle, then `read_console(types=["error"])`. Use scope **`all`** to IMPORT new
  files — scope `scripts` recompiles but won't register a brand-new `.cs`/`.meta`, so new tests
  won't appear.
- `execute_code` runs C# as a method BODY: no `using` directives (fully-qualify, e.g.
  `UnityEditor.AssetDatabase`), and use `UnityEngine.Object.DestroyImmediate`, never bare `Destroy`.
- Visual checks: `manage_camera(action=screenshot, camera="MirrorCamera", include_image=true)`
  shows the character; `capture_source="game_view"` (play mode) shows the full UI; `scene_view`
  will not show screen-space canvases.

## Scene-editing and re-import gotchas

- **Never run a script that calls `EditorSceneManager.OpenScene(Single)` on a scene with unsaved
  edits — it discards them.** `MirrorSetup` (menu: Tools/LPC/Setup Mirror) does exactly this; it
  rebuilds `MirrorCharacter` from `CatalogStarter` and re-saves.
- **Re-importing the catalog updates LayerSet assets, but already-built characters keep stale
  baked clip references** (`SetLayer` copies array references). Slots with an `AppearanceSelector`
  refresh via `SetLayer`; slots without one (e.g. the head) don't — re-run Setup Mirror to rebuild
  against the current catalog after asset regeneration.
- A world-space sprite cannot be a child of a screen-space UI element. The mirror is a
  render-texture rig (`MirrorCamera` → `MirrorRT` → `MirrorImage` RawImage); group
  `MirrorCharacter`+`MirrorCamera` under a `MirrorRig` to move them together; move the oval by
  moving the RawImage.
- Edit-mode changes made via `execute_code` aren't saved unless you call `MarkSceneDirty` +
  `SaveScene`; a domain reload reverts unsaved runtime-built state.
- Unchanged `File.Copy` output doesn't trigger a reimport — force it with
  `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)` when regenerating catalog files.

## Design rules that shape every change

- **Hide-on-missing-clip:** a layer with no frames for the active clip hides (`sprite = null`)
  rather than showing a stale pose. **Coverage is surfaced, never restricted** — UI flags gaps
  (`SupportedClips`/`MissingClips`/`SlotsMissingClip`, amber `*` in the preview, "(6/15 anims)"
  labels) instead of removing animations from the user's selection.
- **Recolor across ALL clips** (`LpcRecolor.RecolorClips` + `LpcCharacter.SetLayerClips`), never
  just walk, or the color reverts on non-walk animations.
- Frame index = `dir * framesPerDir + frame`; every animation has its own grid — never assume the
  9x4 walk layout. The legacy `frames` field gets walk ONLY.

## Git / commit conventions

- **Multi-line commit messages: write to a file and `git commit -F <file>`.** PowerShell
  here-strings (`@'…'@`) break on embedded double-quotes and `*` — this is the #1 repeated failure
  in this repo.
- `.meta` files MUST be committed (UPM package). Unity generates them on import — run `git add -A`
  after a refresh to pick them up.
- The repo normalizes LF→CRLF on Windows; those warnings are harmless.
- Commits made by an automated coding agent end with its `Co-Authored-By:` trailer. Push at
  session end (see protocol above).
- **Non-interactive shell hygiene** for agents/scripts: use force flags so file ops never hang on
  confirmation prompts — `cp -f` / `mv -f` / `rm -f` (`rm -rf` / `cp -rf` for recursive), `ssh`/`scp`
  with `-o BatchMode=yes`, `apt-get -y`, `HOMEBREW_NO_AUTO_UPDATE=1` for brew.

## Maintainer utilities

- `tools/Build_Art_Release.ps1` rebuilds the `LpcArt-full.zip` art-release artifact (published as
  `gh release create art-vYYYYMMDD … -R TCLowe1982/LPC-Unity-Character-Creator`); it is a
  maintainer tool, not shipped to package consumers. Full pipeline details in
  `art-pipeline-and-licensing.md`.
- Launcher/start batch scripts, if any are added, are named `<Project_Name>_Start.bat` — project
  name first (underscore-separated), then `_Start.bat` — so what a script launches is readable
  from the beginning of the filename.
