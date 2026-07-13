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

## Turret Display Name Remake Plan

Display names are being remade for UI presentation. Keep asset names, prefab names, and stable IDs unchanged unless a separate GUID-safe rename pass is planned.

| Current `turretName` | Korean Display Name | English Display Name |
| --- | --- | --- |
| Pulse Repeater_Definition | 펄스 리피터 | Pulse Repeater |
| sentinel_01_Definition | 센티널 포스트 | Sentinel Post |
| Sentry Pulse_Definition | 센트리 펄스 | Sentry Pulse |
| Vector MG_Definition | 벡터 MG | Vector MG |
| Vulcan Node_Definition | 벌칸 노드 | Vulcan Node |
| Laser_Blue_Definition 1 | 블루 레이 | Blue Ray |
| Laser_Blue_Definition 2 | 블루 랜스 | Blue Lance |
| Laser_Blue_Definition 3 | 블루 저지먼트 | Blue Judgment |
| Laser_Red_Definition 1 | 레드 레이 | Red Ray |
| Laser_Red_Definition 2 | 레드 랜스 | Red Lance |
| Laser_Red_Definition 3 | 레드 저지먼트 | Red Judgment |
| Lethal_Green_Definition 1 | 그린 니들 | Green Needle |
| Lethal_Green_Definition 2 | 그린 피어스 | Green Pierce |
| Lethal_Green_Definition 3 | 그린 랜서 | Green Lancer |
| Lethal_Red_Definition 1 | 엠버 다트 | Ember Dart |
| Lethal_Red_Definition 2 | 엠버 캐논 | Ember Cannon |
| Lethal_Red_Definition 3 | 엠버 저지먼트 | Ember Judgment |
| Machinegun_Blue_Definition 1 | 블루 MG | Blue MG |
| Machinegun_Blue_Definition 2 | 블루 배러지 | Blue Barrage |
| Machinegun_Blue_Definition 3 | 블루 스톰 | Blue Storm |
| Machinegun_Red_Definition 1 | 레드 MG | Red MG |
| Machinegun_Red_Definition 2 | 레드 배러지 | Red Barrage |
| Machinegun_Red_Definition 3 | 레드 스톰 | Red Storm |
| Plasma_Blue_Definition 1 | 블루 필드 | Blue Field |
| Plasma_Blue_Definition 2 | 블루 서지 | Blue Surge |
| Plasma_Blue_Definition 3 | 블루 코어 | Blue Core |
| Plasma_Yellow_Definition 1 | 선 필드 | Sun Field |
| Plasma_Yellow_Definition 2 | 선 버스트 | Sun Burst |
| Plasma_Yellow_Definition 3 | 선 코어 | Sun Core |
| Electro_Turret_Definition | 스톰 리액터 | Storm Reactor |
| Frost_Turret_Definition | 글레이셜 코어 | Glacial Core |
| Ignition_Turret_Definition | 인페르노 코어 | Inferno Core |
| Poison_Turret_Definition | 베놈 리액터 | Venom Reactor |

## Core ScriptableObjects

| Type | Role |
| --- | --- |
| `TurretDefinitionSO` | Top-level turret identity, prefab, base stat profile, growth, upgrade cost profile, VFX progression, projectile scale progression, audio profile, status profile, evolution progression, max level, engineer seat limit. |
| `TurretStatProfileSO` | Base combat values for tier level 1: damage, range, fire interval, projectile speed, projectile count, pierce count. |
| `TurretStatGrowthProfileSO` | Tier-level-based growth calculation. Damage uses max-level target damage with a log curve, while non-damage stats keep per-level or interval growth. |
| `TurretUpgradeCostProfileSO` | Calculates upgrade costs from current tier level to target tier level. |
| `TurretVFXProfileSO` | Attack VFX selection data: projectile prefab or beam prefab, optional beam attack profile reference, muzzle VFX, muzzle duration. Audio belongs to `TurretAudioProfileSO` and `TurretAudioController` through the project-level audio system. |
| `TurretDamagePolishProfileSO` | Optional per-turret damage polish rules: random damage variance, critical hits, heavy hits, and damage popup type. Leave empty to keep deterministic legacy damage. |
| `BeamAttackProfileSO` | Beam-specific attack rules: damage tick interval, damage multiplier, DPS interpretation, target mode, pierce radius, max targets, and damage layer mask. |
| `FrostStatusProfileSO` | Frost-specific status rules: slow buildup, max slow, freeze timing, freeze explosion, max-HP damage, secondary explosion slow, related VFX references, and optional primary-target max-HP damage growth. |
| `PoisonStatusProfileSO` | Poison-specific status rules: max-HP tick damage ratio, tick interval, duration, stack limit, stack refresh mode, boss damage multiplier, and optional death burst profile reference. |
| `ElectroStatusProfileSO` | Electro-specific base status rules and chain VFX controls: chain lightning count/radius/falloff, base Shock stack duration and stack VFX, Overload trigger policy, base single-target Overload damage, short stun timing, chain-link particle VFX, and optional core line VFX. |
| `IgnitionStatusProfileSO` | Ignition-specific burn status rules: flat DPS multiplier fallback, max-HP tick damage ratio, tick interval, duration, stack limit, stack refresh mode, boss damage multiplier, and attribute interaction flags. |
| `PoisonDeathBurstProfileSO` | Poison lethal-death burst rules: burst VFX, radius, weak Poison values, target layer mask, and boss damage multiplier for secondary weak Poison. |
| `PoisonTurretStatGrowthProfileSO` | Poison_Turret-only stat growth profile. Inherits common turret stat growth and adds Poison status/death-burst growth fields without exposing them on non-Poison turrets. |
| `ElectroTurretStatGrowthProfileSO` | Electro_Turret-only stat growth profile. Inherits common turret stat growth and adds Shock duration, chain target count, and Overload max-HP damage growth fields without exposing them on non-Electro turrets. |
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
6. `TurretDefinitionSO.frostStatusProfile` provides Frost-specific status values when the selected attack is a beam Frost turret.
7. `TurretDefinitionSO.poisonStatusProfile` provides Poison-specific status values when the selected attack is a projectile Poison turret.
8. `TurretDefinitionSO.electroStatusProfile` provides Electro-specific chain, Shock stack, Overload trigger policy, single-target Overload damage, and stun values when the selected attack is an Electro projectile turret.
9. `TurretDefinitionSO.ignitionStatusProfile` provides Ignition burn values when the selected attack uses an Ignition area detector.
10. `TurretDefinitionSO.damagePolishProfile` optionally provides random damage variance, critical hit, and heavy hit rules for projectile and beam damage.
11. `TurretProjectileScaleProgressionSO` selects projectile scale.
12. Projectile firing logic applies projectile prefab, speed, damage, pierce count, scale, collision ignore rules, and optional Poison/Electro payloads per spawn.
13. Beam firing logic keeps the beam VFX alive between fire requests and applies damage by `BeamAttackProfileSO.damageTickInterval`.
14. `IgnitionDamageApplier` reads targets from `IgnitionConeDetector` and forwards Ignition payloads to `IIgnitionStatusEffectReceiver`.
15. `ProjectileDamageDealer` applies projectile damage to `IDamageable` targets, forwards Poison payloads to `IPoisonStatusEffectReceiver`, and triggers Electro chain damage when an Electro payload is active.
16. `ProjectileDamageDealer` and `BeamFiringEvent` roll `TurretDamagePolishProfileSO` before `IDamageable.TakeDamage`; damage, popup style, and popup policy are passed together through `DamageInfo`.
17. `ProjectileDamageDealer` resolves direct-hit popup policy through `DamagePopupPolicyResolver.ResolveDirectHit`, so normal hits accumulate while critical/heavy direct hits display immediately.
18. `BeamFiringEvent` resolves beam tick popup policy through `DamagePopupPolicyResolver.ResolveHighFrequencyTick`, so all beam tick popups accumulate to avoid high-frequency critical/heavy spam.
19. `DamagePopupPolicyResolver` reads `DamagePopupPolicyProfile.asset` through `DamagePopupSettings.asset` when the profile is assigned, so popup policy tuning does not require code changes.
20. `ProjectileHitDetector` handles tracked target, trigger/collision, and movement raycast hit paths.
21. Damage receivers spawn damage popups where appropriate.
22. `TurretDamageMeterSource` is attached per turret instance and is carried through `DamageInfo` plus Poison, Electro, Ignition, and Frost payloads so delayed damage stays credited to the original turret.
23. Damage receivers report only actual HP loss (`beforeHp - afterHp`) to `TurretDamageMeterManager`, so overkill is excluded from the damage meter.

## Runtime Damage Meter

- `TurretDamageMeterManager` owns wave-scoped turret damage totals and keeps previous-wave values visible until the first damage of the next wave is reported.
- `TurretDamageMeterManager.meterUI` is an explicit Inspector reference to the scene `TurretDamageMeterUI`; runtime `GetComponent` fallback is not used for normal play.
- The first damage in a new wave resets all currently installed turret entries to `0` and then applies that first actual damage.
- Removed, sold, disabled, or evolved-away turret instances unregister their source and disappear from the meter. Evolved turrets register as new separate entries.
- Ranking is sorted every `0.25` seconds, while text and bar UI refresh is intended for `0.15` second cadence.
- Row movement is handled by `TurretDamageMeterRowUI` with `Mathf.SmoothDamp` in `Update`; do not add `Vertical Layout Group` to the row root.
- `TurretDamageMeterRowUI` requires a `CanvasGroup` on each row object; do not rely on runtime component creation for fold alpha animation.
- Bars use first-place-relative fill ratio, while percent text uses total installed turret damage share.
- `TurretDamageMeterColorProfileSO` maps turret definitions or stable `turretId` values to bar colors. Direct `TurretDefinitionSO` reference rules take priority, then `turretId`, then fallback color.
- Create the color profile from `Project Z Defense/UI/Turret Damage Meter Color Profile` and assign it to `TurretDamageMeterUI.colorProfile`.
- If the install cap ever exceeds eight, the manager can keep all entries and the UI can display only the configured top row limit.

## Beam Attack Runtime Flow

Beam attacks are project-level adapters around external VFX prefabs. Do not edit Private Assets originals directly; duplicate or wrap beam prefabs under the turret scene folder.

Current implementation:

- `BeamFiringEvent` inherits the existing Modular Turrets `FiringEvent` contract so it can be assigned to `TurretDefinitionRuntimeController.targetFiringEvent`.
- `TurretVFXProfileSO.attackVfxType = Beam` selects beam mode.
- `TurretVFXProfileSO.beamPrefab` provides the visual prefab used by `BeamFiringEvent`.
- `TurretVFXProfileSO.beamAttackProfile` provides the beam-specific attack profile.
- `TurretDefinitionSO.frostStatusProfile` provides Frost-specific status values separately from the beam attack profile.
- `TurretDefinitionRuntimeController.ApplyVFX` passes the selected beam prefab, beam attack profile, Frost status profile, current level, and projectile scale to `BeamFiringEvent`.
- The base `Turret` component still requires a projectile prefab reference to pass its firing gate. For beam VFX profiles, the controller assigns the beam prefab as the turret projectile prefab only to satisfy that gate; `BeamFiringEvent` does not instantiate a projectile.
- `Turret.fireTick` still controls how often the base turret calls `FiringEvent.Fire`. For beam turrets, set the turret stat `fireInterval` low enough for responsive target acquisition and beam startup, usually `0.1` to `0.2`.
- Actual beam damage cadence is controlled by `BeamAttackProfileSO.damageTickInterval`, not by projectile fire interval.
- `TurretStatProfileSO.projectileSpeed` is still passed through the shared turret API for beam profiles, but `BeamFiringEvent` does not use it as travel speed because the beam connects to the target immediately. Keep a safe non-zero value for shared reports, validators, and compatibility, but tune beam feel through beam VFX/profile values instead.

`BeamFiringEvent` runtime behavior:

