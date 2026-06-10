/*
 * Turret Data Structure Plan
 *
 * Purpose
 * - Turret progression data is separated from runtime logic so balancing, VFX, evolution, and test UI work can be edited safely in ScriptableObjects.
 * - The current runtime test flow starts from Sentinel-01.
 * - Turrets use tier level for their own stat/VFX/projectile scale progression.
 * - Total level is tracked separately for display and future reward/economy systems.
 * - Evolution, projectile scale progression, and runtime evolution UI are currently implemented.
 * - Projectile damage delivery, zombie death-state filtering, and world-space damage popups are currently connected.
 * - Second generation turret Definition assets are now prepared and linked to their stat, growth, VFX progression, projectile scale, and base prefab assets.
 *
 * Current Evolution Tree
 *
 * 1. Sentinel-01
 * - Base turret.
 * - Tier level range: 1 to 100.
 * - At tier level 100, level-up is blocked until the player chooses an evolution.
 * - Evolution choices:
 *   Sentinel-01 -> Sentry Pulse
 *   Sentinel-01 -> Vector MG
 *
 * 2. Sentry Pulse
 * - First evolution branch from Sentinel-01.
 * - Tier level starts again from 1 after evolution.
 * - Current asset required level is 100.
 * - It evolves into:
 *   Sentry Pulse -> Pulse Repeater
 *
 * 3. Vector MG
 * - First evolution branch from Sentinel-01.
 * - Tier level starts again from 1 after evolution.
 * - Current asset required level is 100.
 * - It evolves into:
 *   Vector MG -> Vulcan Node
 *
 * 4. Pulse Repeater
 * - Former first-generation branch end on the Sentry Pulse branch.
 * - Formerly named Mass Driver.
 * - Fast firing final turret.
 * - Current Definition maxLevel is 0.
 * - A Pulse Repeater evolution progression asset exists for second-generation entry, but the Definition is not connected to it yet.
 * - Planned second-generation choices:
 *   Pulse Repeater -> Machinegun_Blue_1
 *   Pulse Repeater -> Machinegun_Red_1
 *   Pulse Repeater -> Laser_Blue_1
 *   Pulse Repeater -> Laser_Red_1
 *
 * 5. Vulcan Node
 * - Former first-generation branch end on the Vector MG branch.
 * - Current Definition maxLevel is 0.
 * - A Vulcan Node evolution progression asset exists for second-generation entry, but the Definition is not connected to it yet.
 * - Planned second-generation choices:
 *   Vulcan Node -> Lethal_Green_1
 *   Vulcan Node -> Lethal_Red_1
 *   Vulcan Node -> Plasma_Blue_1
 *   Vulcan Node -> Plasma_Yellow_1
 *
 * 6. Second Generation Turrets
 * - Every second-generation turret currently has three Definition assets:
 *   _1, _2, _3.
 * - Current prepared families:
 *   Machinegun_Blue
 *   Machinegun_Red
 *   Laser_Blue
 *   Laser_Red
 *   Lethal_Green
 *   Lethal_Red
 *   Plasma_Blue
 *   Plasma_Yellow
 * - Each second-generation Definition has basePrefab, baseStatProfile, statGrowthProfile, vfxProgressionProfile, and projectileScaleProgressionProfile assigned.
 * - Second-generation evolutionProgressionProfile references are not connected to Definitions yet.
 * - Laser and Machinegun internal evolution assets exist under TurretEvolutionProgressionSO/2ndGen.
 * - Lethal and Plasma internal evolution assets currently exist under TurretEvolutionProgressionSO root and need to be moved or treated consistently.
 * - Lethal and Plasma internal evolution assets currently need targetDefinition/displayName cleanup before being connected.
 *
 * 7. Third Generation Turrets
 * - Third generation is planned but not implemented.
 * - Current visual plan candidates:
 *   Poison Turret
 *   CuteStarTurret
 *   ElectroTurret
 *   FlameThrower/LameThrower name still needs confirmation.
 *
 * Runtime Level Model
 *
 * Tier Level
 * - Stored as TurretDefinitionRuntimeController.level.
 * - Exposed as CurrentTierLevel.
 * - Used for all current turret balancing:
 *   stat growth, VFX progression, projectile scale progression, evolution requirements, and max level caps.
 * - Resets to 1 after evolution.
 *
 * Total Level
 * - Stored as TurretDefinitionRuntimeController.totalLevel.
 * - Exposed as CurrentTotalLevel.
 * - Does not drive stat growth.
 * - Used for UI display and future progression/economy hooks.
 * - Example: Sentinel-01 tier level 100 evolves into Sentry Pulse tier level 1, while total level stays around 100.
 *
 * Max Tier Level
 * - Stored in TurretDefinitionSO.maxLevel.
 * - 0 means unlimited.
 * - A positive value clamps level-up at that tier level and should be used only for a true final turret that has no further evolution.
 * - Evolution stop points should be controlled through TurretEvolutionProgressionSO.requiredLevel, not TurretDefinitionSO.maxLevel.
 * - If a turret has an evolutionProgressionProfile, the next required evolution level acts as a temporary level cap until the player chooses an evolution.
 * - Example: Sentinel-01 has maxLevel 0, but stops at level 100 because Sentinel-01_Evolution Progression SO has requiredLevel 100.
 * - Pulse Repeater and Vulcan Node currently use maxLevel 0 so they can later connect to second-generation evolution choices.
 *
 * Current Implemented Structure
 *
 * 1. TurretDefinitionSO
 * - Top-level definition for a turret.
 * - Holds identity, base prefab, stat growth, VFX progression, projectile scale progression, evolution progression, and optional max level.
 *
 * Current fields:
 * public string turretId;
 * public string displayName;
 * public GameObject basePrefab;
 * public TurretStatProfileSO baseStatProfile;
 * public int maxLevel;
 * public TurretStatGrowthProfileSO statGrowthProfile;
 * public TurretVFXProgressionSO vfxProgressionProfile;
 * public TurretProjectileScaleProgressionSO projectileScaleProgressionProfile;
 * public TurretEvolutionProgressionSO evolutionProgressionProfile;
 *
 * Recommended turretId format:
 * - Use stable lower_snake_case ids, for example:
 *   sentinel_01
 *   sentry_pulse
 *   vector_mg
 *   pulse_repeater
 *   vulcan_node
 *   machinegun_blue_1
 *   plasma_yellow_3
 * - Do not use display names as ids.
 *
 * 2. TurretStatProfileSO
 * - Holds base combat values for tier level 1 of that turret.
 * - Damage, range, fire interval, projectile speed, projectile count, pierce count.
 * - Projectile speed belongs here, not in VFX data.
 * - Each evolved turret should have its own base stat profile.
 *
 * 3. TurretStatGrowthProfileSO
 * - Calculates tier-level-based runtime stat growth.
 * - TurretDefinitionRuntimeController combines baseStatProfile and statGrowthProfile into TurretRuntimeStat.
 * - Growth uses completed levels: level 1 means 0 completed growth steps.
 * - Example: level 300 means 299 completed growth steps.
 *
 * 4. TurretVFXProfileSO
 * - Holds visual/audio projectile data only.
 * - Projectile prefab, muzzle VFX, muzzle duration, fire sound.
 * - It should not contain balance values such as damage, range, fire interval, or projectile speed.
 * - It should not be duplicated just to represent projectile size.
 *
 * 5. TurretVFXProgressionSO
 * - Selects which TurretVFXProfileSO is active for the current tier level.
 * - This is for projectile type/VFX/sound changes.
 * - Every turret Definition should reference its own progression asset unless intentionally sharing VFX progression.
 * - Level entries are tier-level based, not total-level based.
 *
 * 6. TurretProjectileScaleProgressionSO
 * - Selects projectile scale by current tier level.
 * - This is separate from VFX profile selection.
 * - Runtime applies this scale when pooled projectiles are spawned.
 * - Scale is multiplied by the projectile prefab's original localScale.
 * - Every turret Definition can reference its own projectile scale progression asset.
 * - Level entries are tier-level based, not total-level based.
 *
 * 7. TurretEvolutionProgressionSO
 * - Holds available evolution entries for a turret.
 * - If the current tier level reaches an available evolution level, normal level-up is blocked until evolution is chosen.
 * - If no evolution is available and TurretDefinitionSO.maxLevel is 0, level-up can continue.
 * - If no evolution is available and maxLevel is reached, level-up is blocked.
 * - The displayName field should usually match the target Definition displayName because it is used as the evolution button label.
 * - Leave displayName different only when intentionally showing a branch name or special route name.
 *
 * TurretEvolutionEntry fields:
 * public int requiredLevel;
 * public TurretDefinitionSO targetDefinition;
 * public string displayName;
 * public Sprite evolutionIcon;
 * public GameObject evolutionEffectPrefab;
 * public Vector3 evolutionEffectLocalOffset;
 * public float evolutionEffectDuration;
 *
 * 8. DamagePopupSettings
 * - Holds runtime configuration for world-space damage numbers.
 * - Loaded through Resources.Load("UI/DamagePopupSettings") by DamagePopupSpawner.
 * - References the DamagePopup prefab and controls pool size, font size, optional TMP font asset, color, lifetime, height offset, movement offset, start scale, and end scale.
 * - The current asset lives under:
 *   Assets/__PROJECT__/Resources/UI/DamagePopupSettings.asset
 * - The DamagePopup prefab lives in the same Resources folder so it can be loaded without a scene reference.
 *
 * 9. TurretShopEntrySO
 * - Holds data for turret placement UI entries.
 * - References a TurretDefinitionSO and optionally overrides the spawned prefab or preview prefab.
 * - The runtime placement UI reads display name, icon, cost, turret definition, turret prefab, and preview prefab from this asset.
 * - Turret prefab defaults to TurretDefinitionSO.basePrefab when overridePrefab is not assigned.
 * - Preview prefab defaults to the turret prefab when previewPrefab is not assigned.
 *
 * Current fields:
 * private string displayName;
 * private Sprite icon;
 * private TurretDefinitionSO turretDefinition;
 * private GameObject overridePrefab;
 * private GameObject previewPrefab;
 * private int cost;
 *
 * Evolution Runtime Flow
 *
 * 1. TurretDefinitionRuntimeController checks the current turret's evolutionProgressionProfile using current tier level.
 * 2. TurretEvolutionRuntimeUI displays available evolution buttons.
 * 3. Each button uses the evolution entry icon first, then its fallback sprite.
 * 4. When an evolution is selected:
 *    - evolution effect is spawned through PooledObjectUtility.
 *    - if targetDefinition.basePrefab exists, the old turret prefab is replaced.
 *    - target turret receives the targetDefinition.
 *    - tier level resets to 1.
 *    - total level is preserved.
 * 5. Runtime UI is reattached to the evolved turret tester.
 *
 * Stat/VFX Runtime Flow
 *
 * 1. TurretDefinitionRuntimeController receives the current tier level.
 * 2. It clamps the requested tier level by evolution requirements and maxLevel.
 * 3. It calculates runtime stats using TurretStatCalculator.
 * 4. It applies combat stats through TurretStatProfileApplier.
 * 5. It selects projectile prefab/muzzle/sound through TurretVFXProgressionSO.
 * 6. It selects projectile scale through TurretProjectileScaleProgressionSO.
 * 7. Turret stores the selected projectile prefab, projectile speed, damage, pierce count, and scale.
 * 8. Gun/RocketFire applies damage, speed, scale, pooling reset, and collision ignore rules to each spawned projectile.
 * 9. ProjectileDamageDealer refreshes damage, pierce count, logging state, legacy DamageManager/Projectile damage values, and hit detector state on spawn.
 * 10. ProjectileHitDetector applies damage to tracked targets, trigger/collision hits, or movement raycast hits.
 * 11. DamagePopupSpawner displays damage values when IDamageable implementations report damage.
 *
 * Second Generation VFX Mapping
 *
 * - Laser_Blue_1~3 -> VFX_BlueLaser
 * - Laser_Red_1~3 -> VFX_RedLaser
 * - Machinegun_Blue_1~3 -> VFX_Blue Fire
 * - Machinegun_Red_1~3 -> VFX_Black Fire
 * - Lethal_Red_1~3 -> VFX_Orange Explosion
 * - Lethal_Green_1~3 -> VFX_Green Explosion
 * - Plasma_Blue_1~3 -> VFX_Nova Violet
 * - Plasma_Yellow_1~3 -> VFX_Nova Orange
 *
 * Turret Placement Runtime Flow
 *
 * 1. TurretPlacementUI builds bottom-bar turret slots from TurretShopEntrySO entries.
 * 2. TurretPlacementSlotUI starts placement through drag or click input.
 * 3. TurretPlacementController Raycasts against the TurretBase layer and expects to hit PlacementHitArea.
 * 4. The hit collider must have a TurretBaseSlot in its parent hierarchy.
 * 5. If the slot has a BuildPoint and no current turret, the preview snaps to BuildPoint and uses the valid visual state.
 * 6. If the slot is occupied, the preview still snaps to BuildPoint but uses the invalid visual state.
 * 7. If no TurretBase is hit, the invalid preview is projected onto a fixed placement plane by default.
 * 8. On successful drop/click, the turret prefab is instantiated as a child of BuildPoint.
 * 9. The installed turret localPosition is Vector3.zero and localRotation is Quaternion.identity.
 * 10. The installed turret keeps its prefab localScale; BuildPoint and Turret Base parent transforms provide placement offset and world scale.
 * 11. TurretBaseSlot records the installed TurretDefinitionRuntimeController or fallback GameObject as the occupied turret.
 *
 * Targeting And Firing Notes
 *
 * - TargetFinder selects the nearest valid target in range.
 * - Turret now checks that the head is within turretAngleAttack before firing.
 * - Gun defaults to using the visible muzzle forward direction for projectile rotation.
 * - This keeps the visible muzzle direction and projectile launch direction aligned.
 * - Homing/projectile mover components can still receive the selected target for hit tracking.
 *
 * Pooling Notes
 *
 * - Projectile scale must be applied every time a projectile is spawned from the pool.
 * - Projectile speed, damage, pierce count, and collision ignores must also be refreshed on spawn.
 * - Do not rely on prefab state or previous pooled object state.
 * - Evolution effects should be spawned through PooledObjectUtility.SpawnEffect.
 * - DamagePopup instances are pooled through MemoryPool and prewarmed using DamagePopupSettings.InitialPoolSize.
 * - DamagePopup.Init must receive DamagePopupSettings every spawn because pooled text objects retain previous state.
 * - ProjectileHitDetector clears target/collider state through Init whenever a projectile is reused.
 *
 * Damage And Targeting Integration
 *
 * IDamageable contract
 * - Current runtime damage receivers expose:
 *   float TotalHp { get; }
 *   float CurrHp { get; }
 *   bool IsAlive { get; }
 *   void TakeDamage(float damage);
 * - Damageable state is read-only to external systems. Implementations update HP and alive state internally.
 * - ProjectileDamageDealer skips null targets, dead targets, and already-hit IDamageable instances.
 * - NormalZombie and BossZombie currently implement this contract.
 *
 * ProjectileDamageDealer
 * - Owns damage and pierce count for each projectile instance.
 * - Applies damage through hitCollider.GetComponentInParent<IDamageable>().
 * - Keeps a per-projectile hit list so one projectile does not hit the same damageable repeatedly.
 * - Updates legacy projectile damage components so existing asset logic remains compatible.
 *
 * ProjectileHitDetector
 * - Adds a robust hit path around projectile motion.
 * - Uses tracked target bounds first when a target is supplied.
 * - Falls back to trigger/collision callbacks and movement RaycastNonAlloc.
 * - Returns or disables the projectile when pierce limits are reached.
 *
 * Zombie death-state handling
 * - Dead zombies should report IsAlive == false before they can be selected again.
 * - Targeting and projectile damage paths should both respect IsAlive.
 * - Colliders left after death must not keep receiving turret damage or remain valid target candidates.
 *
 * Damage Popup Runtime Flow
 *
 * 1. IDamageable implementation receives TakeDamage.
 * 2. The implementation updates CurrHp and IsAlive.
 * 3. DamagePopupSpawner.SpawnDamage(targetTransform, damage) is called for visible feedback.
 * 4. DamagePopupSpawner lazily creates itself if no scene instance exists.
 * 5. It loads UI/DamagePopupSettings and UI/DamagePopup from Resources.
 * 6. It prewarms MemoryPool with the configured InitialPoolSize.
 * 7. DamagePopup.Init applies text, DamagePopupSettings, and camera.
 * 8. DamagePopup fades and moves upward, then returns itself to the pool through PoolObject.Despawn.
 *
 * Current Balance Data
 *
 * Sentinel-01
 * - Tier level 1:
 *   Damage 10, Range 50, Fire Interval 1, Projectile Speed 20.
 * - Tier level 100:
 *   Damage 25, Range 80, Fire Interval 0.5, Projectile Speed 35.
 * - Evolution required level: 100.
 *
 * Sentry Pulse
 * - Tier level 1:
 *   Damage 20, Range 60, Fire Interval 0.8, Projectile Speed 25.
 * - Tier level 200:
 *   Damage 30, Range 90, Fire Interval 0.3, Projectile Speed 40.
 * - Evolution required level: 200.
 *
 * Pulse Repeater
 * - Tier level 1:
 *   Damage 30, Range 80, Fire Interval 0.5, Projectile Speed 40.
 * - Tier level 300:
 *   Damage 50, Range 100, Fire Interval 0.1, Projectile Speed 50.
 * - Current maxLevel: 0.
 * - Second-generation entry Evolution SO exists but is not connected to the Definition yet.
 *
 * Vector MG
 * - Tier level 1:
 *   Damage 100, Range 70, Fire Interval 2, Projectile Speed 30.
 * - Tier level 200:
 *   Damage 140, Range 90, Fire Interval 1.5, Projectile Speed 40.
 * - Evolution required level: 200.
 *
 * Vulcan Node
 * - Tier level 1:
 *   Damage 100, Range 100, Fire Interval 1.2, Projectile Speed 35.
 * - Tier level 300:
 *   Damage 150, Range 130, Fire Interval 0.8, Projectile Speed 50.
 * - Current maxLevel: 0.
 * - Second-generation entry Evolution SO exists but is not connected to the Definition yet.
 *
 * Second Generation Data Status
 *
 * - 24 second-generation Definition assets exist.
 * - Definition base prefab/stat/growth/VFX/scale references are assigned.
 * - Current stat and growth values are placeholder-like and need balancing later.
 * - Evolution SO assets for second-generation internal progression exist, but not all are correct or connected.
 * - Known cleanup required:
 *   Lethal_Green1/2, Lethal_Red1/2, Plasma_Blue1/2, and Plasma_Yellow1/2 Evolution SO assets currently point to Machinegun_Red_3 and must be corrected before connection.
 *   Second-generation Evolution SO naming should be normalized to include underscores, for example Machinegun_Red_1_Evolution Progression SO.
 *   displayName values should be normalized to target Definition displayName format, for example Machinegun_Red_2.
 *
 * Branch Setup Checklist
 *
 * For every turret Definition:
 * - turretId uses a stable id.
 * - displayName is user-facing.
 * - basePrefab is assigned.
 * - baseStatProfile is assigned.
 * - statGrowthProfile is assigned if the turret grows.
 * - vfxProgressionProfile is assigned.
 * - projectileScaleProgressionProfile is assigned.
 * - evolutionProgressionProfile is assigned only if the turret can evolve.
 * - maxLevel is 0 unless the turret is intended to stop leveling forever.
 * - If the turret should stop only to wait for an evolution choice, use EvolutionProgressionSO.requiredLevel instead of maxLevel.
 *
 * For every evolution entry:
 * - requiredLevel uses tier level.
 * - targetDefinition is assigned.
 * - displayName matches the target display name unless intentionally overridden.
 * - evolutionIcon is assigned for runtime UI buttons.
 * - evolutionEffectPrefab is assigned if an effect should play.
 * - Do not point targetDefinition back to the same Definition unless intentionally testing a reset loop.
 * - After creating an EvolutionProgressionSO, remember to assign it to the source turret Definition.
 *
 * Turret Placement Setup Checklist
 *
 * For every Turret Base prefab:
 * - The root has TurretBaseSlot.
 * - BuildPoint exists and represents the actual turret mounting position and rotation.
 * - PlacementHitArea exists and has a Collider.
 * - PlacementHitArea is on the TurretBase layer.
 * - PlacementHitArea can be a trigger; TurretPlacementController uses QueryTriggerInteraction.Collide for base detection.
 * - BuildPoint should not contain a default turret in production placement prefabs.
 *
 * For every placement UI entry:
 * - TurretShopEntrySO references the intended TurretDefinitionSO.
 * - The referenced definition has basePrefab assigned.
 * - icon is assigned before final UI polish.
 * - cost is assigned when the economy system is connected.
 * - previewPrefab is assigned only when the placement preview should differ from the actual turret prefab.
 *
 * For TurretPlacementController:
 * - targetCamera is assigned, or Camera.main must exist.
 * - turretBaseLayerMask includes only the TurretBase layer unless another explicit placement layer is added.
 * - invalid preview placement plane is preferred for general map invalid feedback when floor height is stable.
 * - validPreviewMaterial and invalidPreviewMaterial can be left empty for runtime-generated preview materials, but final polish should use explicit materials.
 *
 * Removed / Deprecated
 *
 * TurretPartsProgression
 * - No longer used by current design.
 * - TurretPartsProgressionSO, TurretPartsProgressionApplier, and related scene/prefab references were removed.
 * - Do not add new upgrade part toggling data unless the design explicitly brings visual parts progression back.
 *
 * Projectile Size VFXProfile Duplicates
 * - VFXProfile assets should not be duplicated as size 1 to 5 variants.
 * - Keep one baseline VFXProfile per projectile type.
 * - Use TurretProjectileScaleProgressionSO for size growth.
 * - This avoids asset explosion when evolution branches are added.
 *
 * Private Assets Rule
 * - Avoid direct modification of Private Assets originals when possible.
 * - Prefer project-level wrappers, profiles, adapters, or duplicated prefabs under the project scene folder.
 * - If direct modification is unavoidable for runtime integration, keep the change small and document why.
 * - Current direct Private Assets changes:
 *   Turret.cs: fire only when aim angle is within turretAngleAttack.
 *   Gun.cs: default projectile rotation follows muzzleObject forward direction.
 * - Recent damage popup and projectile damage integration work is project-level and does not require new Private Assets repository changes.
 */

using UnityEngine;

public class TURRET_DATA_STRUCTURE_PLAN : MonoBehaviour
{
}
