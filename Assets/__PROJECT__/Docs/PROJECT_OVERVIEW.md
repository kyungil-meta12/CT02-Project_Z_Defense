# Project Overview

## Purpose

This is the active project overview, design boundary, and shared API summary.

## Game Concept

| Item | Value |
| --- | --- |
| Title | Z-Defence / Project Z Defense |
| Platform | Cross-platform mobile and Windows PC, portrait-oriented control target |
| Genre | Zombie tower defense |
| View | 3D isometric view |
| References | Project Zomboid, Wicked Defense, idle kingdom defense-style loops |

Core concept:

- The player defends a fortress from zombie waves.
- Friendly survivor NPCs can support fort defense, repair, and turret-related progression.
- Waves spawn normal zombies and special boss zombies.
- Turret progression and weapon variation are data-driven through ScriptableObjects.

## Core Systems

| System | Intent |
| --- | --- |
| Fortress defense | Survivors, obstacles, defense lines, turrets, and repair/rebuild flow protect the base. |
| Wave system | Zombie waves advance by kill count and spawn rules. |
| Damage system | Shared `IDamageable` API connects zombies, obstacles, projectiles, skills, and feedback. |
| Turret system | ScriptableObject-driven stats, evolution, VFX, projectile scaling, placement, and runtime UI. |
| Economy and rewards | Planned coin/parts drops, upgrades, and drone automation. |
| Boss enemies | Screamer, Boomer, and Charger/Tank-style behavior variations. |

## Responsibility Boundaries

| Owner Area | Responsibility | Avoid Owning |
| --- | --- | --- |
| Game core/system | Waves, spawn rules, target policy, damage rules, health/death/reward flow, turret stat data, upgrade rules, global game state. | Presentation-only VFX implementation details unless API rules require it. |
| Turret/VFX | Turret placement presentation, aim/fire presentation, projectile VFX, muzzle/hit effects, sounds, recoil, turret test scene, VFX profiles. | Final damage formula, upgrade balance, wave balance. |
| Zombie/map/animation/behavior | Zombie prefab setup, movement, animation, behavior trees/state logic, map pathing, spawn/goal points, boss behavior. | Turret stat balance or projectile VFX ownership. |

When boundaries are unclear, prefer adding small project-level APIs or adapters rather than parallel systems.

## Shared API Contracts

### IDamageable

```csharp
public interface IDamageable
{
    float TotalHp { get; }
    float CurrHp { get; }
    bool IsAlive { get; }
    void TakeDamage(DamageInfo damageInfo);
}
```

Rules:

- Implementations own HP mutation.
- External systems should read state and call `TakeDamage` only.
- Dead objects must report `IsAlive == false` before target selection can include them again.

### ITargetable

```csharp
public interface ITargetable
{
    Transform TargetPoint { get; }
    bool IsAlive { get; }
}
```

Use when a system needs an explicit target point separate from the root transform.

### IProjectileHitHandler

```csharp
public interface IProjectileHitHandler
{
    void OnProjectileHit(GameObject target);
}
```

Use for projectile collision hooks that need extra handling outside direct damage application.

## Current Runtime Integration

- Turret projectiles use `ProjectileDamageDealer` to apply damage to `IDamageable` targets.
- `ProjectileHitDetector` combines tracked target checks, trigger/collision callbacks, and movement raycast checks to reduce fast projectile misses.
- Projectile damage skips null, dead, and already-hit targets.
- `NormalZombie` and `BossZombie` implement `IDamageable` and update death state before they should be selected again.
- `DamagePopupSpawner.SpawnDamage` displays world-space damage feedback.
- `GameManager` owns wave kill count and defense-line/survivor/obstacle registration.
- `Survivor` handles idle repair search, repair movement, repair execution, defense-line retreat/return, and obstacle vault during defense movement.

## Repository And Asset Rules

- Root project repository owns code, scenes, project-level wrappers, and settings assets.
- `Assets/__PROJECT__/Private Assets` is a separate external/private asset area.
- Root `git status` may not show nested Private Assets repository changes.
- If Private Assets are modified, check status inside that folder separately.
- Prefer project-level wrappers, duplicated prefabs, profiles, or adapters before editing original external assets.
- Preserve `.meta` GUIDs when moving Resources or referenced assets.