- Creates one beam instance per configured `gunPrefabs` entry.
- Uses `Gun.muzzleObject` as the start point. If this reference is wrong, the beam starts from the turret base or mesh instead of the real muzzle.
- Keeps the beam VFX active between fire requests while the current target remains alive and in range.
- Updates beam start, direction, length, beam target, and hit effect every frame.
- Can require muzzle-to-target alignment before showing the beam and applying beam damage. Use this for beam turrets whose visual beam should not appear before the rotating head has caught up to a new target. Current Frost_Turret enables this gate.
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

- `FrostStatusProfileSO` stores Frost values (`freezeDuration`, `slowBuildUpDuration`, `maxSlowRatio`, `slowHoldDuration`, `freezeTriggerRatio`, `freezeEffectPrefab`, `freezeEffectDuration`, `freezeDeathEffectPrefab`, `freezeDeathEffectDuration`, `freezeExplosionDamageDelay`, `freezeExplosionRadius`, `freezeExplosionDamage`, `freezePrimaryTargetMaxHpDamageRatio`, `freezeExplosionLayerMask`, `freezeCooldownPerTarget`, `freezeExplosionSlowRatio`, `freezeExplosionSlowDuration`) and optional primary-target max-HP damage ratio growth.
- `BeamFiringEvent` asks `FrostStatusProfileSO` for a level-scaled `ProjectZDefense.StatusEffects.FrostStatusPayload` and forwards it to targets implementing `ProjectZDefense.StatusEffects.IFrostStatusEffectReceiver`.
- `NormalZombie` and `BossZombie` implement `ProjectZDefense.StatusEffects.IFrostStatusEffectReceiver`.
- `FrostStatusRuntime` owns Frost exposure, hold timer, freeze timer, freeze cooldown, visual toggles, and optional freeze explosion effects.
- `NormalZombie` initializes `FrostStatusRuntime` with freeze explosion enabled and only applies the calculated speed multiplier to cached `MoveSpeed` and `AttackSpeed` animator parameters.
- `BossZombie` initializes `FrostStatusRuntime` with freeze explosion disabled. Boss Frost is slow-only by current code, so `canTriggerFreeze = false` blocks freeze explosion but does not block Frost slow payload delivery.
- `BossZombie` no longer uses the behavior blackboard `speed` variable as the C# runtime speed source. Root-motion movement speed is driven by the Animator `speed` parameter from `baseMoveSpeed * frostSpeedMultiplier`, while `OnAnimatorMove` applies `anim.deltaPosition` through `NavMeshAgent.Move`.
- `BossZombie.ApplyFrostSpeedMultiplier` clamps the applied Frost speed multiplier to at least `0.5`, so Frost can slow bosses by up to 50% but cannot fully stop their movement or attack animation.
- `BossZombie.UpdateMoveAnimatorSpeed` must not write `agent.speed` back into the Animator `speed` parameter. `agent.speed` is only an indirect navigation value from `NavigateToTargetAction`; using it as the root-motion speed source disconnects runtime Frost multipliers from actual movement.
- `FrostStatusRuntime.IsFreezeRetargetSuppressed` becomes true only for freeze-capable targets after freeze starts or while the per-target freeze cooldown remains. Slow buildup alone does not suppress targeting.
- `FrostFreezeSuppressedTargetCandidateFilter` lets `Frost_Turret` keep an actively Frost-slowed current target until freeze triggers, then release enemies that are already frozen or waiting for their next freeze eligibility window on the next fire/search tick. Bosses are not excluded because Boss Frost is slow-only.
- `FrostStatusEffectUtility` owns freeze effect spawning and non-alloc overlap explosion damage so future Frost skills can reuse the same explosion behavior.
- `FrostFreezeExplosionDamageTimer` keeps `Ice_Cubes_Explosion` following the original frozen target while it is alive and delays explosion damage so the damage lands at the current effect position when the visual burst happens.
- `FrostStatusRuntime` keeps the active `Ice_Cubes_Explosion` handle and cancels the effect plus pending explosion damage when the original frozen target dies or is reset for pooling.
- `FrostStatusRuntime.TriggerFreezeDeathEffectIfNeeded` spawns the configured `freezeDeathEffectPrefab` when a target dies while its freeze timer is still active. This is separate from `Ice_Cubes_Explosion`, which is still cancelled on death to prevent delayed damage from a dead original target.
- `StatusEffectVisualController` owns enemy-side status VFX slots. It can spawn one `MeshFX_Frozen 1` instance per configured target renderer, inject the renderer into `OverlayFX`, and restore each renderer's cached `sharedMaterials` array when Frost slow ends.
- Frost overlay material cleanup must restore the original `sharedMaterials` arrays instead of stripping overlay slots by assigning `null`. Null material slots can render magenta on normal zombies after the Frost/slow visual ends.
- `FrostStatusRuntime` reports Frost active/inactive state to `StatusEffectVisualController`; zombies do not directly instantiate or configure mesh VFX.
- `NormalZombie` exposes runtime base speed setters so external buffs such as Screamer speed buffs can change the non-Frost base speed without overwriting the active Frost multiplier.
- `maxSlowRatio` is interpreted as a maximum reduction ratio, so `0.9` means the target keeps `10%` speed when Frost buildup reaches the cap.
- `slowBuildUpDuration` controls how long continuous Frost exposure takes to reach `maxSlowRatio`.
- `slowHoldDuration` controls how long the slow remains after Frost exposure stops.
- `freezeTriggerRatio` controls when the freeze explosion branch should trigger after slow buildup reaches the configured ratio.
- `freezeEffectDuration` controls how long the freeze VFX remains alive before returning to the pool.
- `freezeExplosionDamageDelay` controls when radius damage is applied after the freeze VFX starts.
- `freezePrimaryTargetMaxHpDamageRatio` applies max-HP ratio damage only to the original target that triggered `Ice_Cubes_Explosion`; that original target is excluded from the fixed radius explosion damage so it is damaged once.
- `freezeExplosionSlowRatio` and `freezeExplosionSlowDuration` apply a short secondary slow only to targets damaged by the freeze explosion. This secondary slow cannot trigger another freeze explosion by itself.

Frost pooling and optimization follow-up:

- `Ice_Cubes_Explosion` should be verified as a project-owned PoolObject-compatible prefab before multiple Frost turrets become common in production waves.
- `PooledObjectUtility.SpawnEffect` currently provides a safe fallback if the pool is missing, but repeated Frost freeze explosions should not depend on fallback `Instantiate/Destroy` in normal gameplay.
- Add or tune a MemoryPool prewarm count after profiling expected simultaneous frozen targets.
- `FrostFreezeExplosionDamageTimer` must reset payload, primary target, target transform, damage-pending state, timer, and cached position every time the effect is returned or reused.
- Confirm cancelled effects never apply delayed explosion damage after the original target dies or returns to the zombie pool.
- Future optimization should avoid repeated hierarchy scans while the freeze effect follows a target. Prefer a cached collider, cached aim transform, or enemy-provided status-effect anchor instead of resolving child colliders every frame.
- Profile Frost beams with many zombies for GC allocation, `OverlapSphereNonAlloc` buffer pressure, particle count, and damage tick CPU cost.
- If Frost explosion VFX becomes common, make configured pooling mandatory and remove fallback instantiation from the expected runtime path.

Enemy Frost visual setup:

- Add `StatusEffectVisualController` to each zombie prefab that should show Frost slow visuals.
- Add a `StatusEffectVisualSlot` with `visualType = FrostSlow` and `attachMode = RendererOverlay`.
- Assign `MeshFX_Frozen 1` to that slot's `visualPrefab`.
- Assign only body renderers that should receive the frost overlay into that slot's `targetRenderers`.
- Set `fadeInDuration = 0.4` and `fadeOutDuration = 0.4` on FrostSlow slots when the frost overlay should appear and disappear softly instead of snapping.
- For normal zombies, usually assign the active body `SkinnedMeshRenderer`.
- For boss zombies, assign a boss-specific Frost visual prefab to a FrostSlow `StatusEffectVisualSlot` when `MeshFX_Frozen 1` is too visually noisy or incorrectly scaled.
- For boss zombies, assign body and attachment renderers selectively so hair, eyes, or props can be excluded if needed.
- If boss Frost particles look too large, lower `Frost Particle Scale Multiplier` on the boss `StatusEffectVisualController` instead of scaling the source MeshFX prefab.
- Do not modify the original `OverlayFX` script from Private Assets; project-side lifecycle and material cleanup are handled by `StatusEffectVisualController`.
- `MeshFX_Frozen` RendererOverlay visuals should be prewarmed inside `StatusEffectVisualController`, not through global `MemoryPool`, because each instance must bind to that zombie's renderer.
- Enable `prewarmOnEnable` and add `FrostSlow` to `prewarmVisualTypes` on normal and boss zombie prefabs when first-hit Frost visual instantiation causes runtime spikes.
- RendererOverlay slots cache original renderer material arrays when Frost/Poison visuals are activated and restore those arrays on deactivation or slot cleanup. This is required to avoid magenta fallback materials after `MeshFX_Frozen 1` finishes.
- `freezeCooldownPerTarget` must prevent the same target from triggering freeze explosions every damage tick.
- `freezeDuration > 0` temporarily applies a `0` speed multiplier and overrides slow until the freeze timer expires.
- `freezeEffectPrefab` is owned by the attack/status profile, not by every zombie prefab. Zombies only provide receiver logic and effect position.
- `freezeDeathEffectPrefab` is also owned by the Frost status profile and should be assigned when frozen-target deaths need an extra visual burst after `Ice_Cubes_Explosion` is cancelled.
- Frost timers are updated by `FrostStatusRuntime.Tick` from each zombie's existing `Update` path and reset on spawn, despawn, and death.
- Current `Frost_Turret.prefab` connects `FrostFreezeSuppressedTargetCandidateFilter` to `TargetFinder.targetCandidateFilterBehaviours` so it keeps a partially frozen current target until freeze, then forces the current target to be released on the next fire/search tick after a normal zombie has already triggered freeze.
- `FrostFreezeSuppressedTargetCandidateFilter` exposes its retain/exclude conditions in the Inspector (`retainActiveFrostTargetUntilFreeze`, `excludeFrozenTargets`, `excludeFreezeCooldownTargets`) so designers can verify the Frost targeting policy directly on the turret prefab.

Poison status handling:

