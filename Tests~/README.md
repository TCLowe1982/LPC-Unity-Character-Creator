# LPC test bridge

The clip-system logic (frame indexing, walk/loop cycling, one-shot completion, the 17-clip
registry, per-animation frame resolution) is pure arithmetic with no `MonoBehaviour`
dependency, so it can be tested two ways from **one** set of NUnit test files in [`../Tests`](../Tests):

| Runner | How | Needs Unity? |
| --- | --- | --- |
| **Offline** (`dotnet test`) | This folder. Links the pure runtime sources + a tiny UnityEngine shim. | No |
| **Unity Test Runner** | `Tests/TCLowe.Lpc.EditMode.Tests.asmdef` (EditMode). | Yes |

## Run offline

```bash
cd Tests~
dotnet test
```

## How it works

- `Tests~/` ends in `~`, so the Unity editor ignores the whole folder — its `UnityEngine`
  shim never collides with the real engine.
- [`Shim/UnityEngine.cs`](Shim/UnityEngine.cs) stubs only the symbols the linked logic touches
  (`Mathf`, `Sprite`, `[Tooltip]`). Add to it if a linked file starts using a new type.
- [`LpcLogic.Offline.csproj`](LpcLogic.Offline.csproj) **links** `Runtime/LpcClip.cs` and
  `Runtime/LpcClipMath.cs` (single source of truth — not copied) plus the shared tests.
- NUnit is pinned to 3.x to match Unity's bundled test framework, so classic asserts compile
  in both runners.

## Scope

Shared tests cover the Unity-independent logic only. Behaviour that needs real
`MonoBehaviour`/`SpriteRenderer` instances (building a character, animator stepping) belongs in
PlayMode/EditMode tests that run inside Unity, kept separate from the shared offline set.
