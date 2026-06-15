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
| `TurretVFXProfileSO` | Attack VFX selection data: projectile prefab or beam prefab, optional beam attack profile reference, muzzle VFX, muzzle duration. Audio is intentionally removed until the project-level sound system is rebuilt. |
| `BeamAttackProfileSO` | Beam-specific attack rules: damage tick interval, damage multiplier, DPS interpretation, target mode, pierce radius, max targets, damage layer mask, and Frost status values. |
| `TurretVFXProgressionSO` | Selects active VFX profile by current tier level. |
| `TurretProjectileScaleProgressionSO` | Selects projectile scale by current tier level. |
| `TurretEvolutionProgressionSO` | Defines available evolutions, required tier levels, branch-specific costs, icons, and effects. |
| `TurretShopEntrySO` | Legacy type name for turret placement entry data. Defines placement UI slot data, placement costs, icon, definition, prefab override, and preview prefab. |

## Stat And VFX Runtime Flow

1. `TurretDefinitionRuntimeController` receives or owns the current tier level.
2. It clamps level by evolution requirements and max level.
3. `TurretStatCalculator` calculates runtime stats from base and growth profiles.
4. `TurretStatProfileApplier` applies combat stats to the runtime turret components.
5. `TurretVFXProgressionSO` selects projectile, beam, muzzle VFX data, and optional beam attack profile data.
6. `TurretProjectileScaleProgressionSO` selects projectile scale.
7. Projectile firing logic applies projectile prefab, speed, damage, pierce count, scale, and collision ignore rules per spawn.
8. Beam firing logic keeps the beam VFX alive between fire requests and applies damage by `BeamAttackProfileSO.damageTickInterval`.
9. `ProjectileDamageDealer` applies projectile damage to `IDamageable` targets.
10. `ProjectileHitDetector` handles tracked target, trigger/collision, and movement raycast hit paths.
11. Damage receivers spawn damage popups where appropriate.

## Beam Attack Runtime Flow

Beam attacks are project-level adapters around external VFX prefabs. Do not edit Private Assets originals directly; duplicate or wrap beam prefabs under the turret scene folder.

Current implementation:

- `BeamFiringEvent` inherits the existing Modular Turrets `FiringEvent` contract so it can be assigned to `TurretDefinitionRuntimeController.targetFiringEvent`.
- `TurretVFXProfileSO.attackVfxType = Beam` selects beam mode.
- `TurretVFXProfileSO.beamPrefab` provides the visual prefab used by `BeamFiringEvent`.
- `TurretVFXProfileSO.beamAttackProfile` provides the beam-specific attack profile.
- `TurretDefinitionRuntimeController.ApplyVFX` passes the selected beam prefab, beam attack profile, and projectile scale to `BeamFiringEvent`.
- The base `Turret` component still requires a projectile prefab reference to pass its firing gate. For beam VFX profiles, the controller assigns the beam prefab as the turret projectile prefab only to satisfy that gate; `BeamFiringEvent` does not instantiate a projectile.
- `Turret.fireTick` still controls how often the base turret calls `FiringEvent.Fire`. For beam turrets, set the turret stat `fireInterval` low enough for responsive target acquisition and beam startup, usually `0.1` to `0.2`.
- Actual beam damage cadence is controlled by `BeamAttackProfileSO.damageTickInterval`, not by projectile fire interval.

`BeamFiringEvent` runtime behavior:

- Creates one beam instance per configured `gunPrefabs` entry.
- Uses `Gun.muzzleObject` as the start point. If this reference is wrong, the beam starts from the turret base or mesh instead of the real muzzle.
- Keeps the beam VFX active between fire requests while the current target remains alive and in range.
- Updates beam start, direction, length, beam target, and hit effect every frame.
- Uses `TargetFinder.radius` and `TargetFinder.useHorizontalDistance` to validate whether the current beam target is still in range.
- Applies damage ticks through `BeamAttackProfileSO`.
- Uses non-alloc physics buffers for pierce-line damage checks.

Current target modes:

| Mode | Current Behavior | Intended Use |
| --- | --- | --- |
| `CurrentTarget` | Damages only the current turret target. | Simple continuous beam or drain beam. |
| `PierceLine` | Sphere-casts from muzzle to current target and damages up to `maxTargets` along that line. | Frost beam, lance, piercing ray. |
| `ChainNearest` | Currently falls back to current-target damage. | Reserved for future chain-lightning or spreading beam behavior. |

Damage interpretation:

- If `BeamAttackProfileSO.treatTurretDamageAsDps` is enabled, the runtime tick damage is `turretDamage * damageTickInterval * damageMultiplier`.
- If it is disabled, each tick applies `turretDamage * damageMultiplier`.
- For continuous beams, keep `treatTurretDamageAsDps` enabled unless the design explicitly wants per-tick burst damage.

Frost status handling:

- `BeamAttackProfileSO` stores `slowRatio`, `slowDuration`, and `freezeDuration`.
- `BeamFiringEvent` only forwards these values to targets implementing `IFrostStatusEffectReceiver`.
- `NormalZombie` and `BossZombie` do not currently implement `IFrostStatusEffectReceiver`, so slow and freeze values are data-ready but not active until enemy-side status handling is added.
- `slowDuration <= 0` means slow is inactive even if `slowRatio` is greater than zero.

