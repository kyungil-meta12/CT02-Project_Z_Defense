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
| Sentry Pulse | First branch, fast-fire path, tier 1-100 | Evolves at tier 100 into Pulse Repeater. |
| Vector MG | First branch, high-damage path, tier 1-100 | Evolves at tier 100 into Vulcan Node. |
| Pulse Repeater | First-generation fast-fire branch end, tier 1-100 | Evolves at tier 100 into Machinegun or Laser second-generation entries. |
| Vulcan Node | First-generation high-damage branch end, tier 1-100 | Evolves at tier 100 into Lethal or Plasma second-generation entries. |

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
| `TurretDefinitionSO` | Top-level turret identity, prefab, base stat profile, growth, upgrade cost profile, VFX progression, projectile scale progression, evolution progression, max level. |
| `TurretStatProfileSO` | Base combat values for tier level 1: damage, range, fire interval, projectile speed, projectile count, pierce count. |
| `TurretStatGrowthProfileSO` | Tier-level-based growth calculation using completed growth steps. |
| `TurretUpgradeCostProfileSO` | Calculates upgrade costs from current tier level to target tier level. |
| `TurretVFXProfileSO` | Projectile visual data only: projectile prefab, muzzle VFX, muzzle duration. No balance values. Audio is intentionally removed until the project-level sound system is rebuilt. |
| `TurretVFXProgressionSO` | Selects active VFX profile by current tier level. |
| `TurretProjectileScaleProgressionSO` | Selects projectile scale by current tier level. |
| `TurretEvolutionProgressionSO` | Defines available evolutions, required tier levels, branch-specific costs, icons, and effects. |
| `TurretShopEntrySO` | Legacy type name for turret placement entry data. Defines placement UI slot data, placement costs, icon, definition, prefab override, and preview prefab. |

## Stat And VFX Runtime Flow

1. `TurretDefinitionRuntimeController` receives or owns the current tier level.
2. It clamps level by evolution requirements and max level.
3. `TurretStatCalculator` calculates runtime stats from base and growth profiles.
4. `TurretStatProfileApplier` applies combat stats to the runtime turret components.
5. `TurretVFXProgressionSO` selects projectile and muzzle VFX data.
6. `TurretProjectileScaleProgressionSO` selects projectile scale.
7. Firing logic applies projectile prefab, speed, damage, pierce count, scale, and collision ignore rules per spawn.
8. `ProjectileDamageDealer` applies damage to `IDamageable` targets.
9. `ProjectileHitDetector` handles tracked target, trigger/collision, and movement raycast hit paths.
10. Damage receivers spawn damage popups where appropriate.

## Current Balance Direction

- Current turret forms are balanced around tier level `100` evolution gates.
- `TurretStatProfileSO` stores tier level `1` base values used immediately after placement or evolution.
- `TurretStatGrowthProfileSO` grows damage, range, fire interval, projectile speed, projectile count, and pierce count toward tier level `100`.
- The current curve intentionally allows a tier level `100` turret to be stronger than the next evolved turret at tier level `1`, creating a short power dip after evolution and a higher growth ceiling afterward.
- Range is capped at `maxRange = 66` because larger ranges exceed the current game view.

### First-Generation Balance

| Turret | Lv1 damage | Lv1 range | Lv1 fire interval | Lv100 damage | Lv100 range | Lv100 fire interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Sentinel-01 | 25 | 35 | 0.7 | 52.5 | 50 | 0.5 |
| Sentry Pulse | 35 | 42 | 0.4 | 84 | 58 | 0.2 |
| Vector MG | 175 | 44 | 2 | 672 | 58 | 1.6 |
| Pulse Repeater | 70 | 48 | 0.2 | 86.4 | 64 | 0.12 |
| Vulcan Node | 700 | 52 | 2 | 1152 | 66 | 1.6 |

### Second-Generation Lv1 Balance

| Family | Lv1 damage | Lv1 range | Lv1 fire interval |
| --- | --- | --- | --- |
| Machinegun_Blue_1/2/3 | 72 / 144 / 288 | 49 / 53 / 57 | 0.15 / 0.1143 / 0.0889 |
| Machinegun_Red_1/2/3 | 120 / 240 / 480 | 47 / 51 / 55 | 0.25 / 0.1905 / 0.1481 |
| Laser_Blue_1/2/3 | 45 / 90 / 180 | 55 / 59 / 63 | 0.0938 / 0.0714 / 0.0556 |
| Laser_Red_1/2/3 | 180 / 360 / 720 | 53 / 57 / 61 | 0.375 / 0.2857 / 0.2222 |
| Lethal_Green_1/2/3 | 900 / 1800 / 3600 | 45 / 49 / 53 | 1.875 / 1.4286 / 1.1111 |
| Lethal_Red_1/2/3 | 1500 / 3000 / 6000 | 43 / 47 / 51 | 3.125 / 2.381 / 1.8519 |
| Plasma_Blue_1/2/3 | 600 / 1200 / 2400 | 51 / 55 / 59 | 1.25 / 0.9524 / 0.7407 |
| Plasma_Yellow_1/2/3 | 2400 / 4800 / 9600 | 49 / 53 / 57 | 5 / 3.8095 / 2.963 |