- `PoisonStatusProfileSO` stores Poison Lv1/base status values (`maxHpDamageRatioPerTick`, `tickInterval`, `duration`, `maxStackCount`, `stackRefreshMode`, `bossDamageMultiplier`) and can reference a `PoisonDeathBurstProfileSO`.
- `PoisonDeathBurstProfileSO` stores optional lethal-death burst VFX, Lv1/base weak area Poison values, target layer mask, and whether weak Poison can chain-trigger additional normal-zombie death bursts.
- `PoisonTurretStatGrowthProfileSO` owns Poison_Turret-only level growth for Poison tick damage, Poison duration, Poison death burst radius, death burst weak Poison tick damage, and death burst weak Poison duration.
- `TurretDefinitionRuntimeController.ApplyVFX` creates a level-bound Poison payload only for projectile VFX profiles and passes it to the runtime `Turret`.
- The base projectile firing path carries the Poison payload from `Turret` to `FiringEvent`, `Gun`, and `ProjectileDamageDealer`.
- `ProjectileDamageDealer` applies direct projectile damage first, then forwards Poison to targets implementing `ProjectZDefense.StatusEffects.IPoisonStatusEffectReceiver`.
- `NormalZombie` and `BossZombie` implement `ProjectZDefense.StatusEffects.IPoisonStatusEffectReceiver`.
- `PoisonStatusRuntime` owns per-target Poison duration, tick timer, stack count, lethal-pending state, visual toggles, and optional death burst trigger.
- `PoisonStatusRuntimeUtility` owns shared Poison tick timing, remaining tick count, and max-HP tick damage calculations.
- `NormalZombie` initializes `PoisonStatusRuntime` with normal damage rules and allows lethal death burst.
- `BossZombie` initializes `PoisonStatusRuntime` with boss damage multiplier rules and blocks lethal death burst.
- `RefreshDurationOnly` refreshes Poison duration without increasing stack count after the first stack.
- `AddStackAndRefreshDuration` increases stack count up to `maxStackCount` and refreshes duration.
- Poison ticks start after `tickInterval`; direct projectile hit damage remains separate from Poison tick damage.
- Poison status resets on spawn, despawn, and death.
- `StatusEffectVisualController` owns Poison visual slots separately from Frost visual slots.
- Poison visuals can use multiple `StatusEffectVisualSlot` entries on `StatusEffectVisualController`. Use `Anchor` slots for world/character anchored particle effects such as foot miasma or body aura, and `RendererOverlay` slots only for OverlayFX-style renderer-bound mesh effects.
- Poison visual slots can optionally set `Lethal Indicator Child Name`. When remaining Poison ticks are enough to kill the target at its current HP, `NormalZombie` and `BossZombie` toggle that cached child object, for example `PoisonIcon`, without per-frame hierarchy searches.
- `NormalZombie` triggers `PoisonDeathBurstEffectUtility` only when it dies while `IsPoisonLethalPending` is true and the active Poison payload has a death burst profile.
- `BossZombie` can show the lethal indicator and can receive weak area Poison from a normal zombie's burst, but boss death does not trigger the Poison death burst.
- `PoisonDeathBurstProfileSO.allowChainDeathBurst` controls whether weak Poison payloads keep the same death burst profile. When enabled, weak Poison can show the lethal indicator and chain another normal-zombie death burst.
- Poison death burst uses `Physics.OverlapSphereNonAlloc` with a fixed buffer and deduplicates `ProjectZDefense.StatusEffects.IPoisonStatusEffectReceiver` targets before applying weak Poison.

Poison runtime pipeline:

1. `Poison_Turret_Definition.poisonStatusProfile` points to the active `PoisonStatusProfileSO`.
2. `TurretDefinitionRuntimeController.ApplyVFX` creates a `ProjectZDefense.StatusEffects.PoisonStatusPayload` from `PoisonStatusProfileSO` and `TurretStatGrowthProfileSO` when the selected VFX is projectile-based.
3. The payload is stored on the runtime `Turret` and passed through `FiringEvent`, `Gun`, and `ProjectileDamageDealer` each projectile spawn.
4. `ProjectileDamageDealer.TryApplyDamage` applies direct projectile damage first through `IDamageable.TakeDamage`.
5. If the damaged target still lives and implements `ProjectZDefense.StatusEffects.IPoisonStatusEffectReceiver`, `ProjectileDamageDealer` calls `ApplyPoisonStatus`.
6. `NormalZombie` and `BossZombie` delegate Poison timers, stack count, tick timer, lethal-pending state, and visual toggles to `PoisonStatusRuntime`.
7. Poison tick damage uses the same `TakeDamage` path as direct damage so HP UI, popups, rewards, and death flow stay consistent.
8. Every Poison update recalculates whether the remaining scheduled ticks can kill the target; if yes, `IsPoisonLethalPending` becomes true and the visual controller turns on the configured lethal indicator child.
9. `TargetFinder` calls connected `ITargetCandidateFilter` components. `PoisonLethalTargetCandidateFilter` lets Poison turret targeting skip targets whose `IsPoisonLethalPending` is already true.
10. If a normal zombie dies while `IsPoisonLethalPending` is true and the active payload has a `deathBurstProfile`, `NormalZombie.Die` calls `PoisonDeathBurstEffectUtility.TriggerDeathBurst`.
11. `PoisonDeathBurstEffectUtility` spawns the configured burst VFX through `PooledObjectUtility.SpawnEffect`.
12. The same utility applies weak area Poison to nearby living `ProjectZDefense.StatusEffects.IPoisonStatusEffectReceiver` targets using `Physics.OverlapSphereNonAlloc`.
13. Weak area Poison can chain only if `PoisonDeathBurstProfileSO.allowChainDeathBurst` is enabled.
14. Boss zombies can receive direct Poison, weak area Poison, and lethal indicators, but boss death never calls the Poison death burst trigger.

Poison data ownership:

- `PoisonStatusProfileSO` owns base Poison rules and values that should exist at tier level 1.
- `PoisonDeathBurstProfileSO` owns base death-burst VFX, target layer mask, chain toggle, and base weak area Poison rules.
- `PoisonTurretStatGrowthProfileSO` owns Poison-specific level-scaling values. Do not add per-level Poison balancing fields to `PoisonStatusProfileSO` or the shared `TurretStatGrowthProfileSO`.
- `TurretDefinitionRuntimeController` combines the status profile and stat growth profile into one immutable `ProjectZDefense.StatusEffects.PoisonStatusPayload` for the current level.
- Non-Poison turret growth assets should use `TurretStatGrowthProfileSO`, so they do not show Poison-only fields in the Inspector.
- `Poison_Turret_Stat Growth Profile SO` should use `PoisonTurretStatGrowthProfileSO`.
- `PoisonDeathBurstEffectUtility` uses the precomputed values inside `ProjectZDefense.StatusEffects.PoisonStatusPayload`, not raw profile values, so death-burst weak Poison stays level-scaled during chain explosions.
- `PoisonStatusProfileSO.HasPoisonStatus` is only a base-profile quick check. Runtime activation is decided from the scaled payload so a profile with low base values can still become active through growth settings.
- Per-target Poison state changes should be fixed in `PoisonStatusRuntime`, while pure tick math edge cases should be fixed in `PoisonStatusRuntimeUtility`.

Electro status handling:

- `ElectroStatusProfileSO` stores fixed/base Electro values: chain lightning count/radius/damage falloff, base Shock stack duration, Overload trigger policy, base single-target Overload damage values, max Shock stack count, Overload stun timing, and short stun timing.
- `ElectroTurretStatGrowthProfileSO` owns Electro_Turret-only level growth for Shock stack duration, chain target count, normal Overload max-HP damage ratio, and boss Overload max-HP damage ratio. Keep `maxShockStackCount`, `chainRadius`, `overloadStunDuration`, and short stun timing fixed in `ElectroStatusProfileSO` unless the balance direction changes.
- `TurretDefinitionRuntimeController.ApplyVFX` creates an Electro payload only for projectile VFX profiles and passes it to the runtime `Turret`.
- The base projectile firing path carries the Electro payload from `Turret` to `FiringEvent`, `Gun`, and `ProjectileDamageDealer`.
- `ProjectileDamageDealer.TryApplyDamage` applies direct projectile damage first, then forwards Electro status to targets implementing `ProjectZDefense.StatusEffects.IElectroStatusEffectReceiver`.
- `ElectroChainLightningUtility` starts from the directly hit target, finds living `IDamageable` targets inside `chainRadius` with `Physics.OverlapSphereNonAlloc`, deduplicates targets, excludes Overload-stunned targets, then prioritizes non-full higher Shock stack counts before distance. Full-Shock targets remain fallback candidates so Electro hits can refresh their Shock timer when no better chain candidate exists.
- Chain damage uses the same `IDamageable.TakeDamage` path as direct projectile damage, so HP, damage popups, rewards, and death flow stay consistent.
- `NormalZombie` and `BossZombie` implement `ProjectZDefense.StatusEffects.IElectroStatusEffectReceiver` and delegate Shock stack state to `ElectroStatusRuntime`.
- `ElectroStatusRuntime` adds one Shock stack whenever an Electro direct hit or chain hit is received, refreshes the stack timer to `shockStackDuration`, and caps visible stacks at the configured max up to three.
- `ElectroShockTargetCandidateFilter` can be attached to Electro turret `TargetFinder.targetCandidateFilterBehaviours` to exclude Overload-stunned targets and prefer non-full Shock targets for first-shot selection. Full-Shock targets can be used as fallback first-shot targets when every valid candidate is otherwise excluded, so Shock timers can still be refreshed.
- Current Shock stack VFX uses `Volt Sphere 1` from `Scenes/KKW/Turret_Scene/Prefabs/Status Effect/Electro_Turret`. The runtime keeps up to three instances around the target body center and rotates them like orbiting rings while the stack timer is active.
- `ElectroStatusProfileSO` controls normal Shock stack orbit radius, optional boss-only orbit radius, vertical offset, rotation speed, normal/boss visual scale, and camera-facing back-side alpha fade. Hard back-side hiding still exists as a fallback, but the default depth cue is smooth alpha fading instead of instant activation toggling.
- Shock stack VFX can use charged visual mode. In the default Electro profile, 1-2 stacks keep a calmer Volt Sphere by disabling selected sparkle children, and 3 stacks re-enable all children to show the fully charged state.
- Electro hits also apply a short stun through `IElectroStunRuntimeOwner`. The active profile controls short hit stun with `stunDuration`, excludes bosses from short hit stun when `bossHitStunDurationMultiplier = 0`, and uses `bossStunDurationMultiplier` for Overload long-stun tuning. `StatusEffectVisualController` exposes an `ElectroStun` visual slot for short stun VFX such as `FX_Electricity_02 1`.
- Electro attacks do not consume three Shock stacks. When a target has full Shock stacks, non-Electro projectile or beam damage can trigger Overload through `IElectroOverloadTriggerReceiver`.
- Overload currently consumes Shock stacks, plays `overloadImpactEffectPrefab`, applies single-target max-HP ratio damage, and applies long stun using `overloadStunDuration`. While the Overload long-stun timer is active, additional Electro hits do not add new Shock stacks. Bosses use separate max-HP damage ratio and boss stun multiplier values.
- `ElectroChainLinkEffectUtility` renders the chain visual between each chained target pair using the VFX settings stored in `ElectroStatusProfileSO`.
- Electro chain visuals are hybrid by design. `PS_Electro_ChainLink` provides the wide stylized particle lightning, while `ElectroChainCoreLineEffect` provides a thin exact `LineRenderer` connection so the start and end points read clearly.
- Current chain-link particle VFX uses project-owned `PS_Electro_ChainLink`, a duplicate of `PS_LightiningStrike 1` with floor/impact children disabled and lightning/spark children kept active. Current kept children are `Holder`, `Lightning_Arc`, `Lightning`, `Lightning_Big`, `Sparks`, and `Flare`.
- The particle link is still approximate because the imported lightning particle meshes do not map perfectly to two exact endpoint anchors. Do not try to make the particle prefab alone solve exact endpoint readability; the core line owns exact connection readability.
- Tune `chainLinkEffectDuration`, `chainLinkVerticalOffset`, `chainLinkSourceAxis`, `chainLinkLocalPositionOffset`, `chainLinkRotationEulerOffset`, `chainLinkLengthScaleMultiplier`, and `chainLinkThicknessScale` first when the particle link is too short, too high/low, rotated incorrectly, offset from targets, too stretched, or too thick.
- When `useChainLinkEndpointFit` is enabled, `chainLinkLocalStartPoint` and `chainLinkLocalEndPoint` define the local-space visual segment that should be mapped to the previous and next chained target positions. The current `PS_Electro_ChainLink` default is `(0,0,0)` to `(0,0,15)` because the main lightning children extend along local Z.
- `ProjectileDamageDealer` passes the first hit collider into `ElectroChainLightningUtility`. Later chain jumps use the collider returned by the non-alloc overlap search. VFX anchors prefer `Collider.bounds.center`, then fall back to target `Transform.position`, then to the cached fallback position.
- `ElectroChainLinkAnchorTracker` keeps a spawned chain-link effect following the latest collider centers while the effect is alive. This compensates for the imported particle prefab's delayed start timing and moving zombies.
- `ElectroChainCoreLineEffect` is attached at runtime to the spawned chain-link effect object. It creates or reuses a two-point world-space `LineRenderer`, uses the same vertical offset as the particle link, and updates the line endpoints from `ElectroChainLinkAnchorTracker`.
- Core line timing is controlled separately through `chainCoreLineStartDelay` and `chainCoreLineDuration`. Use this to align the exact line with the delayed lightning particle flash without keeping a straight line visible longer than intended.
- `chainCoreLineDuration <= 0` means the core line remains visible from `chainCoreLineStartDelay` until the chain-link effect returns to the pool.
- Chain-link VFX reads live values from `ElectroStatusProfileSO` through the payload's source profile reference, so play-mode Inspector edits to those VFX fields affect newly spawned chain links without reapplying the turret definition.
- Enemy-side Shock stack runtime behavior is implemented through `ElectroStatusRuntime`, including Shock stack visuals, short Electro hit stun, and non-Electro Overload consumption.

