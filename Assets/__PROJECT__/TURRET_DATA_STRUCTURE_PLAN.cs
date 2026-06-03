/*
 * Turret Data Structure Plan
 *
 * Purpose
 * - Turret progression data is separated from runtime logic so balancing and VFX work can be edited safely in ScriptableObjects.
 * - Current design starts every turret run from Sentinel-01.
 * - At specific levels, Sentinel-01 can evolve into one of the following turret branches:
 *   Mass Driver, Sentry Pulse, Vector MG, Vulcan Node.
 * - Evolution branch selection is not implemented yet. The current implemented step is level-based projectile scale progression.
 *
 * Current Implemented Structure
 *
 * 1. TurretDefinitionSO
 * - Top-level definition for a turret.
 * - Holds identity, base prefab, stat growth, VFX progression, and projectile scale progression.
 * - Sentinel-01 currently owns the initial definition.
 *
 * Current fields:
 * public string turretId;
 * public string displayName;
 * public GameObject basePrefab;
 * public TurretStatProfileSO baseStatProfile;
 * public TurretStatGrowthProfileSO statGrowthProfile;
 * public TurretVFXProgressionSO vfxProgressionProfile;
 * public TurretProjectileScaleProgressionSO projectileScaleProgressionProfile;
 *
 * 2. TurretStatProfileSO
 * - Holds base combat values.
 * - Damage, range, fire interval, projectile speed, projectile count, pierce count.
 * - Projectile speed belongs here, not in VFX data.
 *
 * 3. TurretStatGrowthProfileSO
 * - Calculates level-based runtime stat growth.
 * - TurretDefinitionRuntimeTester combines baseStatProfile and statGrowthProfile into TurretRuntimeStat.
 *
 * 4. TurretVFXProfileSO
 * - Holds visual/audio projectile data only.
 * - Projectile prefab, muzzle VFX, muzzle duration, fire sound.
 * - It should not be duplicated just to represent projectile size.
 * - It should not contain balance values such as damage, range, fire interval, projectile speed.
 *
 * 5. TurretVFXProgressionSO
 * - Selects which TurretVFXProfileSO is active for a level.
 * - This is for projectile type/VFX/sound changes.
 * - Example use:
 *   Level 1 uses OrangeArrow projectile.
 *   Later evolution branches may use Mass Driver/Sentry Pulse/Vector MG/Vulcan Node projectile profiles.
 *
 * 6. TurretProjectileScaleProgressionSO
 * - Selects projectile scale by level.
 * - This is separate from VFX profile selection.
 * - Current recommended Sentinel-01 setup:
 *   Level 1  -> scale 1
 *   Level 5  -> scale 2
 *   Level 10 -> scale 3
 *   Level 15 -> scale 4
 *   Level 20 -> scale 5
 * - Runtime applies this scale when pooled projectiles are spawned.
 * - Scale is multiplied by the projectile prefab's original localScale.
 *
 * Removed / Deprecated
 *
 * TurretPartsProgression
 * - No longer used by current design.
 * - TurretPartsProgressionSO, TurretPartsProgressionApplier, and related scene/prefab references were removed.
 * - Do not add new upgrade part toggling data unless the design explicitly brings visual parts progression back.
 *
 * Projectile Size VFXProfile Duplicates
 * - VFXProfile assets should not be duplicated as size 1~5 variants.
 * - Keep one baseline VFXProfile per projectile type.
 * - Use TurretProjectileScaleProgressionSO for size growth.
 * - This avoids asset explosion when evolution branches are added.
 *
 * Runtime Flow
 *
 * 1. TurretDefinitionRuntimeTester receives the current level.
 * 2. It calculates runtime stats using TurretStatCalculator.
 * 3. It applies combat stats through TurretStatProfileApplier.
 * 4. It selects projectile prefab/muzzle/sound through TurretVFXProgressionSO.
 * 5. It selects projectile scale through TurretProjectileScaleProgressionSO.
 * 6. Turret stores the selected projectile scale.
 * 7. Gun/RocketFire applies the scale to each spawned pooled projectile.
 *
 * Important Pooling Note
 * - Projectile scale must be applied every time a projectile is spawned from the pool.
 * - Do not rely on prefab scale or previous pooled object state.
 * - Reused pooled projectiles may retain their old transform state unless explicitly reset.
 *
 * Evolution Design Direction
 *
 * Planned high-level flow:
 * 1. Player starts with Sentinel-01.
 * 2. Sentinel-01 levels up normally.
 * 3. At specific evolution levels, player can choose one evolution branch:
 *    Mass Driver, Sentry Pulse, Vector MG, Vulcan Node.
 * 4. Evolution changes turret identity, visual prefab/model, projectile profile, and possibly stat growth.
 * 5. Projectile scale progression can remain shared or become branch-specific depending on balance needs.
 *
 * Recommended future data structure:
 *
 * TurretEvolutionProgressionSO
 * - Holds possible evolution entries.
 * - Each entry has required level, target TurretDefinitionSO, and optional unlock conditions.
 *
 * TurretEvolutionEntry
 * - requiredLevel
 * - targetDefinition
 * - displayName
 * - unlockCost or required item
 *
 * Suggested Responsibility Boundaries
 *
 * Combat / Core Logic
 * - Damage, range, attack rate, projectile count, projectile speed, pierce count.
 * - Targeting, health, wave interaction, economy, upgrade cost.
 *
 * Turret / Projectile Presentation
 * - Turret model selection.
 * - Projectile prefab and visual effects.
 * - Muzzle VFX and fire sound.
 * - Projectile scale progression.
 *
 * Private Assets Rule
 * - Avoid direct modification of Private Assets originals when possible.
 * - Prefer project-level wrappers, profiles, adapters, or duplicated prefabs under the project scene folder.
 * - If direct modification is unavoidable for runtime integration, keep the change small and document why.
 *
 * Current Editor Setup Checklist
 *
 * Sentinel-01 Definition:
 * - baseStatProfile assigned.
 * - statGrowthProfile assigned.
 * - vfxProgressionProfile assigned.
 * - projectileScaleProgressionProfile assigned.
 *
 * Sentinel-01 VFX Progression:
 * - Uses baseline VFXProfile assets only.
 * - Does not use size 2~5 duplicate VFXProfiles.
 *
 * Sentinel-01 Projectile Scale Progression:
 * - Contains level entries for scale 1~5.
 * - Assigned to TurretDefinitionSO.projectileScaleProgressionProfile.
 */

using UnityEngine;

public class TURRET_DATA_STRUCTURE_PLAN : MonoBehaviour
{
}