### Second-Generation Lv100 Range Targets

| Family | Lv100 range |
| --- | --- |
| Machinegun_Blue_1/2/3 | 60 / 63 / 66 |
| Machinegun_Red_1/2/3 | 58 / 62 / 65 |
| Laser_Blue_1/2/3 | 66 / 66 / 66 |
| Laser_Red_1/2/3 | 64 / 66 / 66 |
| Lethal_Green_1/2/3 | 58 / 62 / 64 |
| Lethal_Red_1/2/3 | 56 / 60 / 62 |
| Plasma_Blue_1/2/3 | 64 / 66 / 66 |
| Plasma_Yellow_1/2/3 | 62 / 64 / 66 |

Second-generation fire interval progression uses a family baseline curve: `_1` starts slower, `_2` is slightly faster than baseline, and `_3` reaches the fastest form. Damage growth was not flattened after this fire interval pass, so second-generation forms may feel stronger than a strict equal-DPS table.

### Second-Generation Projectile Speed

Projectile speed is a feel differentiator from second generation onward. `Laser_Blue` should feel almost instant, `Laser_Red` remains fast but heavier, machineguns use fast physical bullets, and plasma/lethal lines use slower projectiles to emphasize high-impact shots.

| Family | Lv1 projectile speed | Lv100 projectile speed |
| --- | --- | --- |
| Laser_Blue_1/2/3 | 90 / 110 / 130 | 130 / 150 / 180 |
| Laser_Red_1/2/3 | 70 / 85 / 100 | 105 / 120 / 140 |
| Machinegun_Blue_1/2/3 | 55 / 65 / 75 | 80 / 90 / 105 |
| Machinegun_Red_1/2/3 | 45 / 55 / 65 | 70 / 80 / 95 |
| Plasma_Blue_1/2/3 | 35 / 42 / 50 | 55 / 62 / 75 |
| Plasma_Yellow_1/2/3 | 25 / 30 / 35 | 40 / 48 / 55 |
| Lethal_Green_1/2/3 | 30 / 35 / 40 | 45 / 52 / 60 |
| Lethal_Red_1/2/3 | 22 / 26 / 30 | 36 / 42 / 50 |

## Evolution Runtime Flow

1. `TurretDefinitionRuntimeController` checks available evolutions for current tier level.
2. `TurretEvolutionRuntimeUI` displays choices.
3. UI calls `TryEvolve` or `TryCreateEvolvedInstance` so `ItemManager` spends `evolutionCosts` before runtime state changes.
4. UI uses entry icon first, then fallback sprite.
5. On selection, evolution effect can spawn through `PooledObjectUtility.SpawnEffect`.
6. If target definition has a base prefab, the old turret prefab is replaced.
7. Target turret receives the new definition.
8. Tier level resets to `1`; total level is preserved.
9. Runtime UI reattaches to the evolved turret controller.
10. The temporary runtime popup also refreshes the selected turret range indicator after selection, upgrade, or evolution.

## Upgrade Cost Flow

1. `TurretDefinitionRuntimeController.GetUpgradeCosts` queries `TurretDefinitionSO.upgradeCostProfile`.
2. `TurretUpgradeCostProfileSO` calculates total `ResourceCost[]` from current tier level to target tier level.
3. UI calls `TryUpgrade`, not `AddLevel`, for player-facing upgrades.
4. `ItemManager.TrySpend` consumes all required currencies atomically before `SetLevel` runs.
5. Hold upgrade input stops when the target is capped, evolution becomes available, or currency is insufficient.

## Placement Runtime Flow

1. `TurretPlacementUI` builds bottom-bar slots from placement entries currently typed as `TurretShopEntrySO`.
2. `TurretPlacementSlotUI` starts placement by drag or click.
3. `TurretPlacementController` raycasts against the TurretBase layer and expects `PlacementHitArea`.
4. Hit collider must have `TurretBaseSlot` in parent hierarchy.
5. If the slot has `BuildPoint` and no turret, preview snaps to `BuildPoint` and shows valid state.
6. If occupied, preview still snaps to `BuildPoint` but shows invalid state.
7. If no turret base is hit, invalid preview projects onto fixed placement plane.
8. `TurretBaseSlot.TryPlace` spends `TurretShopEntrySO.PlacementCosts` through `ItemManager.TrySpend`.
9. Successful placement instantiates the turret prefab as a child of `BuildPoint`.
10. Installed turret local position and rotation should reset to zero/identity.
11. `TurretBaseSlot` records the occupied turret controller or fallback GameObject.
12. `TurretPlacementController` records successful placement count per placement entry.
13. Placement entries can use `Placement Cost Tiers` to change the next placement cost by successful placement count.