Electro runtime pipeline:

1. `Electro_Turret_Definition.electroStatusProfile` points to the active `ElectroStatusProfileSO`.
2. `TurretDefinitionRuntimeController.ApplyVFX` creates a `ProjectZDefense.StatusEffects.ElectroStatusPayload` from `ElectroStatusProfileSO` and the current stat growth profile when the selected VFX is projectile-based.
3. The payload is stored on the runtime `Turret` and passed through `FiringEvent`, `Gun`, and `ProjectileDamageDealer` each projectile spawn.
4. `ProjectileDamageDealer.TryApplyDamage` applies direct projectile damage first through `IDamageable.TakeDamage`.
5. If the damaged target still lives and implements `ProjectZDefense.StatusEffects.IElectroStatusEffectReceiver`, `ProjectileDamageDealer` calls `ApplyElectroStatus` with chain index `0`.
6. `ElectroChainLightningUtility` searches from the direct hit collider center and applies chain damage to up to `maxChainTargets - 1` additional targets.
7. Each bounced target receives damage scaled by `1 - chainDamageFalloffPerJump * chainIndex`, clamped at `0`.
8. Each bounced target that implements `ProjectZDefense.StatusEffects.IElectroStatusEffectReceiver` also receives `ApplyElectroStatus` with its chain index.
9. A short chain-link particle effect is spawned between the previous target collider center and the bounced target collider center.
10. `ElectroChainLinkAnchorTracker` updates the spawned link every `LateUpdate` while alive so moving zombies do not leave the visual stuck at the original hit position.
11. `ElectroChainCoreLineEffect` overlays a thin exact line between the same endpoints during its configured timing window.
12. `ElectroStatusRuntime` refreshes Shock stack duration and shows one to three orbiting `Volt Sphere 1` instances around the target body center.
13. Hit or overload particle bursts remain separate from the chain-link/core-line VFX and can be layered later.

Electro chain VFX field guide:

| Field | Runtime effect |
| --- | --- |
| `playChainLinkEffect` | Enables the particle chain-link effect. |
| `chainLinkEffectPrefab` | Project-owned chain particle prefab, currently `PS_Electro_ChainLink`. |
| `chainLinkEffectDuration` | Total lifetime before `PooledEffectReturner` returns the spawned link effect. Must be long enough for delayed particle children such as `Lightning` and `Lightning_Arc` to start. |
| `chainLinkVerticalOffset` | Shared height offset added to particle and core-line endpoints after resolving collider centers. Lower this when using collider-center anchors instead of root pivots. |
| `chainLinkSourceAxis` | Local axis treated as the prefab's length direction. Current default is local Z. |
| `useChainLinkEndpointFit` | Maps `chainLinkLocalStartPoint` and `chainLinkLocalEndPoint` onto the runtime endpoints. Keep enabled for `PS_Electro_ChainLink`. |
| `chainLinkLocalStartPoint` | Local point that should align to the previous chained target. |
| `chainLinkLocalEndPoint` | Local point that should align to the next chained target. |
| `chainLinkLocalPositionOffset` | Extra local position offset after endpoint fitting. Use only for small visual centering fixes. |
| `chainLinkRotationEulerOffset` | Extra rotation offset after aligning `chainLinkSourceAxis` to the chain direction. |
| `chainLinkLengthScaleMultiplier` | Manual length multiplier used when endpoint fit is disabled or local endpoint distance is invalid. |
| `chainLinkThicknessScale` | Width/thickness scale for non-length axes. |
| `playChainCoreLine` | Enables the exact two-point core `LineRenderer`. |
| `chainCoreLineMaterial` | Material used by the core line. Current asset uses the same lightning material as the particle chain. |
| `chainCoreLineStartColor` | Start color for the core line. |
| `chainCoreLineEndColor` | End color for the core line. |
| `chainCoreLineWidth` | Core line width. Keep thin enough to act as an exact connection guide rather than replacing the particle VFX. |
| `chainCoreLineStartDelay` | Delay before the core line appears. Tune to match the imported particle's delayed lightning flash. |
| `chainCoreLineDuration` | How long the core line remains visible after start delay. `0` keeps it visible until the link effect returns. |

Current `Electro Status Profile SO` VFX test values:

- `chainLinkEffectDuration = 1.5`
- `chainLinkVerticalOffset = 0.15`
- `chainLinkSourceAxis = (0, 0, 1)`
- `useChainLinkEndpointFit = true`
- `chainLinkLocalStartPoint = (0, 0, 0)`
- `chainLinkLocalEndPoint = (0, 0, 15)`
- `chainLinkLengthScaleMultiplier = 0.55`
- `chainLinkThicknessScale = 0.55`
- `playChainCoreLine = true`
- `chainCoreLineWidth = 0.06`
- `chainCoreLineStartDelay = 0.2`
- `chainCoreLineDuration = 0.2`

Electro VFX optimization notes:

- Chain target search uses `Physics.OverlapSphereNonAlloc` and static reusable buffers. Do not replace it with allocating overlap calls.
- Each chain jump can spawn one particle link and one two-point core line. With `maxChainTargets = 10`, a single hit can create up to `9` link visuals.
- `PS_Electro_ChainLink`, `PS_Lightining_Impac`, and `Stun 1` are repeated Electro gameplay effects and should be registered in `MemoryPoolPrewarmer`.
- Electro chain-link prefabs should keep `PoolObject`, `PooledEffectReturner`, `ElectroChainLinkAnchorTracker`, and `ElectroChainCoreLineEffect` on the root object so first chain playback does not rely on runtime component attachment.
- Electro Overload impact and long-stun effects should keep `PoolObject` and `PooledEffectReturner` on the root object.
- `Volt Sphere 1` Shock stack visuals are target-owned orbit visuals created and reused by `ElectroStatusRuntime`; do not register them in the global `MemoryPool`.
- Normal and boss zombie prefabs can assign `ElectroStatusProfileSO` to their Electro visual prewarm field so `ElectroStatusRuntime` creates up to three inactive Shock stack visuals during enemy initialization instead of on first Electro hit.
- `ElectroChainLinkAnchorTracker` runs only while the spawned link effect is alive. Keep `chainLinkEffectDuration` reasonable if many Electro turrets can fire at once.
- `LineRenderer` work is intentionally limited to two world-space points. Avoid adding per-frame generated multi-point noise unless profiling confirms headroom.
- If many Electro turrets become common, reduce cost in this order: lower `chainLinkEffectDuration`, lower `maxChainTargets`, lower `chainCoreLineDuration`, disable `Lightning_Big`, then consider skipping VFX on some later chain jumps.

Poison growth fields on `PoisonTurretStatGrowthProfileSO`:

| Field | Runtime effect |
| --- | --- |
| `poisonMaxHpDamageRatioPerTickPerLevel` | Adds to direct Poison max-HP tick damage ratio per completed tier level. |
| `maxPoisonMaxHpDamageRatioPerTick` | Caps direct Poison max-HP tick damage ratio. |
| `poisonDurationPerLevel` | Adds direct Poison duration per completed tier level. |
| `maxPoisonDuration` | Optional direct Poison duration cap. `0` means uncapped. |
| `poisonDeathBurstRadiusPerLevel` | Adds to death-burst weak area Poison radius per completed tier level. |
| `maxPoisonDeathBurstRadius` | Optional death-burst radius cap. `0` means uncapped. |
| `poisonDeathBurstMaxHpDamageRatioPerTickPerLevel` | Adds to weak area Poison max-HP tick damage ratio per completed tier level. |
| `maxPoisonDeathBurstMaxHpDamageRatioPerTick` | Caps weak area Poison max-HP tick damage ratio. |
| `poisonDeathBurstDurationPerLevel` | Adds weak area Poison duration per completed tier level. |
| `maxPoisonDeathBurstDuration` | Optional weak area Poison duration cap. `0` means uncapped. |

Electro growth fields on `ElectroTurretStatGrowthProfileSO`:

| Field | Runtime effect |
| --- | --- |
| `shockStackDurationPerLevel` | Adds to Shock stack duration per completed tier level. |
| `maxShockStackDuration` | Optional Shock stack duration cap. `0` means uncapped. |
| `chainTargetCountIntervalLevel` | Chain target count growth interval. `0` disables interval growth. |
| `chainTargetCountPerInterval` | Adds to maximum chain target count each time the interval is completed. |
| `maxChainTargetCount` | Optional maximum chain target count cap. `0` means uncapped. |
| `overloadMaxHpDamageRatioPerLevel` | Adds to normal-target Overload max-HP damage ratio per completed tier level. |
| `maxOverloadMaxHpDamageRatio` | Caps normal-target Overload max-HP damage ratio. |
| `bossOverloadMaxHpDamageRatioPerLevel` | Adds to boss-target Overload max-HP damage ratio per completed tier level. |
| `maxBossOverloadMaxHpDamageRatio` | Caps boss-target Overload max-HP damage ratio. |

Poison lethal indicator pipeline:

- `StatusEffectVisualController` owns gameplay-agnostic visual slots.
- Poison visuals should be configured through `StatusEffectVisualSlot` entries instead of one-off scripts on zombie prefabs.
- `Lethal Indicator Child Name` is a slot-level child lookup name. Current Poison aura uses `PoisonIcon`.
- The child lookup is cached per spawned visual instance, so the controller does not search the hierarchy every frame.
- `Lethal Indicator Local Position Offset` moves the cached indicator child after it is found. Use this to pull `PoisonIcon` forward or upward so it does not overlap the zombie body.
- The indicator is a prediction based on currently known Poison ticks. If another turret damages the target after the indicator appears, the target may die earlier, but the indicator remains valid because Poison alone was already enough to finish it.
- The remaining tick calculation intentionally excludes ambiguous duration-boundary ticks from the lethal prediction. This prevents a target that is one tick short of dying from briefly showing `PoisonIcon` and then losing it on the next update.
- The remaining tick calculation must not add epsilon to the full interval division. Tick-boundary tolerance belongs to actual tick application only, not to lethal prediction.
- Lethal prediction should be conservative: if a floating-point boundary is ambiguous, prefer showing the icon late instead of showing it when Poison is not actually lethal.
- Non-Poison damage calls refresh the lethal prediction immediately when the target is still alive, so other turrets can reveal a now-guaranteed Poison execution without waiting for the next Poison timer update.
- The indicator is turned off on Poison expiration, spawn reset, despawn reset, and death reset.

Poison death burst and chain rules:

- Only `NormalZombie` triggers Poison death burst.
- `BossZombie` is intentionally excluded from burst triggering even if its lethal indicator is visible.
- The dead source target is excluded from the weak area Poison overlap result.
- Duplicate colliders under the same target are deduplicated before applying weak Poison.
- Weak Poison is created by `PoisonDeathBurstEffectUtility` from the already scaled `ProjectZDefense.StatusEffects.PoisonStatusPayload`.
- When `allowChainDeathBurst` is true, that weak payload keeps the same `deathBurstProfile`; when false, the weak payload sets `deathBurstProfile = null`.
- Chain explosions are therefore data-driven and can be disabled from the profile without changing code.
- Chain behavior should be tuned carefully because multiple overlapping bursts can quickly multiply area pressure.

Poison performance notes:

