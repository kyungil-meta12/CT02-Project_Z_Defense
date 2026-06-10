# Team Coding Convention

## Purpose

This is the active coding and Unity workflow convention for project work.

## C# Style

| Item | Rule |
| --- | --- |
| Classes, structs, enums, methods, ScriptableObject types | PascalCase |
| Fields, parameters, enum values | camelCase |
| Parameter conflict with field | Add trailing `_` to the parameter name |
| Static variables | PascalCase |
| Constants | UPPER_CASE |
| Properties | PascalCase |
| Attributes and fields | Keep on one horizontal line when practical |
| Braces | Always use braces for conditionals and loops |
| Brace style | Allman style |
| Hungarian notation | Do not use |
| Lambdas | Use only when clearly justified |

Example:

```csharp
void SetObjectType(Type objType_)
{
    objType = objType_;
}
```

## Method Comments And Logs

- When writing a new method, add a brief Korean comment explaining its purpose.
- Runtime debug logs must be Korean, actionable, and not spammed every frame.
- Avoid debug logs in hot paths unless explicitly debugging and throttled.

## Unity Editor Rules

- GameObject and prefab names should use PascalCase.
- Runtime-spawned objects should be grouped under type-specific container parents where practical.
- Preserve `.meta` GUIDs when moving assets.
- Do not casually reorder serialized Unity components or prefab references.

## MemoryPool And PoolObject Rules

| Rule | Requirement |
| --- | --- |
| Singleton teardown | In `OnDestroy`, set `Inst = null` only when `Inst == this`. |
| Pool return | Before pushing to the origin stack, guard against missing/null `OriginStack`. |
| Input safety | Guard null prefabs in `GetInstance`, `CreateInstance`, and prewarm paths. |
| Type mismatch | If `GetComponent<T>` fails after spawning, log the cause immediately. |
| Runtime destruction | Use `Destroy`, not `DestroyImmediate`, during runtime. |
| Reset timing | Every pooled object must reset mutable state before use. |
| Spawn order | Prefer `Pop -> position/data setup -> Reset/Init -> SetActive(true)`. |

`MemoryPool.CreateInstance` XML docs should state:

- Creates or reuses an instance but does not return a component.
- Creates a new instance if the pool is empty.
- Prefab must inherit `PoolObject`.
- `OnSpawn()` is called when the instance is returned.

`MemoryPool.GetInstance<T>` XML docs should state:

- Returns a component of type `T`.
- Creates a new instance if the pool is empty.
- Prefab must inherit `PoolObject`.
- `OnSpawn()` is called when the instance is returned.
- Include one usage example in `<para>`.

## Performance And GC Rules

Hot paths include:

- `Update`
- AI ticks
- projectile movement
- targeting
- damage loops
- UI refresh loops

Avoid in hot paths:

- LINQ
- closures
- repeated `FindObjectOfType`
- `FindGameObjectsWithTag`
- allocating Unity APIs such as `Physics.OverlapSphere`
- string formatting
- new temporary collections
- unthrottled logging

Prefer:

- cached components
- cached animator hashes
- cached ScriptableObject-derived runtime values
- reusable lists and arrays
- NonAlloc physics APIs
- squared-distance comparisons
- event registration
- throttled polling
- object pooling

## Collaboration Rules

- Share main scene work before and after changing it.
- Keep changes small and independently testable.
- Preserve unrelated local changes.
- Avoid direct modification of `Private Assets` originals when a project-level wrapper, profile, adapter, or copied prefab can solve the problem.