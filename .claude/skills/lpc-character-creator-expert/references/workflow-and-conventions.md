# Workflow & Conventions

## Task tracking — beads (`bd`)

- Use `bd` for ALL tracking (NOT TodoWrite/markdown). `bd ready` / `bd show <id>` / `bd update <id>
  --claim` / `bd close <id> --reason="…"`. The epic is **`2g8`**; children are `2g8.N`.
- `bd remember "…"` for cross-session knowledge (there is a memory about the Unity test bed). `bd prime`
  after compaction.
- Session close: commit code, `git push`. Beads state lives in `.beads/issues.jsonl` (committed) — no
  separate Dolt remote is configured, so it travels via git.

## Tests — dual runner

- **Shared NUnit tests** in `Tests/*.cs` run **both** offline and in Unity. Keep them pure (only `Lpc`
  types, `NUnit`, and `Sprite[]` as opaque arrays — never `new Sprite()`; use `new Sprite[n]`).
- **Offline:** `cd Tests~ && dotnet test`. NUnit is pinned to **3.x** to match Unity's bundled framework
  so classic asserts (`Assert.AreEqual`, …) compile in both. When you add a new pure Runtime file that a
  shared test needs, add a `<Compile Include="..\Runtime\X.cs" …/>` line to `Tests~/LpcLogic.Offline.csproj`.
- **Unity-only tests** (real GameObjects/Sprites/Textures, Editor types) go in `Tests/Integration/` — the
  offline csproj's non-recursive `..\Tests\*.cs` glob skips that subfolder; Unity's asmdef compiles it
  recursively. The EditMode asmdef references Runtime, Editor, and Samples.
- **In Unity:** `run_tests(mode="EditMode", assembly_names="TCLowe.Lpc.EditMode.Tests")` then
  `get_test_job(job_id, wait_timeout=30, include_failed_tests=true)`.

## The Unity MCP bridge (how to actually run things)

- **Test bed:** the game project **Ultima4_2d** at `C:/UnityProjects/Ultima4_2d` references this package
  via `manifest.json` (`file:D:/Projects/LPC-Unity-Character-Creator`) and has `testables` set, so the
  EditMode tests show in its Test Runner. It must have the package's `Tests/` for tests to appear.
- **Server:** MCP-for-Unity (CoplayDev) registered with Claude Code at USER scope,
  `http://127.0.0.1:8080/mcp` (streamable-http). **Native tools (`run_tests`, `execute_code`,
  `manage_editor`, `manage_camera`, `refresh_unity`, `read_console`) only load after a Claude Code
  restart** — until then you can drive the server with raw JSON-RPC over `/mcp` (initialize → notify →
  `tools/call`).
- **Recompile loop:** after editing scripts, `refresh_unity(mode=force, scope=all, compile=request,
  wait_for_ready=true)`, then a second `refresh_unity(if_dirty, scripts, none, wait_for_ready)` to let the
  domain reload settle, then `read_console(types=["error"])`. **Use scope `all` to IMPORT new files**
  (scope `scripts` recompiles but won't register a brand-new `.cs`/`.meta`, so new tests won't appear).
- **execute_code** runs C# as a method BODY: no `using` directives (fully-qualify, e.g.
  `UnityEditor.AssetDatabase`), and use `UnityEngine.Object.DestroyImmediate` (no bare `Destroy`).
- **Visual checks:** `manage_camera(action=screenshot, camera="MirrorCamera", include_image=true)` to see
  the character; `capture_source="game_view"` (play mode) to see the full UI; `scene_view` won't show
  screen-space canvases.

## Scene-editing gotchas (these bit us)

- **Never run a script that does `EditorSceneManager.OpenScene(Single)` on a scene with unsaved edits —
  it discards them.** `MirrorSetup` (Tools/LPC/Setup Mirror) does this; it rebuilds `MirrorCharacter` from
  `CatalogStarter` and re-saves.
- **Re-importing the catalog updates LayerSet ASSETS, but already-built characters keep stale baked
  clips.** Slots with an `AppearanceSelector` get refreshed (SetLayer); slots WITHOUT one (e.g. the head)
  don't — re-run Setup Mirror to rebuild against the current catalog.
- A **world-space sprite cannot be a child of a screen-space UI element**. The mirror is a render-texture
  rig (`MirrorCamera` → `MirrorRT` → `MirrorImage` RawImage); group `MirrorCharacter`+`MirrorCamera` under
  a `MirrorRig` to move them together; move the oval by moving the RawImage.
- Edit-mode changes made via `execute_code` aren't saved unless you `MarkSceneDirty` + `SaveScene`; a
  domain reload reverts unsaved runtime-built state.

## Git / commit conventions

- **Multi-line commit messages: write to a file and `git commit -F <file>`.** PowerShell here-strings
  (`@'…'@`) break on embedded double-quotes and `*`. (This is the #1 repeated failure.)
- The repo normalizes LF→CRLF on Windows — the warnings are harmless.
- `.meta` files MUST be committed (it's a UPM package). Unity generates them on import — `git add -A`
  after a refresh to pick them up.
- End commit messages with: `Co-Authored-By: Claude …`. Push at session end; verify `## main...origin/main`.