- Poison timers run inside each affected zombie's existing `Update` path.
- `NormalZombie` and `BossZombie` only call `PoisonStatusRuntime.Tick`; they should not grow separate Poison timer or stack fields again.
- Target exclusion filters are checked only during `TargetFinder.FindNearestTarget`, not every frame for every zombie.
- Area Poison uses a fixed collider buffer and does not allocate via `Physics.OverlapSphere`.
- Burst VFX should be backed by `PoolObject`/`MemoryPool` when the chain setting is enabled for production waves.
- `TargetFinder.showDebug` is intended for scene debug lines. Candidate-loop console logs should stay disabled or one-shot only because repeated target scans can otherwise produce many messages.

## Poison Projectile Setup

Current Poison projectile assets:

| Asset | Role |
| --- | --- |
| `Prefabs/Turret/3rdGen/Poison_Turret.prefab` | Runtime turret prefab. |
| `SO/TurretDefinition/3rdGen/Poison_Turret_Definition.asset` | Poison turret definition. |
| `SO/TurretVfxProgresstion/3rdGen/Poison_Turret_VFX Progression SO.asset` | Selects the Poison projectile VFX profile. |
| `SO/VFXProfiles/Projectile/VFX_Slime/VFX_Slime 1.asset` | Current Poison projectile VFX profile. |
| `SO/Turret Stat Profile/3rdGen/Poison_Status_Profile_SO.asset` | Current Poison status profile connected to the turret definition. |
| `SO/AttackProfiles/Poison Death Burst Profile SO.asset` | Current Poison death burst and weak area Poison profile. |
| `Prefabs/Status Effect/Poison_Turret/MiasmaCannister 1.prefab` | Current foot/ground Poison visual. |
| `Prefabs/Status Effect/Poison_Turret/19_Poison_Aura_Hazard_Mixed 1.prefab` | Current body Poison aura visual containing the optional `PoisonIcon` child. |

Required Poison wiring:

- `Poison_Turret_Definition.poisonStatusProfile` must point to a Poison status profile asset.
- If Poison lethal-death burst is required, create a `Create > Project Z Defense > Poison Death Burst Profile` asset and assign it to `PoisonStatusProfileSO.deathBurstProfile`.
- `Poison_Turret_Definition.vfxProgressionProfile` must select a projectile VFX profile, not a beam profile.
- The selected projectile prefab must have or receive `ProjectileDamageDealer` through the existing projectile spawn path.
- The selected projectile prefab should already be registered in the MemoryPool prewarm list if it is used frequently.
- Normal and boss zombie prefabs should have `StatusEffectVisualController` Poison fields configured only when Poison visuals are required.
- `Poison_Turret.prefab` must have `SimpleFire` connected as `TurretDefinitionRuntimeController.targetFiringEvent`.
- `SimpleFire.gunPrefabs[0]` must reference the turret root object that owns `Gun`, not the visual head/rotator child.
- `TargetFinder.pivotObject` should reference `FireNozzle` so range and line-of-sight checks originate from the muzzle area.
- Current `Poison_Turret.prefab` intentionally uses `TargetFinder.aimHeightRatio = 0`. Although most turrets use `0.35`, this value stabilized Poison projectile firing angle in play-mode testing.
- Current `Poison_Turret.prefab` connects `PoisonLethalTargetCandidateFilter` to `TargetFinder.targetCandidateFilterBehaviours` so it stops spending shots on targets already guaranteed to die from Poison ticks.
- If `TargetFinder.aimHeightRatio` is changed later, retest muzzle alignment, fire gating, projectile travel, and target jitter together; do not treat it as a purely visual setting.

Recommended Poison Status Profile values for first testing:

| Field | Suggested Value | Note |
| --- | ---: | --- |
| `maxHpDamageRatioPerTick` | `0.01` | 1% max HP per tick per stack. |
| `tickInterval` | `1.0` | One tick per second. |
| `duration` | `4.0` | Lasts long enough for several ticks. |
| `maxStackCount` | `3` | Allows repeated hits to matter without unbounded scaling. |
| `stackRefreshMode` | `AddStackAndRefreshDuration` | Repeated hits increase stacks and refresh duration. |
| `bossDamageMultiplier` | `0.5` | First-pass boss resistance value. |

Current `Poison_Status_Profile_SO` test values:

- `maxHpDamageRatioPerTick = 0.05`
- `tickInterval = 1.0`
- `duration = 12.0`
- `maxStackCount = 1`
- `stackRefreshMode = AddStackAndRefreshDuration`
- `bossDamageMultiplier = 1.0`
- `deathBurstProfile = Poison Death Burst Profile SO`

Current `Poison_Turret_Stat Growth Profile SO` Poison growth test values:

- `poisonMaxHpDamageRatioPerTickPerLevel = 0`
- `maxPoisonMaxHpDamageRatioPerTick = 1`
- `poisonDurationPerLevel = 0`
- `maxPoisonDuration = 0`
- `poisonDeathBurstRadiusPerLevel = 0`
- `maxPoisonDeathBurstRadius = 0`
- `poisonDeathBurstMaxHpDamageRatioPerTickPerLevel = 0`
- `maxPoisonDeathBurstMaxHpDamageRatioPerTick = 1`
- `poisonDeathBurstDurationPerLevel = 0`
- `maxPoisonDeathBurstDuration = 0`
- These zero growth values intentionally preserve current play-mode balance while moving future level-scaling responsibility to the stat growth profile.

Recommended Poison Death Burst Profile values for first testing:

| Field | Suggested Value | Note |
| --- | ---: | --- |
| `burstEffectPrefab` | poison burst VFX prefab | Use a project-owned duplicate or pool-compatible prefab when possible. |
| `effectDuration` | `3.0` | Match the particle lifetime. |
| `effectFadeOutDuration` | `0.5` | Fades pooled particle alpha before return so the burst does not disappear abruptly. |
| `radius` | `2.5` | First test range for weak area Poison. |
| `targetLayerMask` | zombie damage layers | Must include normal and boss zombie damage layers if bosses should receive weak area Poison. |
| `maxHpDamageRatioPerTick` | `0.002` | Weak Poison should be much lower than direct Poison. |
| `tickInterval` | `1.0` | Keep readable for play-mode verification. |
| `duration` | `2.0` | Short enough to remain a bonus effect, not a second main Poison. |
| `maxStackCount` | `1` | Recommended first value to avoid burst overlap scaling too fast. |
| `stackRefreshMode` | `RefreshDurationOnly` | Keeps repeated weak bursts predictable. |
| `bossDamageMultiplier` | `0.2` to `1.0` | Bosses can receive weak area Poison, but tune separately from normal zombies. |
| `allowChainDeathBurst` | `true` | Allows weak Poison lethal kills on normal zombies to trigger another burst. Turn off if chain reactions overperform. |

Current `Poison Death Burst Profile SO` test values:

- `burstEffectPrefab = 3_Poison_MushRoom 1` or the currently connected project-owned Poison burst prefab.
- `effectDuration = 2.0`
- `effectFadeOutDuration = 0.5`
- `radius = 3.5`
- `targetLayerMask.m_Bits = 17536`
- `maxHpDamageRatioPerTick = 0.02`
- `tickInterval = 0.5`
- `duration = 5.0`
- `maxStackCount = 1`
- `stackRefreshMode = RefreshDurationOnly`
- `bossDamageMultiplier = 1.0`
- `allowChainDeathBurst = true`

Enemy Poison visual setup:

- Add or keep `StatusEffectVisualController` on every zombie prefab that should display Poison visuals.
- Add a Poison `Anchor` slot for `MiasmaCannister 1` and assign `PoisonFootAnchor`.
- Add a Poison `Anchor` slot for `19_Poison_Aura_Hazard_Mixed 1` and assign `PoisonBodyAnchor`.
- On the body aura slot, set `Lethal Indicator Child Name = PoisonIcon` if the aura prefab contains that disabled child.
- If `PoisonIcon` overlaps the character body, tune `Lethal Indicator Local Position Offset` on the body aura slot. Current starting values are `0, 0.15, 0.35` for `NZ_AirportSecurity` and `0, 0.35, 0.8` for `BossZombie_Tank`.
- Leave legacy `Poison Visual Prefab` empty when using `Visual Slots`; the slot list is the preferred current setup.
- Boss prefabs may use the same visual flow, but boss death still does not trigger Poison death burst.

Poison play-mode verification checklist:

1. Place `Poison_Turret` in `2nd Main Scene`.
2. Confirm the turret rotates toward a valid zombie and fires a projectile from the configured gun/muzzle.
3. Confirm direct projectile damage lands before Poison status is applied.
4. Wait for `tickInterval` and confirm Poison tick damage uses max-HP ratio damage.
5. Re-hit the same target and confirm stack policy matches `PoisonStatusProfileSO.stackRefreshMode`.
6. Damage a Poisoned target with another turret and confirm `PoisonIcon` turns on when the remaining Poison ticks alone can finish the current HP.
7. Confirm `Poison_Turret` stops choosing targets with visible lethal indicators when `PoisonLethalTargetCandidateFilter` is connected to `TargetFinder`.
8. Kill a lethal-indicator normal zombie and confirm burst VFX spawns.
9. Confirm nearby normal zombies receive weak area Poison.
10. Confirm nearby boss zombies receive weak area Poison.
11. Confirm a lethal-indicator boss death does not spawn a Poison death burst.
12. With `allowChainDeathBurst` enabled, confirm weak Poison can create another normal-zombie burst if it becomes lethal.
13. With `allowChainDeathBurst` disabled, confirm weak Poison can still damage targets but does not trigger additional bursts.
14. Watch Unity Profiler for repeated burst VFX fallback instantiation if MemoryPool prewarm is not configured.

Poison troubleshooting notes:

- If `Poison_Turret` rotates but does not fire, check `TurretDefinitionRuntimeController.targetFiringEvent`, `SimpleFire.gunPrefabs`, and the root `Gun` reference first.
- If the range/line-of-sight gizmo appears to start from the wrong place, check `TargetFinder.pivotObject`; current Poison setup expects `FireNozzle`.
- If firing angle becomes unstable, keep `TargetFinder.aimHeightRatio = 0` for Poison before trying code changes.
- If PoisonIcon never appears, check the body aura slot's `Lethal Indicator Child Name` and verify the aura prefab actually contains a child named `PoisonIcon`.
- If bursts do not chain, check `PoisonDeathBurstProfileSO.allowChainDeathBurst`, weak Poison damage values, and whether the dying target is a normal zombie rather than a boss.
- If bursts hit nothing, check `PoisonDeathBurstProfileSO.targetLayerMask` against the zombie damage layers.

Poison current setup checklist:

- `Poison_Turret_Definition.poisonStatusProfile` is connected to the active Poison status profile asset.
- Run `Project Z Defense/Validation/Validate Turret Economy`; the validator should report no missing `poisonStatusProfile` for `poison_turret`.
- Confirm `Poison_Turret.prefab` loads normally in Unity and keeps valid prefab/source references.
- Confirm the Poison turret uses a projectile VFX profile, not a beam profile.
- Play-mode test direct hit damage first, then verify Poison ticks start after `tickInterval`.
- Verify stack behavior with repeated hits: `RefreshDurationOnly` should not increase stacks, while `AddStackAndRefreshDuration` should increase up to `maxStackCount`.
- If Poison visual feedback is needed, configure `StatusEffectVisualController` Poison fields on normal and boss zombie prefabs after choosing the visual prefab.
- For the current Poison VFX setup, add two Poison `Anchor` slots on each relevant zombie prefab: `MiasmaCannister 1` on `PoisonFootAnchor`, and `19_Poison_Aura_Hazard_Mixed 1` on `PoisonBodyAnchor`.
- If `19_Poison_Aura_Hazard_Mixed 1` contains a disabled `PoisonIcon` child, set that slot's `Lethal Indicator Child Name` to `PoisonIcon` so it becomes visible only when remaining Poison ticks guarantee the kill.
- To enable lethal-death burst, create a `Poison Death Burst Profile`, assign its burst VFX and weak Poison values, then connect it to the active `Poison Status Profile` through `Death Burst Profile`.
- After functional validation, tune `maxHpDamageRatioPerTick`, `duration`, `maxStackCount`, and `bossDamageMultiplier` against late-wave HP values.

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
- `Frost_Turret_Definition.frostStatusProfile` points to the Frost status profile asset that owns slow, freeze, explosion, and optional primary-target max-HP damage growth values.
- `BeamFiringEvent.gunPrefabs` can point to the root if the root has the `Gun` component.
- `Gun.muzzleObject` must point to the actual `FireNozzle` object. If it points to the turret mesh, such as `SM_Laser_Gun_Base`, the beam starts from the wrong position and may appear to aim downward.
- `TargetFinder.pivotObject` should point to a rotating head or muzzle-related object so range and line-of-sight checks use a sensible origin.
- `Frost_Turret` stat `fireInterval` should be near `0.1` to `0.2` for responsive continuous beam startup.
- `Frost_Turret` stat `projectileSpeed` has little runtime impact because Frost uses `BeamFiringEvent` instead of a moving projectile. Do not delete or zero it; keep it as a compatibility/stat-report value.

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