## Frost Beam Setup

Current Frost beam assets:

| Asset | Role |
| --- | --- |
| `Prefabs/Turret/3rdGen/Frost_Turret.prefab` | Runtime turret prefab. |
| `Prefabs/Beam/FrostRay/FrostRay_TurretBeam.prefab` | Project-owned duplicated/adapted FrostRay beam VFX prefab. |
| `SO/TurretDefinition/3rdGen/Frost_Turret_Definition.asset` | Frost turret definition. |
| `SO/TurretVfxProgresstion/3rdGen/Forst_Turret_VFX Progression SO.asset` | Selects the FrostRay VFX profile. |
| `SO/VFXProfiles/Beam/VFX_ForstRay/New Turret VFX Profile SO.asset` | Beam VFX profile for FrostRay. |
| `SO/AttackProfiles/Frost_BeamAttackProfile.asset` | Beam attack rules for Frost. |

Required Frost prefab wiring:

- `Frost_Turret` root has `Turret`, `TargetFinder`, `TurretStatProfileApplier`, `TurretDefinitionRuntimeController`, `BeamFiringEvent`, and `Gun`.
- `TurretDefinitionRuntimeController.targetFiringEvent` points to `BeamFiringEvent`.
- `TurretDefinitionRuntimeController.turretDefinition` points to `Frost_Turret_Definition`.
- `BeamFiringEvent.gunPrefabs` can point to the root if the root has the `Gun` component.
- `Gun.muzzleObject` must point to the actual `FireNozzle` object. If it points to the turret mesh, such as `SM_Laser_Gun_Base`, the beam starts from the wrong position and may appear to aim downward.
- `TargetFinder.pivotObject` should point to a rotating head or muzzle-related object so range and line-of-sight checks use a sensible origin.
- `Frost_Turret` stat `fireInterval` should be near `0.1` to `0.2` for responsive continuous beam startup.

Required VFX Profile wiring:

- `Attack Vfx Type`: `Beam`
- `Beam Prefab`: `FrostRay_TurretBeam`
- `Beam Attack Profile`: `Frost_BeamAttackProfile`
- `Projectile Prefab`: empty is allowed.

Recommended Frost Beam Attack Profile values for first testing:

| Field | Suggested Value | Note |
| --- | ---: | --- |
| `damageTickInterval` | `0.2` | Five damage ticks per second. |
| `damageMultiplier` | `1` | Keep neutral until balance pass. |
| `treatTurretDamageAsDps` | enabled | Prevents low `fireInterval` from multiplying DPS. |
| `targetMode` | `PierceLine` | Matches piercing frost ray behavior. |
| `maxTargets` | `4` | Temporary lane-clearing value. |
| `pierceRadius` | `0.35` | Tune by enemy collider size and visual width. |
| `damageBufferSize` | `16` | Must be at least `maxTargets`; increase only if needed. |
| `slowRatio` | `0.2` | Data-ready until enemy status receiver exists. |
| `slowDuration` | `1.0` | Must be greater than zero to apply slow later. |
| `freezeDuration` | `0` | Reserve for future freeze behavior. |

FrostRay VFX notes:

- `FrostRay_TurretBeam` uses `PilotoStudio.BeamEmitter`.
- The actual line target is `BeamEmitter.beamTarget`; there are several objects named `holder`, so name-based lookup is not reliable.
- The hit effect is `BeamEmitter.beamTargetHitFX`, currently `Hit_Spikes`.
- `BeamFiringEvent` resolves those `BeamEmitter` references at runtime and moves them to the current target position every frame.
- The FrostRay beam visual is authored around a base length of `5`. `BeamFiringEvent.scaleBeamLengthAlongLocalX` scales local X by `targetDistance / beamBaseLength`.
- To reduce only the hit effect size, scale the `Hit_Spikes` object or its children, not the root `FrostRay_TurretBeam` object. Scaling the root changes the whole beam length and width.
- If VFX density or particle emission looks wrong after length scaling, tune the duplicated `FrostRay_TurretBeam` prefab under the project folder, not the Private Assets original.

## Current Balance Direction

