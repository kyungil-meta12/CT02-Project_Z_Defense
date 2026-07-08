# Project Structure

## Purpose

This document is the shortest map for finding where project code, scenes, prefabs, ScriptableObjects, and external assets belong.

## Top-Level Folders

| Path | Role | Notes |
| --- | --- | --- |
| `Assets/__PROJECT__/Prefabs` | Runtime gameplay prefabs and their local scripts/data | Zombies, boss zombies, obstacles, survivors, spawner, HP UI, MemoryPool prefab. |
| `Assets/__PROJECT__/Scripts` | Shared project-level runtime systems | `GameManager`, `MemoryPool`, `PoolObject`, `DamagePopupSpawner`, DNP popup backend, camera, item systems. |
| `Assets/__PROJECT__/Scenes` | Main scene, test scenes, scene-owned feature work | `Main.unity` is the current integration scene. `KKW/Turret_Scene` owns turret-related feature assets. |
| `Assets/__PROJECT__/Resources` | Runtime `Resources.Load` targets | Keep paths stable. Current UI resources live under `Resources/UI`. |
| `Assets/__PROJECT__/Public Assets` | Project-owned shared art/font assets | Safe to reference from project systems. |
| `Assets/__PROJECT__/Private Assets` | External or purchased source assets | Avoid direct edits. Prefer wrappers, adapters, copied prefabs, or project-level profiles. |
| `Assets/__PROJECT__/Docs` | Lightweight project documentation | Markdown-only operational docs. |

## Main Runtime Areas

| Area | Main Files | Responsibility |
| --- | --- | --- |
| Game state and defense lines | `Scripts/Singleton/GameManager/GameManager.cs` | Wave kill count, obstacle/survivor registration, defense-line breach/restore, repair target selection. |
| Zombies | `Prefabs/Damageable/NormalZombie`, `Prefabs/Damageable/BossZombie` | Movement, attack, damage, death, pool return, boss behavior. |
| Obstacles and repair | `Prefabs/Damageable/Obstacle`, `Prefabs/Survivor` | Obstacle HP/fracture/repair reservation, survivor retreat/return/repair/vault. |
| Obstacle placement | `Prefabs/Damageable/Obstacle/ObstacleBuild*`, `Prefabs/Damageable/Obstacle/ObstaclePlacement*` | Fixed defense-line obstacle/gate slot placement and rebuild UI. |
| Spawning | `Prefabs/ZombieSpawner`, `Scenes/KKW/Turret_Scene/SO/Zombie Wave Spawn Profile`, `Scenes/KKW/Turret_Scene/SO/Zombie_Specs` | `ZombieWaveSpawnProfileSO` driven wave ranges, spawn counts, weighted normal entries, boss spawn schedules, and runtime stat/reward multipliers. Zombie specs own only baseline stats and random variance. |
| Turrets | `Scenes/KKW/Turret_Scene` | Turret definitions, stats, evolution, placement, projectile damage, VFX profile data. |
| Common feedback | `Scripts/DamagePopup*`, `Resources/UI` | World-space DNP damage number policy, spawning, sorting, and runtime profiling settings. |
| Status effects | `Scripts/StatusEffects` | Shared status-effect visuals and per-target runtimes such as `FrostStatusRuntime` and `PoisonStatusRuntime`. |
| Targeting filters | `Scripts/Targeting` | Project-owned target candidate filter interfaces and reusable filter components. |
| Pooling | `Scripts/Singleton/MemoryPool`, `Scripts/PoolObject` | Runtime object reuse and pool containers. |

## Where To Put New Work

| New Work | Preferred Location |
| --- | --- |
| Core gameplay rule shared by systems | `Assets/__PROJECT__/Scripts` |
| A prefab-specific behavior | Near the prefab under `Assets/__PROJECT__/Prefabs/...` |
| Obstacle/gate placement and defense-line slot behavior | `Assets/__PROJECT__/Prefabs/Damageable/Obstacle` |
| Turret, projectile, placement, skill, or turret SO work | `Assets/__PROJECT__/Scenes/KKW/Turret_Scene/...` until the turret module is moved to a shared folder |
| Turret special attack profiles | `Assets/__PROJECT__/Scenes/KKW/Turret_Scene/SO/AttackProfiles/...` |
| Turret-owned status effect VFX prefabs | `Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Prefabs/Status Effect/...` |
| Runtime-loaded UI settings or prefabs | `Assets/__PROJECT__/Resources/UI` only when `Resources.Load` is required |
| Documentation | `Assets/__PROJECT__/Docs` |
| External asset adaptation | Project wrapper/profile first; avoid editing `Private Assets` originals |

## Current Structural Notes

- Some production scripts currently live under scene folders, especially turret systems under `Scenes/KKW/Turret_Scene/Scripts`.
- Turret-owned status effect VFX, including Frost freeze visuals and Poison aura/burst visuals, currently live under `Scenes/KKW/Turret_Scene/Prefabs/Status Effect`.
- Do not move scripts, prefabs, or ScriptableObjects casually. Unity `.meta` GUID stability is more important than folder tidiness.
- Scene YAML changes must preserve existing `guid`, `fileID`, prefab instance links, component order, and serialized references unless the task explicitly requires a structural change.
- If code and scene setup disagree, inspect the current scene/prefab before adding a parallel system.

## Required Reading By Task

| Task Type | Read First |
| --- | --- |
| Any project work | `README.md`, `TEAM_CODING_CONVENTION.md`, this document |
| Any code change | `TEAM_CODING_CONVENTION.md`, this document, then task-specific Docs |
| Ownership or architecture decision | `PROJECT_OVERVIEW.md`, this document |
| Turret/projectile/placement/damage popup | `TURRET_SYSTEM.md`, `COMMON_SYSTEMS.md` |
| Survivor/obstacle/defense line/spawner | `GAMEPLAY_RUNTIME_FLOW.md`, `SCENE_SETUP.md` |
| Scene or prefab reference change | `SCENE_SETUP.md` |