Recommended Frost Status Profile values for first testing:

| Field | Suggested Value | Note |
| --- | ---: | --- |
| `slowBuildUpDuration` | `1.0` | Continuous exposure time to reach maximum slow. |
| `maxSlowRatio` | `0.9` | Target keeps 10% movement/attack speed at full buildup. |
| `slowHoldDuration` | `1.0` | Slow retention time after beam exposure stops. |
| `freezeTriggerRatio` | `0.9` | Trigger freeze branch when slow reaches the cap. |
| `freezeEffectPrefab` | `IceCubesExplosion` | Assign in editor when the project-owned effect prefab is available. |
| `freezeEffectDuration` | `5.5` | Keep long enough for `Ice_Cubes_Explosion` particles with 5s lifetime to finish. |
| `freezeExplosionDamageDelay` | `2.2` | Delay radius damage until the visual cube burst moment. |
| `freezeExplosionRadius` | `2.5` | First test radius for freeze explosion damage. |
| `freezeExplosionDamage` | `0` | Keep zero until explosion damage balance is decided. |
| `freezePrimaryTargetMaxHpDamageRatio` | `0.1` | Original frozen target takes 10% max HP damage instead of the fixed radius explosion damage. |
| `freezePrimaryTargetMaxHpDamageRatioPerLevel` | `0.00202` | Optional level growth for max-HP ratio damage only. Set `0` when Frost status should not scale by level. |
| `maxFreezePrimaryTargetMaxHpDamageRatio` | `1.0` | Upper cap for level-scaled max-HP ratio damage. |
| `freezeExplosionLayerMask` | damage layers | Should match zombie damageable layers for overlap damage. |
| `freezeExplosionSlowRatio` | `0.3` | Targets damaged by the freeze explosion lose 30% speed briefly. |
| `freezeExplosionSlowDuration` | `1.0` | Duration of the secondary explosion slow. |
| `freezeCooldownPerTarget` | `2.0` | Prevent repeated explosions from the same target every tick. |

Current `Frost_BeamAttackProfile` test values:

- `targetMode = PierceLine`
- `maxTargets = 4`
- `damageTickInterval = 0.2`
- `damageMultiplier = 1`
- `treatTurretDamageAsDps = true`

Current Frost status profile test values:

- `slowBuildUpDuration = 1.5`
- `maxSlowRatio = 0.9`
- `slowHoldDuration = 1.0`
- `freezeTriggerRatio = 0.9`
- `freezeDuration = 3.8`
- `freezeEffectDuration = 5.0`
- `freezeExplosionDamageDelay = 2.8`
- `freezeExplosionRadius = 2.5`
- `freezeExplosionDamage = 1`
- `freezePrimaryTargetMaxHpDamageRatio = 0.8`
- `freezePrimaryTargetMaxHpDamageRatioPerLevel = 0.00202` if Lv1 80% should grow toward Lv100 100%.
- `maxFreezePrimaryTargetMaxHpDamageRatio = 1.0`
- `freezeExplosionSlowRatio = 0.3`
- `freezeExplosionSlowDuration = 1.0`
- `freezeCooldownPerTarget = 8.0`

FrostRay VFX notes:

- `FrostRay_TurretBeam` uses `PilotoStudio.BeamEmitter`.
- The actual line target is `BeamEmitter.beamTarget`; there are several objects named `holder`, so name-based lookup is not reliable.
- The hit effect is `BeamEmitter.beamTargetHitFX`, currently `Hit_Spikes`.
- `BeamFiringEvent` can prewarm beam instances with `prewarmBeamOnEnable` so the first beam attack, including Frost and Ignition, does not pay the beam prefab instantiate and reference discovery cost.
- `BeamFiringEvent` resolves `BeamEmitter.beamTarget` and `BeamEmitter.beamTargetHitFX` together during beam instance creation, then moves the cached transforms to the current target position every frame.
- `BeamFiringEvent` caches the current target collider when the target changes, avoiding repeated `GetComponentInChildren<Collider>` calls while a continuous Frost beam tracks the same target.
- The FrostRay beam visual now uses the `BeamEmitter.beamTarget` transform, currently named `holder_Main`, as the runtime endpoint.
- `BeamFiringEvent` moves `holder_Main` and `BeamEmitter.beamTargetHitFX` to the current target position every frame, so the Frost turret no longer needs root X scaling for beam length.
- The old root X scale and child inverse-scale correction path has been removed from `BeamFiringEvent`; beam length should be driven by the endpoint holder instead.
- To reduce only the hit effect size, scale the `Hit_Spikes` object or its children, not the root `FrostRay_TurretBeam` object. Scaling the root changes the whole beam length and width.
- Current duplicated `Hit_Spikes` transform scale is `0.2, 0.2, 0.2` for the Frost turret test prefab.
- If VFX density or particle emission looks wrong after length scaling, tune the duplicated `FrostRay_TurretBeam` prefab under the project folder, not the Private Assets original.

Ignition status handling:

- `IgnitionStatusProfileSO` stores Ignition base burn values (`damageMultiplier`, `maxHpDamageRatioPerTick`, `tickInterval`, `duration`, `maxStackCount`, `stackRefreshMode`, reaction burn values, `bossDamageMultiplier`, `interactionFlags`).
- `Ignition_Turret_Stat Growth Profile SO` uses `IgnitionTurretStatGrowthProfileSO` and owns Ignition-specific scaling for max-HP burn ratio, burn duration, reaction max-HP burn ratio, and reaction tick interval. Common `targetDamageAtMaxLevel`, `rangePerLevel`, and `fireIntervalReductionPerLevel` can stay flat when Ignition DPS is driven by max-HP burn instead of `damage / fireInterval`.
- `Ignition_Turret_Definition.ignitionStatusProfile` points to the active Ignition status profile. The runtime `IgnitionDamageApplier` receives the profile, level, and growth profile through `ITurretStatusProfileReceiver`; the prefab-local `IgnitionDamageApplier.ignitionStatusProfile` field can stay empty.
- `IgnitionConeDetector` owns the cone overlap check using non-alloc physics. `IgnitionDamageApplier` applies the resulting payload to targets implementing `ProjectZDefense.StatusEffects.IIgnitionStatusEffectReceiver`, but only while `BeamFiringEvent.HasActiveBeamVisual()` reports that the flame VFX is actually visible.
- `IgnitionConeDetector.range` is fixed at `18` for Ignition_Turret. `TurretStatProfileSO.range` is also set to `18` only for targeting/selection consistency; range growth does not drive the flame cone.
- `IgnitionBurningTargetCandidateFilter` is assigned to `Ignition_Turret` so enemies that are not currently burning are preferred. Burning enemies remain fallback targets when no non-burning target is available.
- `NormalZombie` and `BossZombie` implement `ProjectZDefense.StatusEffects.IIgnitionStatusEffectReceiver` and delegate duration, tick timer, and stack count to `IgnitionStatusRuntime`.
- `IgnitionStatusRuntime` reports burn active/inactive state to `StatusEffectVisualController`; zombies do not directly instantiate or configure mesh fire VFX.
- Ignition tick damage prefers `maxHpDamageRatioPerTick` when it is greater than `0`. If it is `0`, the runtime falls back to `damagePerSecond * tickInterval`.
- `Ignition_Turret.asset.damage` is set to `0` because the active balance path is max-HP percentage burn. Change `Ignition_Status_Profile_SO` and `Ignition_Turret_Stat Growth Profile SO` when tuning real Ignition DPS.
- The current first-test `Ignition_Status_Profile_SO` base values are `damageMultiplier = 0`, `maxHpDamageRatioPerTick = 0.01`, `tickInterval = 0.2`, `duration = 5`, `maxStackCount = 1`, and `stackRefreshMode = RefreshDurationOnly`.
- With those base values, a target that touches the flame receives a 5-second burn that deals 1% of max HP every 0.2 seconds before boss modifiers and Ignition-specific growth.
- `RefreshDurationOnly` keeps the burn predictable by refreshing the 5-second timer without increasing stack count. Use `AddStackAndRefreshDuration` only if the design explicitly wants repeated flame contact to increase burn damage.
- Ignition status resets on spawn, despawn, and death. Ignition burn-death VFX is currently not used.
- Add a `StatusEffectVisualSlot` with `visualType = IgnitionBurn` and `attachMode = RendererOverlay` to zombie prefabs that should show burn visuals.
- Assign `MeshFX_Fire 1` to the IgnitionBurn slot's `visualPrefab`.
- Assign the body `SkinnedMeshRenderer` entries that should emit fire particles into the IgnitionBurn slot's `targetRenderers`.
- Tune `particleScaleMultiplier` per zombie or boss prefab when the fire particles are too large or too small.
- Set `fadeInDuration = 1` and `fadeOutDuration = 1` on IgnitionBurn slots so `MeshFX_Fire 1` fades from 0% to 100% alpha when burn starts and from 100% to 0% alpha when burn ends.
- `StatusEffectVisualAlphaFader` is added to runtime visual instances and only updates while an alpha fade is active.
- Add an additional `IgnitionBurn` Anchor slot for the base cartoon flame, usually on a `FireBodyAnchor`, and assign `Fire_cartoon_fire 1`.
- Ignition can enter a fixed reaction state when the burning target is hit by third-generation Frost, Poison, or Electro status attacks.
- Ignition also checks pre-existing status when burn is first applied: active Poison, active Frost slow/freeze, or Electro with full Shock stacks/Overload stun can immediately convert the burn into the corresponding reaction.
- The first valid reaction is fixed until the current Ignition burn ends. Later Frost, Poison, or Electro hits do not change the reaction type.
- While reacted, Ignition burn uses the reaction-specific damage values from `IgnitionStatusProfileSO`: `reactionMaxHpDamageRatioPerTick`, `reactionTickInterval`, and `reactionDamageMultiplier` fallback. The incoming Frost, Poison, or Electro direct/status damage is not multiplied.
- The current first-test reaction values are `reactionMaxHpDamageRatioPerTick = 0.02`, `reactionTickInterval = 0.2`, and `reactionDamageMultiplier = 0`, so reacted burn deals twice the current base max-HP burn per tick at the same tick cadence.
- Reaction visuals use `StatusEffectVisualType.IgnitionReaction` slots and the slot's `ignitionReactionType` field:
  - `Electro` should use `Fire_cartoon_electric 1`.
  - `Frost` should use `Fire_cartoon_frost 1`.
  - `Poison` should use `Fire_cartoon_poison 1`.
- When an Ignition reaction visual is active, the regular `IgnitionBurn` visuals stay active so `MeshFX_Fire 1` remains on the target while the reacted flame style is layered on top.

## Current Balance Direction