- Current turret forms are balanced around tier level `100` evolution gates.
- `TurretStatProfileSO` stores tier level `1` base values used immediately after placement or evolution.
- `TurretStatGrowthProfileSO` grows damage, range, fire interval, projectile speed, projectile count, and pierce count toward tier level `100`.
- The current curve intentionally allows a tier level `100` turret to be stronger than the next evolved turret at tier level `1`, creating a short power dip after evolution and a higher growth ceiling afterward.
- Range is capped at `maxRange = 66` because larger ranges exceed the current game view.
- Single-target DPS should be checked from runtime stat values as `damage * projectileCount / fireInterval`.
- `pierceCount` is not part of single-target DPS. Treat it as lane-clearing potential, roughly `DPS * (pierceCount + 1)` only when every pierced target is expected to be hit.

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
| Machinegun_Blue_1/2/3 | 365.63 / 771.38 / 1333.5 | 49 / 53 / 57 | 0.45 / 0.3429 / 0.2667 |
| Machinegun_Red_1/2/3 | 609.38 / 1285.88 / 2221.5 | 47 / 51 / 55 | 0.75 / 0.5715 / 0.4443 |
| Laser_Blue_1/2/3 | 152.5 / 321.25 / 556 | 55 / 59 / 63 | 0.0938 / 0.0714 / 0.0556 |
| Laser_Red_1/2/3 | 609.5 / 1285.75 / 2222 | 53 / 57 / 61 | 0.375 / 0.2857 / 0.2222 |
| Lethal_Green_1/2/3 | 3047 / 6428.75 / 11111 | 45 / 49 / 53 | 1.875 / 1.4286 / 1.1111 |
| Lethal_Red_1/2/3 | 5078.25 / 10714.5 / 18519 | 43 / 47 / 51 | 3.125 / 2.381 / 1.8519 |
| Plasma_Blue_1/2/3 | 2031.25 / 4285.75 / 7407 | 51 / 55 / 59 | 1.25 / 0.9524 / 0.7407 |
| Plasma_Yellow_1/2/3 | 8125 / 17142.75 / 29630 | 49 / 53 / 57 | 5 / 3.8095 / 2.963 |

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

Second-generation fire interval progression uses a family baseline curve: `_1` starts slower, `_2` is slightly faster than baseline, and `_3` reaches the fastest form. Non-machinegun families are balanced against the current fire intervals so each second-generation stage shares the same DPS targets across families: `_1` grows from about 1,625 to 4,250 DPS, `_2` grows from about 4,500 to 9,500 DPS, and `_3` grows from about 10,000 to 15,000 DPS. `Machinegun_Blue` and `Machinegun_Red` trade single-target DPS for lane clearing: their fire intervals are about 3x slower, damage is about 1.5x higher, single-target DPS is about half of the standard second-generation target, and pierce count is `_1 = 1`, `_2 = 2`, `_3 = 3`.

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

### Current Turret Cost Balance

Upgrade costs use a low base Coin value plus an additional cost percentage per tier level, so early upgrades are cheaper while late upgrades become heavier. The Lv1 -> Lv100 total is kept close to the previous flat-cost totals.

`TurretUpgradeCostProfileSO` calculates each level-up cost from the target tier level. For a one-level upgrade, the effective cost is `ceil(baseCoin * (1 + (targetLevel - 1) * additionalPercent))`. Multi-level upgrade requests sum that same formula for every level from `currentTierLevel + 1` through `targetTierLevel`.

The intended economy ramp is form-based: Sentinel-01 uses a 1% per-tier surcharge, first evolutions use 2%, first-generation branch ends and second-generation `_1` forms use 3%, second-generation `_2` forms use 4%, and second-generation `_3` forms use 5%. Keep future cost profiles aligned with this ramp unless the reward curve is rebalanced at the same time.

| Form | Base Coin per level-up | Additional cost per tier level | Approx. Lv1 -> Lv100 total |
| --- | ---: | ---: | ---: |
| Sentinel-01 | 233 | 1% | 34,601 |
| Sentry Pulse / Vector MG | 350 | 2% | 69,300 |
| Pulse Repeater / Vulcan Node | 640 | 3% | 158,400 |
| Second-generation `_1` | 3,200 | 3% | 792,000 |
| Second-generation `_2` | 5,667 | 4% | 1,683,099 |
| Second-generation `_3` | 10,571 | 5% | 3,662,852 |

Evolution costs are branch-entry gates:

| Evolution step | Coin cost |
| --- | ---: |
| Sentinel-01 -> Sentry Pulse / Vector MG | 10,000 |
| Sentry Pulse -> Pulse Repeater / Vector MG -> Vulcan Node | 60,000 |
| Pulse Repeater / Vulcan Node -> second-generation `_1` branch | 180,000 |
| Second-generation `_1` -> `_2` | 300,000 |
| Second-generation `_2` -> `_3` | 450,000 |

A complete path from Sentinel-01 tier level 1 to a second-generation `_3` tier level 100 costs roughly 7.4M Coin before placement costs. Integer cost rounding causes only a tiny difference from the previous flat-cost total. This is tuned as a first pass against the current 500-wave reward curve.

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
- `HovlProjectilePierceGuard` prevents HOVL's own target-hit return flow from ending a projectile before `ProjectileDamageDealer` reaches its pierce limit.
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
- upgrade cost ramp mismatches against the current form rules.
- evolution cost ramp mismatches against the current branch-entry rules.
- `maxLevel` values set together with `evolutionProgressionProfile`.
- evolution entries that target the same `TurretDefinitionSO` as their source definition.

Use `Project Z Defense/Reports/Turret Balance Report` when reviewing balance changes. The report window summarizes turret Lv1/Lv100 DPS, upgrade costs, evolution costs, wave reward estimates, and how many extra upgrade levels one average wave reward can buy for each upgrade cost tier. Use its CSV export when sharing a balance pass outside Unity.

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
