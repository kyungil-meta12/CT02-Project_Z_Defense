# Agent Instructions

- When reading files that may contain Korean text from PowerShell, force UTF-8 output and file decoding so comments and strings are not misread:
  ```powershell
  [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
  Get-Content -LiteralPath "path\to\file" -Encoding UTF8
  ```

## Required Project Context

- Before starting project work, read the minimal common context and then only the task-specific Docs listed in `Assets/__PROJECT__/Docs/README.md`.

Always read:

1. `Assets/__PROJECT__/Docs/README.md`
   - Docs index, fast-start reading order, and task-specific document map.

2. `Assets/__PROJECT__/Docs/TEAM_CODING_CONVENTION.md`
   - Code style, Unity conventions, MemoryPool/PoolObject rules, logging, and performance/GC guidelines.

3. `Assets/__PROJECT__/Docs/PROJECT_STRUCTURE.md`
   - Project folder map, runtime areas, asset ownership, and where new work should be placed.

Task-specific examples:

- Survivor, obstacle, zombie spawn, and defense-line work: read `GAMEPLAY_RUNTIME_FLOW.md` and `SCENE_SETUP.md`.
- Turret, combat, projectile, placement, and damage popup work: read `TURRET_SYSTEM.md` and `COMMON_SYSTEMS.md`.
- Architecture or responsibility-boundary decisions: read `PROJECT_OVERVIEW.md`.

## High Priority Rules

- Follow `Assets/__PROJECT__/Docs/TEAM_CODING_CONVENTION.md` for all code changes.
- When writing a new method, add a brief Korean comment explaining its purpose.
- Write debugging logs in Korean.
- When generating code, consider optimizations appropriate for an idle mobile game.
- After generating code, simulate and inspect possible edge cases before finishing.
- For turret, combat, projectile, placement, and damage popup work, consult `Assets/__PROJECT__/Docs/TURRET_SYSTEM.md` first.
- For survivor, obstacle, zombie spawn, defense-line, scene setup, and shared runtime system work, consult the relevant document under `Assets/__PROJECT__/Docs` first.
- When design intent or responsibility boundaries are unclear, use `Assets/__PROJECT__/Docs/PROJECT_OVERVIEW.md` as the source of truth.
- Avoid direct modification of Private Assets originals when possible; prefer project-level wrappers, profiles, adapters, or duplicated prefabs under the project folder.

## Working Procedure

- Before editing, identify the target system owner and responsibility boundary from the project documents.
- Check the current implementation before proposing a design; prefer extending existing project-level APIs over adding parallel systems.
- Preserve unrelated local changes. Do not revert scene, prefab, asset, or script changes unless explicitly requested.
- If a task touches Unity YAML assets, preserve existing `guid`, `fileID`, component order, and prefab references unless the change requires otherwise.
- When adding serialized fields to scripts that are already attached to prefabs, update the relevant prefab or asset values when practical.
- After completing code changes, check whether related `Assets/__PROJECT__/Docs` documents need updates and apply or report any required documentation changes.
- Prefer small, focused changes that can be compiled and play-mode tested independently.

## Unity Code Rules

- Avoid `FindGameObjectsWithTag`, repeated `FindObjectOfType`, LINQ, closures, string formatting, or new collection allocation in hot paths such as `Update`, AI ticks, projectile movement, targeting, damage, and UI refresh loops.
- Cache components, animator hashes, reusable lists, target references, and ScriptableObject-derived runtime values when they are used repeatedly.
- Use throttled polling, event registration, object pooling, and NonAlloc Unity APIs for idle-mobile gameplay loops.
- Use `Destroy` instead of `DestroyImmediate` during runtime.
- Runtime debug logs must be actionable and written in Korean; avoid logs that can spam every frame.
- Prefer squared-distance checks over `Vector3.Distance` when only comparing distance.
- Keep ScriptableObject data as configuration and runtime mutable state inside runtime controllers or instances.

## Edge Case Checklist

- After code changes, mentally simulate null references, disabled objects, destroyed objects, duplicate registration, repeated enable/disable, empty collections, unreachable NavMesh paths, invalid animator parameters, and zero or negative inspector values.
- For pooled objects, verify `OnEnable`, `OnDisable`, reset, reservation, and event subscription cleanup paths.
- For UI and health systems, verify max/current value clamping, missing UI references, death-state behavior, and repeated damage or repair calls.
- For AI and targeting, verify target loss, target death, blocked paths, timeout behavior, and multiple actors selecting the same target.
- For fracture, VFX, projectile, and popup work, verify prefab references, read/write mesh requirements, pooling fallback, and runtime allocation spikes.

## Verification

- Run a compile check when script changes are made, preferably:
  ```powershell
  dotnet build Assembly-CSharp.csproj --no-restore
  ```
- Report whether the build passed, and separate new issues from pre-existing warnings.
- Report whether related Docs changes were checked after code changes.
- If Unity Editor-only validation is required but cannot be run from the terminal, state the remaining play-mode checks explicitly.