- Current turret forms are balanced around tier level `100` evolution gates.
- `TurretStatProfileSO` stores tier level `1` base values used immediately after placement or evolution.
- `TurretStatGrowthProfileSO` grows damage toward `targetDamageAtMaxLevel` with a logarithmic curve controlled by `damageLogCurveStrength`.
- Range, fire interval, projectile speed, projectile count, and pierce count still use the existing per-level or interval growth fields.
- Damage growth end level is the nearest `EvolutionProgressionSO.requiredLevel`; if no evolution requirement exists, `TurretDefinitionSO.maxLevel` is used, then fallback level `100`.
- `TurretData.csv` exports damage growth through `TargetDamageAtMaxLevel` and `DamageLogCurveStrength`; `DamagePercentPerLevel` is no longer used by runtime damage growth.
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

Prefab replacement scale rules:

- Evolved prefabs are instantiated under the previous turret parent, then local position and rotation are restored from the previous turret.
- Definitions that still have another `evolutionProgressionProfile` keep the target prefab root local scale and inherit the build-point/base scale like normal placement.
- Final evolution definitions without an `evolutionProgressionProfile`, currently the third-generation turrets, divide the target prefab root local scale by the parent `lossyScale`. This prevents scaled turret bases from multiplying the final turret's authored prefab size again.

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

1. Scene-placed `TurretPlacementSlotUI` buttons reference a `TurretShopEntrySO` and `TurretPlacementController` directly.
2. `TurretPlacementSlotUI` starts placement by drag or click.
3. `TurretPlacementController` raycasts against the TurretBase layer and expects `PlacementHitArea`.
4. Hit collider must have `TurretBaseSlot` in parent hierarchy.
5. If the slot has `BuildPoint` and no turret, preview snaps to `BuildPoint` and shows valid state.
6. If occupied, preview still snaps to `BuildPoint` but shows invalid state.
7. If no turret base is hit, invalid preview projects onto fixed placement plane.
8. `TurretBaseSlot.TryPlace` spends `TurretShopEntrySO.PlacementCosts` through `ItemManager.TrySpend`.
9. Successful placement instantiates the turret prefab as a child of `BuildPoint`.
10. Installed turret local position and rotation should reset to zero/identity.
11. `TurretSelectionLayerUtility` normalizes the installed turret hierarchy to the TurretBase layer so camera touch raycasts can select it consistently.
12. `TurretBaseSlot` records the occupied turret controller or fallback GameObject.
13. `TurretPlacementController` records successful placement count per placement entry.
14. Placement entries can use `Placement Cost Tiers` to change the next placement cost by successful placement count.

Placement preview scale rules:

- Valid slot previews use `TurretPlacementController.previewScaleMultiplier`.
- Invalid world previews use `TurretPlacementController.invalidWorldPreviewScaleMultiplier`.
- Invalid world previews also multiply by the current or first available `BuildPoint.lossyScale`, matching the world-space size the turret would have after being installed as a child of a scaled base.
- Keep both multiplier values equal when drag, invalid placement, and snapped placement previews should preserve the same apparent turret prefab size after build-point scale is applied.
- Installed turrets are instantiated as children of `TurretBaseSlot.BuildPoint`, so any parent/build-point transform scale affects the final world-space size.
- `Project Z Defense/Placement/Turret Hologram` is the URP transparent shader prepared for valid turret placement previews. Create a material from it in the editor and assign it to `TurretPlacementController.validPreviewMaterial` when the valid preview should appear as a blue hologram.
- `validPreviewColor` and `invalidPreviewColor` are fallback colors only. If the matching preview material field is assigned, `TurretPlacementPreview` uses that material as authored and does not override its color through `MaterialPropertyBlock`.
- Runtime fallback preview materials are only created for missing material fields, so assigning `validPreviewMaterial` avoids creating the unused valid fallback material.

`TurretPlacementUI` still has a legacy rebuild helper, but `rebuildOnStart` should stay disabled for production scenes. The legacy `Project Z Defense/UI/Create Turret Placement UI` creator menu is disabled by default; place editable scene buttons manually and assign the desired `TurretShopEntrySO` assets.

Placement input note:

- As of 2026-06-30, Main scene placement input is supported by `EventSystemDebugger`'s legacy pointer bridge.
- The regression symptom was that `EventSystem.current.RaycastAll` detected `TurretPlacementSlot_Button` and `CameraControlCanvas/Panel`, but `TurretPlacementSlotUI` and `CameraTouchHandler` pointer/drag callbacks were not invoked.
- The bridge uses old `Input.GetMouseButton*`/touch polling, then forwards `PointerDown`, `BeginDrag`, `Drag`, `EndDrag`, `PointerUp`, and `Click` through `ExecuteEvents` to the top UI raycast result.
- Keep `EventSystemDebugger` active in Main while this bridge is required. If `InputSystemUIInputModule` is fixed later, first check for duplicate pointer events before removing or disabling the bridge.

## Turret Upgrade UI Setup

The active turret popup flow is manually maintained under `Canvas > Turret UI`.

- `TurretSelectionUIController` owns selected turret context, range display, and routing.
- `TurretSelectPopupUI` is the action hub for Upgrade, Detail, Skill, and close.
- `UpgradePopup` owns `TurretUpgradePopupUI` and handles level-up plus routing to `TurretEvolutionPopupUI`.
- `EvolutionPopup` owns `TurretEvolutionPopupUI` and handles candidate selection, cost display, and final evolution execution.
- `DescriptionPopup` owns `TurretDetailPopupUI` and handles read-only current turret stats.

The old Canvas-level `TurretUpgradePopup` prefab instance, `TurretTemporaryUpgradePopupUI`, `TurretEngineerSeatButton`, and `TurretUICreator` editor helper were removed on 2026-07-08. Do not reintroduce that temporary popup path unless a new migration plan explicitly requires it.

Turret selection uses direct pointer input only when `EventSystem.current.IsPointerOverGameObject()` is false. Outside-click dismissal is handled by the transparent background button's `OnClick`, so clicks inside the visible panel stay in UI space and do not run world selection.

Engineer buff policy:

- Assigning an engineer to a turret only reserves the target and starts movement.
- The damage buff is applied only after the engineer reaches the turret standby/build point.
- Mounted engineers are hidden through the survivor visibility/interaction controls instead of destroying the survivor instance.
- `TurretEngineerBuffReceiver.OnBuffStateChanged` is used by the turret popup to refresh active engineer seat triggers without making zombie spawn, drop, or survivor rescue systems respond to the mount/dismount state.
- `TurretEngineerBuffReceiver.DamageBonusRatioPerEngineer` is used only for UI display of the existing serialized buff value; it is not exported or imported by turret CSV tools.
- `TurretDefinitionSO.maxEngineerSeatCount` owns the maximum mounted engineer count for each turret definition. `0` means engineers cannot mount that turret.
- Initial engineer seat limits are 1st generation `1`, 2nd generation `2`, and 3rd generation `3`; future balance changes should adjust the SO or CSV value, not the runtime code.
- `TurretDataCsvEditorTool` exports and imports this value through the `MaxEngineerSeatCount` column.
- `TurretDataCsvEditorTool` exports and imports `TurretDamagePolishProfileSO.criticalChance` and `heavyHitChance` through the single `CriticalChancePercent` column. The CSV cell uses `criticalPercent;heavyPercent` such as `7;1`, while the ScriptableObject stores both values as `0~1`. Importing a legacy single value such as `7` updates only `criticalChance` and keeps the existing `heavyHitChance`.

## Targeting And Firing Notes

- `TargetFinder` selects the nearest valid target in range.
- `TargetFinder` resolves hit colliders to a stable tagged or `IDamageable` target root before returning a target, avoiding aim jitter from multi-collider enemies.
- `TargetFinder.IsCurrentTargetValid` is used by `Turret` before rotation and firing so a retained pooled target must still be active, alive, in range, and optionally visible before another shot is emitted.
- `TargetFinder` can ignore `ObstacleBuildSlot` helper colliders, placed `Obstacle` colliders, and an additional ignore layer mask during line-of-sight checks so defense-line barricades do not hide zombies from turret targeting.
- `TargetFinder.aimHeightRatio` controls where inside the target collider height the turret aims and runs line-of-sight checks. Keep the default `0.35` for existing turrets. Current `Poison_Turret` intentionally uses `0` because play-mode testing showed the projectile firing angle is more stable with that value.
- `IAimPointProvider` lets zombie roots return cached aim points, avoiding repeated child-collider searches from turret aim/prediction paths.
- `TargetFinder.targetCandidateFilterBehaviours` can connect project-owned `ITargetCandidateFilter` and `ITargetCandidateRetentionFilter` components. `PoisonLethalTargetCandidateFilter` is intended for `Poison_Turret` only and skips targets whose remaining Poison ticks already guarantee death. `FrostFreezeSuppressedTargetCandidateFilter` is intended for `Frost_Turret` only and retains actively Frost-slowed current targets until freeze while skipping and releasing freeze-capable targets that are already frozen or still inside their per-target freeze cooldown. `StickyCurrentTargetRetentionFilter` is currently connected to `Vector MG` so a valid current target is retained until it dies, leaves range, becomes inactive, or loses line of sight.
- `Turret` smooths target aim point and target velocity prediction, ignores vertical prediction by default, and uses `TurretLeadPredictionUtility` to aim at an estimated projectile/target intercept point.
- Prediction lead time can scale from slow-projectile long lead to fast-projectile short lead, improving low-speed projectile hit rate without making laser-speed shots over-lead visibly.
- `Turret` staggers its first target search within `targetSearchInterval` so many turrets do not all run physics target scans on the same frame.
- Turret fires on its configured fire interval while a valid target and projectile prefab exist. `requireAimAngleBeforeFire` can restore strict `turretAngleAttack` gating if a specific turret needs to wait for perfect alignment.
- Gun projectile rotation should follow visible muzzle forward direction.
- Homing/projectile mover components may still receive selected targets for hit tracking.

## Runtime Range Indicator

- See `TURRET_UI_REWORK_STATUS.md` for the current turret UI rework checklist, known weak points, and next button/reference verification order.
- New turret UI work should use `TurretSelectionUIController` for turret click selection, range display, and popup routing.
- `TurretSelectionUIController` can require a second click on the same turret within `secondClickInterval` before opening `TurretSelectPopup`, so the first click can be used as a range-only selection.
- `TurretSelectPopupUI` is a lightweight action hub only. It routes Upgrade to `TurretUpgradePopupUI`, Detail to `TurretDetailPopupUI`, and Skill to `TurretSkillPopupUI`.
- `TurretSelectPopupUI` displays `TurretDefinitionSO.shortDescription` in its Note text. This text may include TMP rich text tags such as `<nobr>` to prevent Korean word breaks.
- `TurretUpgradePopupUI`, `TurretDetailPopupUI`, `TurretSkillPopupUI`, and `TurretEvolutionPopupUI` are inspector-wired popup pages. Child popup `BackButton` controls should return to `TurretSelectPopup`.
- `TurretDetailPopupUI` reads `TurretDefinitionSO.damagePolishProfile` for critical and heavy-hit chance display. Missing profiles should display `0%` rather than blocking the detail popup.
- `TurretUpgradePopupUI` should route its `LowPanel/Evolution` button to `TurretEvolutionPopupUI` instead of performing evolution directly.
- Upgrade and evolution cost slots should follow `ResourceCost` data from turret ScriptableObjects, and resource name/image display should use `InventorySystem` metadata when available.
- `TurretEvolutionPopupUI` owns only evolution UI selection and execution. The source of truth for candidates remains `TurretEvolutionProgressionSO`, exposed through `TurretDefinitionRuntimeController.GetAvailableEvolutionCount`, `GetAvailableEvolution`, `CanEvolve`, and `TryCreateEvolvedInstance`.
- `TurretEvolutionPopupUI` chooses `MiddlePanel_A`, `MiddlePanel_B`, or `MiddlePanel_C` by available evolution count. Use A for one candidate, B for two candidates, and C for three or more candidates. Current authored data uses C for four-candidate branches.
- Clicking a `NextTurretImage` candidate should not evolve immediately. It selects the candidate, refreshes the candidate-specific `evolutionCosts`, and highlights that candidate's `NextTurretImageFrame`. The low-panel `Evolution` button performs the actual evolution for the selected candidate.
- Evolution material slots live under `MiddleLowPanel/RequireSorceImagePanel` and are capped at eight visible slots. `RequireSorceText` is a static title and should not be overwritten at runtime.
- Empty evolution material slots should keep their authored placeholder image, currently `crosshair`, while clearing `ItemName` and `ItemCount` text.
- `TurretRangeIndicator` renders one reusable world-space indicator for the currently selected turret.
- `TurretRangeIndicator` can use a configured prefab such as `Marker circle simple cyan 1` for polished range presentation, while keeping the previous `LineRenderer` circle as a fallback when no prefab is assigned.
- Prefab-based range indicators are instantiated once, reused, moved to the selected turret center, and scaled by `range / prefabRadiusAtScaleOne`.
- Particle-based range indicator prefabs can have their particle loops forced at runtime so short marker effects can stay visible while a turret remains selected.
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
- Frost freeze explosion effects should spawn through `PooledObjectUtility.SpawnEffect` and should use a real pool when repeated frequently.
- `ProjectileHitDetector` must clear target/collider state on reuse.
- Damage popup generation must go through `DamagePopupSpawner` so accumulation, throttling, stacked offsets, and DNP rendering stay centralized.
- Direct projectile hits should use `DamagePopupPolicyResolver.ResolveDirectHit`.
- Beam ticks, DoT, chain, AoE, and status burst damage should use accumulated popup policies so same-target damage stays readable and bounded.

