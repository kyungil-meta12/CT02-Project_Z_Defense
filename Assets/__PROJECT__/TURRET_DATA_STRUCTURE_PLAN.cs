/*
 * Turret Data Structure Plan
 *
 * Purpose
 * - Turret progression data is separated from runtime logic so balancing, VFX, evolution, and test UI work can be edited safely in ScriptableObjects.
 * - The current runtime test flow starts from Sentinel-01.
 * - Turrets use tier level for their own stat/VFX/projectile scale progression.
 * - Total level is tracked separately for display and future reward/economy systems.
 * - Evolution, final turret level caps, projectile scale progression, and runtime evolution UI are currently implemented.
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
 * - At tier level 200, it evolves into:
 *   Sentry Pulse -> Pulse Repeater
 *
 * 3. Vector MG
 * - First evolution branch from Sentinel-01.
 * - Tier level starts again from 1 after evolution.
 * - At tier level 200, it evolves into:
 *   Vector MG -> Vulcan Node
 *
 * 4. Pulse Repeater
 * - Final turret on the Sentry Pulse branch.
 * - Formerly named Mass Driver.
 * - Fast firing final turret.
 * - Max tier level: 300.
 * - No further evolution currently.
 * - Future plan: max-level final turrets may be sold for special currency.
 *
 * 5. Vulcan Node
 * - Final turret on the Vector MG branch.
 * - No further evolution currently.
 * - Max tier level: 300.
 * - Future plan: max-level final turrets may be sold for special currency.
 *
 * Runtime Level Model
 *
 * Tier Level
 * - Stored as TurretDefinitionRuntimeTester.level.
 * - Exposed as CurrentTierLevel.
 * - Used for all current turret balancing:
 *   stat growth, VFX progression, projectile scale progression, evolution requirements, and max level caps.
 * - Resets to 1 after evolution.
 *
 * Total Level
 * - Stored as TurretDefinitionRuntimeTester.totalLevel.
 * - Exposed as CurrentTotalLevel.
 * - Does not drive stat growth.
 * - Used for UI display and future progression/economy hooks.
 * - Example: Sentinel-01 tier level 100 evolves into Sentry Pulse tier level 1, while total level stays around 100.
 *
 * Max Tier Level
 * - Stored in TurretDefinitionSO.maxLevel.
 * - 0 means unlimited.
 * - A positive value clamps level-up at that tier level.
 * - Pulse Repeater currently uses maxLevel = 300.
 * - TurretEvolutionRuntimeUI shows "Max Level" when this cap is reached and no evolution is available.
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
 * - TurretDefinitionRuntimeTester combines baseStatProfile and statGrowthProfile into TurretRuntimeStat.
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
 * Evolution Runtime Flow
 *
 * 1. TurretDefinitionRuntimeTester checks the current turret's evolutionProgressionProfile using current tier level.
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
 * 1. TurretDefinitionRuntimeTester receives the current tier level.
 * 2. It clamps the requested tier level by evolution requirements and maxLevel.
 * 3. It calculates runtime stats using TurretStatCalculator.
 * 4. It applies combat stats through TurretStatProfileApplier.
 * 5. It selects projectile prefab/muzzle/sound through TurretVFXProgressionSO.
 * 6. It selects projectile scale through TurretProjectileScaleProgressionSO.
 * 7. Turret stores the selected projectile prefab, projectile speed, damage, pierce count, and scale.
 * 8. Gun/RocketFire applies damage, speed, scale, pooling reset, and collision ignore rules to each spawned projectile.
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
 * - Max tier level: 300.
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
 * - Max tier level: 300.
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
 * - maxLevel is 0 unless the turret is intended to stop leveling.
 *
 * For every evolution entry:
 * - requiredLevel uses tier level.
 * - targetDefinition is assigned.
 * - displayName matches the target display name unless intentionally overridden.
 * - evolutionIcon is assigned for runtime UI buttons.
 * - evolutionEffectPrefab is assigned if an effect should play.
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
 */

using UnityEngine;

public class TURRET_DATA_STRUCTURE_PLAN : MonoBehaviour
{
}
