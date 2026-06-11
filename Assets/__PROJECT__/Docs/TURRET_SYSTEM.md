# Turret System

## Purpose

This is the active turret data, progression, placement, combat, projectile, and pooling summary.

## Ownership

The turret module covers:

- turret definitions
- stat profiles and stat growth
- turret level/evolution flow
- VFX profile selection
- projectile scale progression
- turret placement UI and base slots
- projectile damage handoff
- turret-related runtime UI
- turret and projectile VFX pooling hooks

The turret module does not own final wave balance or global reward rules unless explicitly connected through a shared API.

## Runtime Level Model

| Concept | Meaning |
| --- | --- |
| Tier level | Current level within a turret form. Drives current balancing, VFX, projectile scale, evolution requirement, and max cap. |
| Total level | Lifetime/display level across evolutions. Does not currently drive stat growth. |
| Max tier level | `TurretDefinitionSO.maxLevel`; `0` means unlimited. Positive values stop leveling at that form cap. |

Evolution resets tier level to `1` and preserves total level.

## Current Evolution Tree

| Turret | Role | Evolution |
| --- | --- | --- |
| Sentinel-01 | Base turret, tier 1-100 | Evolves at tier 100 into Sentry Pulse or Vector MG. |
| Sentry Pulse | First branch, fast-fire path, tier 1-200 | Evolves at tier 200 into Pulse Repeater. |
| Vector MG | First branch, high-damage path, tier 1-200 | Evolves at tier 200 into Vulcan Node. |
| Pulse Repeater | Final Sentry Pulse branch | Max tier 300. |
| Vulcan Node | Final Vector MG branch | Max tier 300. |

Recommended turret IDs use stable lower_snake_case:

- `sentinel_01`
- `sentry_pulse`
- `vector_mg`
- `pulse_repeater`
- `vulcan_node`

Do not use display names as stable IDs.

## Core ScriptableObjects

| Type | Role |
| --- | --- |
| `TurretDefinitionSO` | Top-level turret identity, prefab, base stat profile, growth, VFX progression, projectile scale progression, evolution progression, max level. |
| `TurretStatProfileSO` | Base combat values for tier level 1: damage, range, fire interval, projectile speed, projectile count, pierce count. |
| `TurretStatGrowthProfileSO` | Tier-level-based growth calculation using completed growth steps. |
| `TurretVFXProfileSO` | Visual/audio projectile data only: projectile prefab, muzzle VFX, muzzle duration, fire sound. No balance values. |
| `TurretVFXProgressionSO` | Selects active VFX profile by current tier level. |
| `TurretProjectileScaleProgressionSO` | Selects projectile scale by current tier level. |
| `TurretEvolutionProgressionSO` | Defines available evolutions and required tier levels. |
| `TurretShopEntrySO` | Defines placement UI slot data, cost, icon, definition, prefab override, preview prefab. |

## Stat And VFX Runtime Flow

1. `TurretDefinitionRuntimeController` receives or owns the current tier level.
2. It clamps level by evolution requirements and max level.
3. `TurretStatCalculator` calculates runtime stats from base and growth profiles.
4. `TurretStatProfileApplier` applies combat stats to the runtime turret components.
5. `TurretVFXProgressionSO` selects projectile/muzzle/audio data.
6. `TurretProjectileScaleProgressionSO` selects projectile scale.
7. Firing logic applies projectile prefab, speed, damage, pierce count, scale, and collision ignore rules per spawn.
8. `ProjectileDamageDealer` applies damage to `IDamageable` targets.
9. `ProjectileHitDetector` handles tracked target, trigger/collision, and movement raycast hit paths.
10. Damage receivers spawn damage popups where appropriate.

## Evolution Runtime Flow

1. `TurretDefinitionRuntimeController` checks available evolutions for current tier level.
2. `TurretEvolutionRuntimeUI` displays choices.
3. UI uses entry icon first, then fallback sprite.
4. On selection, evolution effect can spawn through `PooledObjectUtility.SpawnEffect`.
5. If target definition has a base prefab, the old turret prefab is replaced.
6. Target turret receives the new definition.
7. Tier level resets to `1`; total level is preserved.
8. Runtime UI reattaches to the evolved turret controller.

## Placement Runtime Flow

1. `TurretPlacementUI` builds bottom-bar slots from `TurretShopEntrySO` entries.
2. `TurretPlacementSlotUI` starts placement by drag or click.
3. `TurretPlacementController` raycasts against the TurretBase layer and expects `PlacementHitArea`.
4. Hit collider must have `TurretBaseSlot` in parent hierarchy.
5. If the slot has `BuildPoint` and no turret, preview snaps to `BuildPoint` and shows valid state.
6. If occupied, preview still snaps to `BuildPoint` but shows invalid state.
7. If no turret base is hit, invalid preview projects onto fixed placement plane.
8. Successful placement instantiates the turret prefab as a child of `BuildPoint`.
9. Installed turret local position and rotation should reset to zero/identity.
10. `TurretBaseSlot` records the occupied turret controller or fallback GameObject.

## Targeting And Firing Notes

- `TargetFinder` selects the nearest valid target in range.
- `TargetFinder` resolves hit colliders to a stable tagged or `IDamageable` target root before returning a target, avoiding aim jitter from multi-collider enemies.
- `TargetFinder` can ignore `ObstacleBuildSlot` helper colliders, placed `Obstacle` colliders, and an additional ignore layer mask during line-of-sight checks so defense-line barricades do not hide zombies from turret targeting.
- `Turret` smooths target aim point and target velocity prediction, clamps prediction lead time, and ignores vertical prediction by default to reduce visible tracking jitter.
- Turret fires only when the head is within `turretAngleAttack`.
- Gun projectile rotation should follow visible muzzle forward direction.
- Homing/projectile mover components may still receive selected targets for hit tracking.

## Pooling Rules

- Projectile scale must be applied every spawn.
- Projectile speed, damage, pierce count, target/collider state, and collision ignores must be refreshed every spawn.
- `ProjectileDamageDealer` allows colliders under the tracked target `IDamageable` even if the specific child collider is not on the damage layer; this prevents HOVL hit VFX from ending a projectile on a non-damage child collider without applying damage.
- Do not rely on prefab state or previous pooled state.
- Evolution effects should spawn through `PooledObjectUtility.SpawnEffect`.
- `ProjectileHitDetector` must clear target/collider state on reuse.
- `DamagePopup.Init` must receive settings every spawn because pooled text objects retain previous state.

## Setup Checklist

For each turret definition:

- Stable `turretId` assigned.
- User-facing `displayName` assigned.
- `basePrefab` assigned.
- `baseStatProfile` assigned.
- Growth, VFX progression, projectile scale progression assigned when needed.
- Evolution progression assigned only if the turret can evolve.
- `maxLevel` is `0` unless the form should stop leveling.

For each evolution entry:

- `requiredLevel` uses tier level.
- `targetDefinition` assigned.
- Display name and icon assigned for runtime UI.
- Evolution effect prefab assigned only when an effect should play.

For each turret base:

- Root has `TurretBaseSlot`.
- `BuildPoint` exists.
- `PlacementHitArea` exists with collider.
- `PlacementHitArea` is on TurretBase layer.
- Build point should not contain a default turret for production placement prefabs.

## Deprecated Or Avoided Patterns

- Do not reintroduce visual part toggling data unless design explicitly requires it.
- Do not duplicate `TurretVFXProfileSO` assets only to represent projectile size.
- Use `TurretProjectileScaleProgressionSO` for projectile scale growth.
- Avoid direct Private Assets edits unless a project-level wrapper or adapter cannot solve the integration.
