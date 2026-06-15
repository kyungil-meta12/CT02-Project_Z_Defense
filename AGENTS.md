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
- Follow the documentation rules in the "Code Documentation Rules" section below for all classes and methods.
- Write Unity Inspector headers (`[Header]`) in Korean.
- Write debugging logs in Korean.
- When generating code, consider optimizations appropriate for an idle mobile game.
- After generating code, simulate and inspect possible edge cases and optimization risks before finishing.
- For turret, combat, projectile, placement, and damage popup work, consult `Assets/__PROJECT__/Docs/TURRET_SYSTEM.md` first.
- For survivor, obstacle, zombie spawn, defense-line, scene setup, and shared runtime system work, consult the relevant document under `Assets/__PROJECT__/Docs` first.
- When design intent or responsibility boundaries are unclear, use `Assets/__PROJECT__/Docs/PROJECT_OVERVIEW.md` as the source of truth.
- Avoid direct modification of Private Assets originals when possible; prefer project-level wrappers, profiles, adapters, or duplicated prefabs under the project folder.

## Code Documentation Rules

### Class Documentation
- When creating a new class, add a summary comment above the class declaration explaining its purpose and responsibility.
- Use C# XML documentation format (`/// <summary>`) for public classes.
- Use Korean single-line comment (`//`) for internal or utility classes.

### Method Documentation
- Every method must have a Korean comment directly above its declaration explaining its purpose.
- This rule applies to ALL methods without exception:
  - Unity lifecycle methods (`Awake`, `Start`, `Update`, `OnEnable`, `OnDisable`, etc.)
  - Public methods
  - Private helper methods
  - Static helper methods
  - Coroutines
  - Event handlers
- Write the comment in a single line starting with `//` followed by a clear action-oriented statement.
- Place the comment immediately before the method signature with no blank lines in between.

Example:
```csharp
// 게임 시작 시 필요한 컴포넌트를 초기화한다
private void Awake()
{
    // ...
}

// 포인터 위치에 해당하는 슬롯을 레이캐스트로 찾는다
private ObstacleBuildSlot FindSlot(Vector2 screenPosition, out RaycastHit hit)
{
    // ...
}
```

### Script Writing Procedure
Before writing a new script:
1. Read `AGENTS.md` and relevant task-specific Docs.
2. Create a mental or written checklist of required rules for the task:
   - Memory pool usage if object spawning is involved
   - Hot-path optimization requirements
   - Interface implementations needed
   - Edge cases to consider
3. Write class summary comment.
4. Write each method with its Korean purpose comment BEFORE writing the method body.
5. After completing the script, verify no method is missing its comment.

### Comment Verification Checklist
After writing or modifying a script, verify:
- [ ] Class has summary comment
- [ ] Every method (including Unity lifecycle, private, static) has a Korean purpose comment
- [ ] Inspector headers (`[Header]`) are written in Korean
- [ ] Runtime logs are written in Korean
- [ ] No GC allocation, `Find*`, or LINQ in hot paths
- [ ] Optimization risks checked, including garbage allocation, memory-heavy call frequency, and many-in-scene object scaling
- [ ] Edge cases considered (null, disabled, destroyed, duplicate registration, etc.)
- [ ] Related Docs updated if needed
- [ ] Build passes with `dotnet build Assembly-CSharp.csproj --no-restore`

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
- Inspector headers (`[Header]`) must be written in Korean so designers can understand serialized field groups quickly.
- Prefer squared-distance checks over `Vector3.Distance` when only comparing distance.
- Keep ScriptableObject data as configuration and runtime mutable state inside runtime controllers or instances.

## Edge Case Checklist

- After code changes, mentally simulate null references, disabled objects, destroyed objects, duplicate registration, repeated enable/disable, empty collections, unreachable NavMesh paths, invalid animator parameters, and zero or negative inspector values.
- During the same review, check optimization risks: garbage allocation, memory-heavy method call frequency, repeated component or object searches, string formatting in repeated paths, and whether the code remains safe when many copies of the object exist in the scene.
- For pooled objects, verify `OnEnable`, `OnDisable`, reset, reservation, and event subscription cleanup paths.
- For UI and health systems, verify max/current value clamping, missing UI references, death-state behavior, and repeated damage or repair calls.
- For AI and targeting, verify target loss, target death, blocked paths, timeout behavior, and multiple actors selecting the same target.
- For fracture, VFX, projectile, and popup work, verify prefab references, read/write mesh requirements, pooling fallback, and runtime allocation spikes.

## Verification

After script changes, complete the "Comment Verification Checklist" in the "Code Documentation Rules" section.

- Run a compile check when script changes are made, preferably:
  ```powershell
  dotnet build Assembly-CSharp.csproj --no-restore
  ```
- Report whether the build passed, and separate new issues from pre-existing warnings.
- Report whether related Docs changes were checked after code changes.
- If Unity Editor-only validation is required but cannot be run from the terminal, state the remaining play-mode checks explicitly.