## Targeting And Firing Notes

- `TargetFinder` selects the nearest valid target in range.
- `TargetFinder` resolves hit colliders to a stable tagged or `IDamageable` target root before returning a target, avoiding aim jitter from multi-collider enemies.
- `TargetFinder` can ignore `ObstacleBuildSlot` helper colliders, placed `Obstacle` colliders, and an additional ignore layer mask during line-of-sight checks so defense-line barricades do not hide zombies from turret targeting.
- `Turret` smooths target aim point and target velocity prediction, ignores vertical prediction by default, and uses `TurretLeadPredictionUtility` to aim at an estimated projectile/target intercept point.
- Prediction lead time can scale from slow-projectile long lead to fast-projectile short lead, improving low-speed projectile hit rate without making laser-speed shots over-lead visibly.
- `Turret` staggers its first target search within `targetSearchInterval` so many turrets do not all run physics target scans on the same frame.
- Turret fires on its configured fire interval while a valid target and projectile prefab exist. `requireAimAngleBeforeFire` can restore strict `turretAngleAttack` gating if a specific turret needs to wait for perfect alignment.
- Gun projectile rotation should follow visible muzzle forward direction.
- Homing/projectile mover components may still receive selected targets for hit tracking.

## Runtime Range Indicator

- `TurretTemporaryUpgradePopupUI` owns the selected-turret range display because it already owns turret click selection and selection clearing.
- `TurretRangeIndicator` renders one reusable `LineRenderer` circle in world space for the currently selected turret.
- The indicator uses the selected turret's current calculated runtime range, so level-up and evolution changes are reflected when the popup refreshes.
- The indicator is hidden when selection is cleared or placement input is active.

## Pooling Rules

- Projectile scale must be applied every spawn.
- Projectile speed, damage, pierce count, target/collider state, and collision ignores must be refreshed every spawn.
- `ProjectileComponentCache` caches frequently reused projectile components and colliders on pooled projectile instances to avoid repeated hierarchy scans during firing.
- `ProjectileDamageDealer` allows colliders under the tracked target `IDamageable` even if the specific child collider is not on the damage layer; this prevents HOVL hit VFX from ending a projectile on a non-damage child collider without applying damage.
- Do not rely on prefab state or previous pooled state.
- Evolution effects should spawn through `PooledObjectUtility.SpawnEffect`.
- `ProjectileHitDetector` must clear target/collider state on reuse.
- `DamagePopup.Init` must receive settings every spawn because pooled text objects retain previous state.

## Setup Checklist

Run `Project Z Defense/Validation/Validate Turret Economy` after wiring turret economy assets.

The validator checks:

- missing `upgradeCostProfile` references on turret definitions.
- empty `baseCostsPerLevel` arrays on upgrade cost profiles.
- empty `evolutionCosts` arrays on evolution entries with a target definition.
- missing `placementCosts` on turret placement entries.
- negative cost amounts in upgrade, evolution, and placement cost arrays.

For each turret definition:

- Stable `turretId` assigned.
- User-facing `displayName` assigned.
- `basePrefab` assigned.
- `baseStatProfile` assigned.
- Growth, upgrade cost, VFX progression, projectile scale progression assigned when needed.
- Evolution progression assigned only if the turret can evolve.
- `maxLevel` is `0` unless the form should stop leveling.

For each evolution entry:

- `requiredLevel` uses tier level.
- `targetDefinition` assigned.
- `evolutionCosts` assigned when the branch should consume currency.
- Display name and icon assigned for runtime UI.
- Evolution effect prefab assigned only when an effect should play.

For each turret base:

- Root has `TurretBaseSlot`.
- `BuildPoint` exists.
- `PlacementHitArea` exists with collider.
- `PlacementHitArea` is on TurretBase layer.
- Build point should not contain a default turret for production placement prefabs.

For each turret placement entry:

- `Placement Costs` is the default first placement cost.
- `Placement Cost Tiers.Min Placed Count` means successful placements already completed for that entry.
- Example: `Min Placed Count = 1` applies to the second successful placement attempt.
- The highest matching tier is used when multiple tiers match.

## Deprecated Or Avoided Patterns

- Do not reintroduce visual part toggling data unless design explicitly requires it.
- Do not duplicate `TurretVFXProfileSO` assets only to represent projectile size.
- Use `TurretProjectileScaleProgressionSO` for projectile scale growth.
- Avoid direct Private Assets edits unless a project-level wrapper or adapter cannot solve the integration.