## Third-Generation VFX Pooling Matrix

| Turret | VFX | Runtime owner | Pooling / prewarm policy |
| --- | --- | --- | --- |
| Frost_Turret | `FrostRay_TurretBeam` | `BeamFiringEvent` under the turret muzzle | Not registered in global `MemoryPool`. `prewarmBeamOnEnable` creates beam instances under each configured gun/muzzle before the first attack. |
| Frost_Turret | `Ice_Cubes_Explosion` | `FrostStatusRuntime` / `FrostStatusEffectUtility` | Global `MemoryPool` effect. Prefab root should keep `PoolObject`, `PooledEffectReturner`, and `FrostFreezeExplosionDamageTimer`; `MemoryPoolPrewarmer` should prewarm expected simultaneous frozen targets. |
| Frost_Turret | `Impact_Frost 1` | `FrostStatusRuntime` / `FrostStatusEffectUtility` | Global `MemoryPool` effect. Prefab root should keep `PoolObject` and `PooledEffectReturner`; prewarm enough for short freeze-death bursts. |
| Frost_Turret | `MeshFX_Frozen 1` | Enemy `StatusEffectVisualController` | Target-owned renderer overlay. Do not register in global `MemoryPool`; prewarm through `StatusEffectVisualController.prewarmVisualTypes` because each instance must bind to that enemy's renderers. |
| Poison_Turret | `Projectile 12 slime 1` from `VFX_Slime 1` | Projectile firing path | Global `MemoryPool` projectile. Projectile prefab should keep `PoolObject` and `PooledProjectileReturner`; prewarm by expected projectile concurrency. |
| Poison_Turret | `3_Poison_MushRoom 1` | `PoisonDeathBurstEffectUtility` | Global `MemoryPool` effect. Prefab root should keep `PoolObject` and `PooledEffectReturner`; use `PoisonDeathBurstProfileSO.effectDuration` / fade values for return timing. |
| Poison_Turret | `MiasmaCannister 1`, `19_Poison_Aura_Hazard_Mixed 1` | Enemy `StatusEffectVisualController` | Target-owned visual slots. Do not register in global `MemoryPool`; prewarm through enemy visual slots and keep the optional lethal indicator child cached by slot name. |
| Electro_Turret | Electro projectile prefab from `VFX_Electro 1` | Projectile firing path | Global `MemoryPool` projectile. Projectile prefab should keep `PoolObject` and `PooledProjectileReturner`; prewarm by expected projectile concurrency. |
| Electro_Turret | `PS_Electro_ChainLink` | `ElectroChainLinkEffectUtility` | Global `MemoryPool` effect. Prefab root should keep `PoolObject`, `PooledEffectReturner`, `ElectroChainLinkAnchorTracker`, and `ElectroChainCoreLineEffect`. |
| Electro_Turret | `PS_Lightining_Impac`, `Stun 1` | `ElectroStatusRuntime` Overload branch | Global `MemoryPool` effects. Prefab roots should keep `PoolObject` and `PooledEffectReturner`; prewarm for expected simultaneous Overload bursts. |
| Electro_Turret | `Volt Sphere 1` | `ElectroStatusRuntime` on the target | Target-owned Shock stack orbit visual. Do not register in global `MemoryPool`; assign an Electro visual prewarm profile on enemy prefabs so up to three inactive instances are created on enemy initialization. |
| Electro_Turret | `FX_Electricity_02 1` | Enemy `StatusEffectVisualController` | Target-owned stun visual slot. Do not register in global `MemoryPool`; prewarm through `StatusEffectVisualController.prewarmVisualTypes`. |
| Ignition_Turret | Ignition beam prefab from `VFX_Ignition_Turret` | `BeamFiringEvent` under the three flame nozzles | Not registered in global `MemoryPool`. `prewarmBeamOnEnable` creates one beam instance per configured gun/muzzle, currently three total, before the first flame attack. `IgnitionDamageApplier` gates cone burn damage through active flame VFX visibility. |
| Ignition_Turret | `MeshFX_Fire 1`, `Fire_cartoon_fire 1` | Enemy `StatusEffectVisualController` | Target-owned burn visuals. Do not register in global `MemoryPool`; prewarm through `StatusEffectVisualController.prewarmVisualTypes`. `MeshFX_Fire 1` stays active during reaction burn. |
| Ignition_Turret | `Fire_cartoon_electric 1`, `Fire_cartoon_frost 1`, `Fire_cartoon_poison 1` | Enemy `StatusEffectVisualController` | Target-owned reaction visuals. Do not register in global `MemoryPool`; prewarm through `StatusEffectVisualController.prewarmVisualTypes` and activate only the fixed reaction type. |
| Ignition_Turret | `Effect_02/03/04_deathvfx 1` | None | Deprecated unused assets. They were moved under the Ignition status-effect `Unused` folder and are not part of the active pooling policy. |

## Setup Checklist

Run `Project Z Defense/Validation/Validate Turret Economy` after wiring turret economy assets.

The validator checks:

- missing `upgradeCostProfile` references on turret definitions.
- empty `baseCostsPerLevel` arrays on upgrade cost profiles.
- empty `evolutionCosts` arrays on evolution entries with a target definition.
- missing `placementCosts` on turret placement entries.
- missing `frostStatusProfile` on the Frost turret definition.
- missing `poisonStatusProfile` on the Poison turret definition.
- negative cost amounts in upgrade, evolution, and placement cost arrays.
- upgrade cost ramp mismatches against the current form rules.
- evolution cost ramp mismatches against the current branch-entry rules.
- `maxLevel` values set together with `evolutionProgressionProfile`.
- evolution entries that target the same `TurretDefinitionSO` as their source definition.

Use `Tools/터렛 웨이브 밸런스 시뮬레이션` when reviewing balance changes. The report window runs an editor-only numeric simulation from current SO data: `InventorySystem.initialWalletCurrencies` initial Coin, cumulative average wave rewards (current wave's own reward excluded from that wave's available budget, since it hasn't been earned yet), per-wave boss spawn count, turret placement costs, greedy upgrade distribution, total turret DPS with the current 8-turret install cap, total wave HP, and estimated clear seconds. Wave spawn profile ranges are expanded into one report row per wave, because `spawnCount` is per wave, not a range total. The window checks source data signatures during auto refresh and recalculates the table only when relevant data changes. The top toolbar exposes a target clear time input used by graph comparisons and reserved for later wave-clear table tuning, plus an obstacle target-time multiplier used to compare obstacle destruction time against `target clear seconds * multiplier`. It also exposes shared DPS assumptions for Frost, Poison, Electro, and Ignition expected target counts, Electro Overload trigger expectation, plus a per-pierce DPS multiplier; these EditorPrefs-backed values are reused by the wave-clear table, turret scenario detail table, and graph data until the user changes them. The item balance tab lists turret upgrade/evolution cost currencies per wave and shows the wave-start cumulative maximum reference amount after expected drops, wave reward multipliers, craft inputs, and decompose expected values are applied. The turret and obstacle balance calculations use this item balance matrix as their shared budget source, including non-Coin placement, build, upgrade, and evolution costs. The graph tab overlays wave-based graph lines for total zombie HP, cumulative currencies, top-rank processable HP, all-turret per-species processable HP, and clear-time ratio against the target clear time. The graph groups vertical axes into HP, ratio, and currency groups; mouse wheel zooms the selected vertical group, Shift+wheel zooms the wave axis, and dragging pans the zoomed view. The obstacle balance tab uses total normal-zombie DPS excluding bosses (`AverageNormalZombieDps * NormalSpawnCount`) before comparing destruction time against the obstacle target-time multiplier. Zombie DPS is used only by the obstacle balance tab and comes from `ZombieWaveDpsMeasurementProfileSO`: exact wave data first, then normalized data from measured waves using attack damage and move/attack speed multipliers. If no DPS data exists, the obstacle balance tab leaves DPS at 0 and writes a missing-DPS note in its remarks column. Wave CSV export also writes measured/reference DPS columns: normal DPS from `NormalOnly`, and boss DPS by `BossZombieType`; missing waves are normalized from measured waves by attack damage and move/attack speed multipliers. Runtime measurement is a GameManager debug option and saves only wave number plus normal or boss-type DPS entries when a wave succeeds or fails. All-turret per-species processable HP lines are colored by turret evolution depth, gradually moving from green through blue to purple. Elemental final-tier DPS is estimated from each status profile: Poison and Ignition use max-HP tick damage scaled by their expected target counts, Electro includes chain-lightning falloff capped by its expected target count and optional Overload max-HP damage based on `fireInterval * maxShockStackCount * triggerExpectation`, and Frost adds freeze primary explosion damage plus expected extra explosion targets on its freeze cycle. Projectile pierce increases direct DPS by `1 + pierceCount * per-pierce DPS multiplier`. Processable HP is `total DPS * target clear seconds`, so turret lines can share the zombie HP axis. Clear-time ratio is `estimated clear seconds / target clear seconds`, with `1.0x` as the target line. CSV export writes `item_balance.csv`, `wave_clear_simulation.csv`, `turret_species_detail.csv`, `obstacle_balance.csv`, and `source_data_warnings.csv` with Korean headers.

The balance report filters turret cumulative currency displays to currencies directly used by turret upgrades or turret evolution, so wave comparison stays focused on progression costs. It still uses `Assets/__PROJECT__/Prefabs/InventorySystem/ItemData.csv` craft and decompose relations when calculating virtual available amounts for those displayed currencies. Obstacle balance keeps its own build/upgrade currency scope and may include craft/decompose related materials for optimal-level and optimal-combination calculations. Decompose ranges use expected value `(min + max) / 2`.

The turret balance report displays base DPS together with special-hit expected DPS as `base (critical/heavy)`. Special-hit expected DPS applies `TurretDamagePolishProfileSO.HeavyHitChance`, `HeavyHitMultiplier`, `CriticalChance`, and `CriticalMultiplier` to direct damage using the runtime order where heavy hit is checked first and critical is checked only when heavy hit does not trigger. Damage variance and max-HP status damage are not included in the special-hit expected value. Wave clear seconds and optimal turret ranking use special-hit expected DPS. In the graph tab, turret DPS lines draw only special-hit expected processable HP at full opacity, and total zombie HP can be toggled separately from the per-type HP stack.

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
