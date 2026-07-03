# Project Instructions for AI Agents

This file provides instructions and context for AI coding agents working on this project.

<!-- BEGIN BEADS INTEGRATION v:1 profile:minimal hash:ca08a54f -->
## Beads Issue Tracker

This project uses **bd (beads)** for issue tracking. Run `bd prime` to see full workflow context and commands.

### Quick Reference

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --claim  # Claim work
bd close <id>         # Complete work
```

### Rules

- Use `bd` for ALL task tracking — do NOT use TodoWrite, TaskCreate, or markdown TODO lists
- Run `bd prime` for detailed command reference and session close protocol
- Use `bd remember` for persistent knowledge — do NOT use MEMORY.md files

## Session Completion

**When ending a work session**, you MUST complete ALL steps below. Work is NOT complete until `git push` succeeds.

**MANDATORY WORKFLOW:**

1. **File issues for remaining work** - Create issues for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **PUSH TO REMOTE** - This is MANDATORY:
   ```bash
   git pull --rebase
   bd dolt push
   git push
   git status  # MUST show "up to date with origin"
   ```
5. **Clean up** - Clear stashes, prune remote branches
6. **Verify** - All changes committed AND pushed
7. **Hand off** - Provide context for next session

**CRITICAL RULES:**
- Work is NOT complete until `git push` succeeds
- NEVER stop before pushing - that leaves work stranded locally
- NEVER say "ready to push when you are" - YOU must push
- If push fails, resolve and retry until it succeeds
<!-- END BEADS INTEGRATION -->


> **Deep context for AI agents lives in** `Documentation~/expert-guide/` (model-agnostic, generated from a
> full-repo Ledger Pattern sweep), with the Claude-side wrapper at
> `.claude/skills/lpc-character-creator-expert/SKILL.md`. Read them first when working on this repo.

## Build & Test

This is a Unity UPM package (`com.tclowe.lpc-character-creator`, Unity 2022.3+). Two test runners
share one set of NUnit tests:

```bash
# Offline pure-logic tests (no Unity needed):
cd Tests~ && dotnet test

# In Unity: via the MCP bridge against the test bed project Ultima4_2d
#   run_tests(mode="EditMode", assembly_names="TCLowe.Lpc.EditMode.Tests")
```

`Tests/*.cs` = shared (offline + Unity); `Tests/Integration/*.cs` = Unity-only (real GameObjects/
Sprites); `Tests~/` = the offline `dotnet` project (hidden from Unity by the `~`). When adding a pure
Runtime file a shared test needs, add a `<Compile Include>` line to `Tests~/LpcLogic.Offline.csproj`.

## Architecture Overview

A layered LPC character = a stack of `SpriteRenderer` layers driven to the same `(clip, dir, frame)`
each tick. **Animations are code-driven — no Unity `.anim` clips.** Pure arithmetic lives in Runtime
statics (`LpcClipMath`, `LpcSliceMath`, `LpcBodyType`, `LpcCategory`, `LpcCredits`) so it unit-tests
offline. The Editor auto-slices imported LPC sheets into `LpcLayerSet.clips`; the runtime indexes
`dir*framesPerDir + frame` per the active clip. Full map: `Documentation~/expert-guide/architecture.md`.

## Conventions & Patterns

- Put bug-prone logic in a **pure Runtime static** and test it **offline** (`dotnet test`); keep
  MonoBehaviour/asset glue in `Tests/Integration/`.
- Frame index = `dir * framesPerDir + frame`; each animation has its own grid (see `LpcClips`, 15 ULPC
  animations whose names match the on-disk PNG files).
- A layer with no frames for the active clip **holds walk frame 0** (standing, same direction);
  layers lacking walk too hide (no stale pose). The UI surfaces coverage gaps.
- **Recolor across ALL clips** (`RecolorClips`/`SetLayerClips`), not just walk.
- Multi-line commit messages: **`git commit -F <file>`** (PowerShell here-strings break on quotes/`*`).
- `.meta` files are committed (UPM). Don't run `OpenScene(Single)` scripts on unsaved scenes.
- More gotchas (MCP bridge, scene editing, re-import staleness):
  `Documentation~/expert-guide/workflow-and-conventions.md`.
