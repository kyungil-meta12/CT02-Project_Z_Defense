# Gameplay Runtime Flow

## Purpose

This document summarizes the current runtime flow for waves, zombies, obstacles, defense lines, survivors, repair, and damage.

## Wave And Spawn Flow

1. `GameManager.Awake` applies the configured `startWave` and starting game time scale.
2. `ZombieSpawner.Start` reads the assigned `ZombieWaveSpawnProfileSO`.
3. If no wave spawn profile is assigned, `ZombieSpawner` disables spawning for that wave and logs a warning.
4. `ZombieSpawner` sends the target kill count to `GameManager.InputDestKillCount`.
5. During runtime, `ZombieSpawner` spawns normal zombies from pooled prefabs by interval.
6. With `ZombieWaveSpawnProfileSO`, normal prefab candidates can be weighted and restricted by wave range.
7. Boss zombie spawn waves are controlled by the separate boss schedule on `ZombieWaveSpawnProfileSO`; each boss type uses first-wave and wave-interval values exported through `ZombieBossSpawnSchedule.csv`.
8. When multiple boss schedules match the same wave, all matching bosses spawn one per early spawn tick as additional enemies, without reducing the normal zombie spawn count.
9. Spawn profile runtime multipliers can adjust spawned zombie HP, attack damage, move/attack speed, and reward amount.
10. Runtime-only fallback scaling applies after the final finite wave range in `ZombieWaveSpawnProfileSO`: the next wave immediately adds the configured HP and attack multiplier increments, then repeats every configured interval.
11. Zombies notify kill progress through `GameManager.IncreaseKillCount` when their death flow completes.
12. `GameManager.Update` fully restores or rebuilds all registered obstacle slots when `KillCount == DestKillCount`, increases `Wave`, and invokes `OnWaveIncrease`.
13. `ZombieSpawner` receives the next wave and recalculates spawn settings from the active wave profile.
14. Game-over restart finds the last boss wave before the failed wave, restarts from the following wave, and invokes `OnWaveDecrease` for UI systems that must refresh on rollback.

## Normal Zombie Role Specs

Normal zombie prefabs are grouped into shared role-based `NormalZombieSpec` assets so profession variants can have distinct baseline stats without requiring one spec per prefab.

| Role Spec | Prefabs | Intent |
| --- | --- | --- |
| `NormalZombieSpec_Weak` | `NZ_Tourist`, `NZ_Bellhop`, `NZ_ShopKeeper` | Early weak civilian zombies. |
| `NormalZombieSpec_Basic` | `NZ_BusinessMan`, `NZ_Hobo`, `NZ_AirportWorker`, `NZ_Farmer` | Baseline general zombies. |
| `NormalZombieSpec_Fast` | `NZ_Runner` | Lower HP, higher move/attack speed pressure. |
| `NormalZombieSpec_Tough` | `NZ_RoadWorker`, `NZ_Mechanic`, `NZ_Trucker` | Higher HP, slower heavy-worker zombies. |
| `NormalZombieSpec_Attacker` | `NZ_Pimp`, `NZ_Prizoner`, `NZ_AirportSecurity` | Higher attack threat zombies. |
| `NormalZombieSpec_Elite` | `NZ_FireFighter`, `NZ_Solider` | Late stronger profession zombies. |

`NormalZombieSpec` and `BossZombieSpec` own only baseline combat stats and per-instance random variance. `ZombieWaveSpawnProfileSO` owns wave-specific spawn composition, entry weight, HP multiplier, attack damage multiplier, move/attack speed multiplier, and reward multiplier for both normal and boss zombies.

## Zombie Spawn Data Ownership

`ZombieSpawnData` has been retired. Do not add new wave, spawn count, spawn interval, or zombie stat scaling data to `ZombieSpawner` or a separate legacy spawn asset.

Use `ZombieWaveSpawnProfileSO` for:

- Wave ranges and stage segmentation.
- Spawn count and spawn interval.
- Weighted normal prefab entries and legacy fallback boss entries.
- Boss first-wave and wave-interval schedules through the separate boss schedule CSV.
- Runtime HP, attack damage, move/attack speed, and reward multipliers.

Current first-pass campaign balancing uses wave `1~500`, with the final `451~500` stage using `hpMultiplier = 280`. With current role specs this puts late normal elite zombies around `79,800~100,800` HP before future turret DPS rebalancing.

Runtime zombie HP is calculated from the spawned prefab's combat spec as `Spec.Hp * Random.Range(MinHp, MaxHp) * stage.hpMultiplier`. Stage HP multipliers are intentionally stepped by wave range rather than calculated per wave, so balance reviews should compare turret DPS against the active stage range and weighted zombie composition.

If a wave falls between configured finite stage ranges, runtime spawning keeps using the existing previous-stage fallback without extra multiplier scaling. If a wave is beyond the final finite stage range, runtime spawning keeps the final stage's spawn interval, spawn count, and candidate composition, but adds the profile's post-final-wave HP and attack multiplier increments immediately and once per configured interval after that. This post-final fallback is intentionally runtime-only and is not exported to the wave CSV or reflected in the balance report.

The current 500-wave profile gradually removes early weak/basic roles and narrows late waves toward `Attacker` and `Elite` normal zombies. This means late-wave difficulty is driven by both higher `hpMultiplier` values and a heavier spawn composition, not by HP multiplier alone.

## Damage Contract

All runtime damage receivers that participate in shared combat should follow `IDamageable`:

```csharp
float TotalHp { get; }
float CurrHp { get; }
bool IsAlive { get; }
void TakeDamage(DamageInfo damageInfo);
```

Current implementations include:

- `NormalZombie`
- `BossZombie`
- `Obstacle`
- turret test targets where needed

Damage rules:

- Dead targets must return `IsAlive == false` before they can be selected or damaged again.
- Damage paths should ignore null targets, dead targets, and duplicate projectile hits.
- Boss Tank and Boomer skills use boss-position-centered AoE damage against the configured obstacle target layer, with `Physics.OverlapSphereNonAlloc` and duplicate `IDamageable` filtering per tick. Tank uses its attack-damage multiplier, while Boomer uses a configured max-HP damage ratio per tick.
- Boss Screamer skill buffs nearby normal zombie speed and instantly restores nearby living normal zombies by a configured max-HP ratio.
- Normal zombie death disables all cached child colliders, including hit and attack colliders, so dead pooled bodies do not block turret projectiles while waiting for death animation and pool return.
- Normal zombie death also stops Rigidbody simulation during the death state and restores the original Rigidbody settings on spawn so the body does not sink after colliders are disabled.
- World-space damage feedback is spawned through `DamagePopupSpawner.SpawnDamage` where visible feedback is required.

## Obstacle And Defense Line Flow

1. `ObstacleBuildSlot` registers with `GameManager` as a defense-line slot.
2. `Obstacle` registers with `GameManager` on enable/start and syncs with its parent `ObstacleBuildSlot` when one exists.
3. `Obstacle.TakeDamage` clamps HP and hides HP UI on death.
4. When HP reaches zero, `Obstacle` marks `IsAlive = false`, clears repair reservation, and calls `Fracture`.
5. `Fracture` triggers DinoFracture and notifies `GameManager.NotifyObstacleFractured` once.
6. `GameManager` finds the `ObstacleBuildSlot` occupied by that obstacle.
7. The slot is cleared, the defense line is marked breached, and all registered survivors receive `StartDefenseLineRetreat`.

Important policy:

- Broken obstacles are not repair targets because `Obstacle.IsDamaged` requires `IsAlive`.
- Fractured obstacles and fracture pieces are not treated as slot occupants.
- A breached defense line is a state on `GameManager.DefenseLineEntry`; restoration must call `NotifyDefenseLineRestored`.
- Defense lines are managed by slots, not a direct obstacle list.
- Current main-scene slot counts are 1st line `3`, 2nd line `3`, 3rd line `3`, and 4th line `1` gate slot.
- At startup, any registered defense line with empty slots is initialized as breached so survivors use the same retreat/repair-blocking policy as a runtime breach.

## Obstacle Placement Flow

1. Scene-placed `ObstaclePlacementSlotUI` buttons reference an `ObstacleBuildEntrySO` and `ObstaclePlacementController` directly.
2. `ObstaclePlacementSlotUI` starts placement only by drag; a simple button click does not enter placement mode.
3. `ObstaclePlacementController` raycasts against `ObstacleBuildSlot` hit areas.
4. During drag, preview follows the pointer on the configured world plane as a red invalid preview when no slot is under the pointer.
5. When the pointer enters a slot, preview snaps to the slot `BuildPoint` and shows its valid or invalid placement color.
6. If the slot has stored destroyed-obstacle progress for the same `ObstacleDefinitionSO` or stable build-entry `SaveId`, preview and placement use the inherited level's prefab and rebuild discount. A valid discounted rebuild uses a sky-blue preview.
7. While hovering a rebuild slot, the placement button cost shows the final discounted amount and discount rate. Leaving the slot or ending placement restores the normal placement price.
8. After saved obstacles are restored, placement buttons refresh their normal prices from the restored slot-progress counts without requiring a drag input.
9. Placement is valid only when the slot is empty, the entry type matches the slot type, and `ItemManager` can afford the entry's effective `ResourceCost[]`.
10. Placement is allowed only on the defense line directly in front of the contiguous restored defense-line block from the rear. For example, if only the rear gate line at index `3` is restored, obstacles can be placed only on line `2`; once line `2` is fully restored, placement advances to line `1`.
11. `ObstacleBuildSlot.CanPlaceEntry` checks slot availability, type match, defense-line placement order, and effective cost availability through `ItemManager.CanAfford`.
12. `ObstacleBuildSlot.TryPlace` deducts the same effective costs shown by the preview using `ItemManager.TrySpend` before instantiating the obstacle under `BuildPoint`.
13. If the obstacle prefab is invalid and placement fails after spending, the deducted costs are refunded.
14. The placed obstacle receives its `ObstacleDefinitionSO` and inherited or initial level through `ObstacleUpgradeRuntimeController`.
15. The placed obstacle is assigned to the slot and `GameManager.NotifyObstaclePlaced` is called.
16. If the line was breached and all registered slots on that defense line are occupied again, `GameManager.NotifyDefenseLineRestored` restores that defense line.
17. Placement is confirmed only by `OnEndDrag`; world clicks never confirm an active preview.

`ObstaclePlacementUI` remains available as an optional runtime rebuild helper, but manual scene buttons are the default setup.

Slot type policy:

- 1st and 2nd defense lines use `Obstacle` slots.
- 3rd defense line uses one `Gate` slot.
- Obstacle entries cannot be installed into gate slots, and gate entries cannot be installed into obstacle slots.

Cost policy:

- Each `ObstacleBuildEntrySO` defines `ResourceCost[] buildCosts` for obstacle or gate placement.
- `ItemManager.TrySpend` deducts placement costs only if all required currencies are available.
- If placement fails after cost deduction (invalid prefab), costs are refunded via `ItemManager.Refund`.
- Legacy `int cost`, `Cost`, and Coin-only spend/refund fallback paths are intentionally removed for obstacle placement.
- Obstacle placement now uses the same multi-currency cost contract as turret placement, turret upgrade, and turret evolution.
- Do not reintroduce `AddCoinCount`, `CanUseCoin`, or `TryUseCoin` for obstacle rebuilds; use `AddReward`, `CanAfford`, `TrySpend`, and `Refund`.

## Obstacle Upgrade And Rebuild Level Flow

Obstacle progression is slot-centered rather than instance-centered:

1. `ObstacleDefinitionSO` defines the obstacle identity, slot type, spec, max level, upgrade cost profile, and level-based prefab progression.
2. `ObstacleUpgradeCostProfileSO` calculates `ResourceCost[]` upgrade costs from the current level to the target level.
3. `ObstaclePrefabProgressionSO` selects the prefab and placement rotation for the current level.
4. `ObstacleUpgradeRuntimeController` stores the currently installed obstacle's definition and level.
5. `ObstacleBuildSlot` stores the latest known definition and level for that slot.
6. When a live obstacle upgrades, `ItemManager.TrySpend` consumes the upgrade costs before the level is applied.
7. If the target level uses a different prefab, `ObstacleBuildSlot` instantiates the target prefab under `BuildPoint`, applies the same definition and target level, preserves the previous HP ratio, updates slot occupancy, and destroys the old obstacle.
8. When an obstacle fractures, `ObstacleBuildSlot.ClearCurrentObstacle` stores the fractured obstacle's definition and level before clearing occupancy.
9. Rebuilding the same `ObstacleDefinitionSO` in that slot inherits the stored level and immediately uses the prefab for that level.
10. Rebuilding a different definition starts at level 1. Obstacle and gate definitions do not inherit progress from each other.

HP policy:

- `ObstacleSpec.Hp` is the base max HP.
- `ObstacleSpec.levelWeight` is the fixed max-HP increase per level.
- Runtime max HP is `Hp + max(1, level) * levelWeight`.
- Obstacle HP does not use percentage HP weight or random min/max HP ranges.

Upgrade policy:

- Destroyed or fractured obstacles cannot be upgraded; they must be rebuilt through placement.
- Obstacles reserved by a survivor for repair cannot be upgraded in the first-pass implementation.
- Upgrade HP changes preserve the current HP ratio instead of fully healing the obstacle.
- Rebuild placement cost remains `ObstacleBuildEntrySO.BuildCosts`; inherited level does not add an extra rebuild surcharge in the first pass.

Debug policy:

- `ObstacleBuildSlot.CanPlaceEntry` is called continuously during placement preview refresh, so it must stay quiet and avoid debug string formatting.
- Detailed failure reasons belong in `ObstacleBuildSlot.TryPlace`, because that method represents the player's actual placement confirmation.
- Current placement logs cover missing build entry, missing prefab, slot type mismatch, missing `BuildPoint`, occupied slot, missing `ItemManager`, insufficient currency, spend failure, invalid placed prefab with refund, and successful placement.
- Keep these logs actionable and Korean, but do not log from per-frame preview paths unless a future diagnostic mode adds throttling.

## Survivor Flow

Survivor states:

- `Idle`
- `MoveToTarget`
- `Repairing`
- `Retreating`
- `ReturningToDefensePoint`
- `RescueEntering`
- `TreatmentReady`
- `MovingToHospital`
- `InTreatment`
- `ReturningFromHospital`
- `RoleSelectionReady`
- `EngineerReady`
- `MovingToEngineerStandby`
- `EngineerAssigned`
- `Vaulting`

Runtime behavior:

1. `Survivor` registers with `GameManager` on enable/start and unregisters on disable.
2. Survivor role is stored as `SurvivorRole`: `survivor`, `constructionWorker`, or `engineer`.
3. In `Idle`, only `constructionWorker` survivors periodically ask `GameManager.TryGetRepairTarget` for a damaged obstacle.
4. In `MoveToTarget`, survivor moves toward the reserved obstacle using `NavMeshAgent` and throttled destination refresh, and may vault over detected `Obstacle` objects along the path.
5. In `Repairing`, survivor repairs the target by a `SurvivorSpec` percentage of the obstacle's maximum HP per second and calls `Obstacle.Repair` until the obstacle is fully repaired or target becomes invalid.
6. In `Retreating` or `ReturningToDefensePoint`, survivor moves to the configured defense point and may vault over `Obstacle` objects.
7. Rescue survivors can spawn at wave start from `SurvivorRescueSpawner`; `enableRescueSpawn` can disable this wave spawn feature, and `SurvivorRescueSpawnProfileSO` decides whether the current wave attempts a spawn and which chance to use.
8. Spawned rescue survivors move from zombie spawn points to the final rear point, wait for treatment, move to the hospital, hide for the treatment timer, return, and then wait for role selection.
   Rescue movement can complete either by reaching the exact target transform within the configured arrival distance or by reaching the end of a valid/partial NavMesh path, so off-NavMesh target placement near the rear or hospital point does not leave the survivor stuck in a moving interaction status.
9. Treated survivors can become `constructionWorker` or `engineer` through `SurvivorInteractionController`; role Mesh/Material values switch by `SurvivorRole` list index and `normal`/`wounded` visual condition when configured.
10. Engineers can be clicked first and then assigned by selecting a turret target button in the engineer buff target UI.
11. The selected engineer moves to the target turret's standby/build point before the turret receives the stackable damage buff through `TurretEngineerBuffReceiver`.
12. When the engineer reaches the turret, the survivor is hidden as a mounted engineer and the turret upgrade popup can show an engineer seat trigger for that turret.
13. Clicking an active engineer seat trigger dismounts that engineer, removes the turret buff, shows the survivor again, and sends the engineer back to the rear gathering point when one is assigned.
14. Long movement time logs a throttled warning but does not cancel the current movement target.
15. Each turret definition limits mounted engineers through `TurretDefinitionSO.maxEngineerSeatCount`; `0` means that turret cannot accept engineers.

Repair target policy:

- `GameManager.TryGetRepairTarget` rejects non-`constructionWorker` survivors.
- Only damaged, alive, unreserved obstacles can be reserved.
- Only obstacles currently occupying registered defense-line slots are considered.
- Breached defense lines are excluded from repair target search.
- If a survivor has retreated behind a defense line, obstacles at or before that active defense-line index are blocked as repair targets.
- Repair target movement and defense-line retreat/return movement can trigger vaulting when an `Obstacle` is detected in the current move direction.
- During construction worker repair movement, obstacles on the repair target's defense line are not vaulted to prevent repeated crossing around the restored point. Unassigned survivors and engineers are not restricted by this repair movement rule.

## Defense Line Retreat And Return

Breach flow:

1. An obstacle fractures.
2. `GameManager.NotifyObstacleFractured` marks the matching line breached.
3. `Gate` slot breaches are marked separately from normal obstacle breaches.
4. Linked turret bases and installed turrets stay active after the line is breached; only initial empty-line and rebuild-complete states control turret base activation.
5. Normal obstacle breaches are only followed by `constructionWorker` survivors.
6. `Gate` breaches first unregister every boarded engineer from its turret, restore its visual and `NavMeshAgent`, and preserve the remembered turret slot for wave restart.
7. After all engineers are prepared, `Gate` breaches force every survivor role to clear current work and move to the retreat point.
8. Survivors clear repair targets and move to the retreat point.
9. After arrival, survivors return to their role idle state, but their active defense-line index remains set for construction workers.
10. Before gate retreat overwrites a rescued survivor's runtime state, the survivor stores whether treatment or role selection was pending.
11. On wave restart, rescued survivors return to the gathering point and restore `TreatmentReady` or `RoleSelectionReady`; treatment in progress is normalized back to treatment pending and does not preserve its remaining timer.

Restore flow:

1. An external rebuild/restore system calls `GameManager.NotifyDefenseLineRestored(index)`.
2. `GameManager` marks the line not breached.
3. Survivors receive the restored point.
4. Survivors move back and clear their active defense-line index after arrival.

## Game Over Restart Flow

Gate breach flow:

1. `Obstacle` fracture notifies `GameManager.NotifyObstacleFractured`.
2. If the fractured slot type is `Gate`, `GameManager.StartGameOverSequence` starts once and pauses wave progression.
3. Registered `ZombieSpawner` instances pause spawning immediately.
4. `GameOverPanelUI` fades in from transparent to opaque.
5. After fade-in, registered spawners return their tracked active zombies to `MemoryPool`.
6. `GameManager` rebuilds registered defense-line slots from stored obstacle definition/level and restores surviving obstacles to full HP.
7. After rebuild attempts, each defense line re-evaluates whether all registered slots are occupied; only fully built lines are marked restored and have linked turret bases enabled.
8. Engineer survivors attempt to re-register their buff to the last stored turret slot.
9. `GameManager` restarts from the wave after the latest boss wave before the failure, or wave 1 when no earlier boss exists; it resets kill count, asks spawners to prepare that wave from the beginning, and invokes `OnWaveDecrease` for display updates.
10. `GameOverPanelUI` fades out from opaque to transparent.
11. Registered spawners resume spawning.

Runtime policy:

- Game-over reset must not award zombie kill rewards or increase kill count.
- `ZombieSpawner` despawns only zombies it spawned and tracks, avoiding full-scene searches during reset.
- Defense-line rebuild is cost-free and uses `ObstacleBuildSlot` stored progress.
- Defense-line turret bases are enabled only for fully rebuilt lines; empty or partially rebuilt lines remain breached and keep their linked turret bases disabled.
- Gate breach dismounts engineers before survivor retreat. The restart sequence normalizes survivor retreat state first, then reassigns engineers to their remembered turret slots when those turrets still exist.
- A rescued survivor whose Agent or gathering point cannot be recovered restores the pending treatment/role-selection interaction immediately at its current position instead of remaining in a generic idle state.

## Wave Save And Restore Flow

1. `GameManager` registers itself with `SaveManager` before `ZombieSpawner.Start()` initializes the active wave.
2. The save file stores only the current wave number under the `Wave` section.
3. A successful wave increase or a game-over checkpoint rollback marks the save data dirty.
4. On the next launch, `GameManager` restores the saved wave before spawners read it.
5. The restored wave starts from the beginning with zero kills and a newly calculated target spawn count.

Runtime policy:

- Currency earned before closing the app remains governed by the currency save section.
- Current-wave kill count, remaining spawn count, turrets, obstacles, survivors, and wave bonus accumulation are not restored.
- Missing save data uses the scene `startWave`; invalid saved wave values are clamped to wave 1.
- The highest wave reached for the first time is saved with the current wave so rollback farming cannot repeat first-entry rewards.

## Survivor Rescue Spawn Policy

- `SurvivorRescueSpawner` rolls its configured spawn chance only when a wave is reached for the first time.
- Replaying waves after a game-over checkpoint rollback does not attempt another survivor spawn.
- Loading a saved current wave does not repeat its survivor spawn attempt.
- `spawnOnStartWave` applies only when the starting wave has never been recorded as reached.

## Survivor Save And Restore Flow

- The wave save section records every registered survivor's role and the treatment or role-selection stage of unassigned survivors.
- Loading creates the saved survivor roster from the rescue survivor prefab in a grid around the final rear gathering point.
- Construction workers return to normal work idle, while engineers return to unassigned engineer idle even if they were mounted on a turret when saved.
- Untreated survivors return as wounded and treatment-ready; treated survivors without a role return role-selection-ready.
- Position, HP, movement target, repair reservation, defense-line state, and engineer turret assignment are not saved.

## Obstacle Save And Restore Flow

- The wave save section records each occupied-progress slot by defense-line index, slot index, stable build-entry ID, and upgrade level.
- Previously placed obstacles remain owned save progress even when fractured before saving.
- After scene slots register, saved obstacles are rebuilt without cost through the existing slot rebuild path and restored to full HP.
- Current HP, fracture state, repair reservation, and world position are not saved.
- Defense-line breach and linked turret-base availability are evaluated after saved obstacles are restored.

## Turret Save And Restore Flow

- The wave save section records each occupied turret base by defense-line index and turret-slot list index.
- Each turret record stores the final evolved `turretId`, current tier level, and cumulative total level; position, target, damage-meter data, and engineer seating are not saved.
- Turret shop entries use a stable `SaveId`, and their cumulative successful placement counts are saved so tiered placement costs cannot be reset by restarting the app.
- After obstacle restoration and before defense-line availability is evaluated, saved turret prefabs are instantiated directly on their build points without placement or evolution costs, effects, or sounds.
- Restored engineers remain unseated at the survivor rally point and must be assigned to a turret again.
- When turret evolution replaces its prefab, seated engineers transfer to the new receiver in registration order up to the evolved turret's seat capacity.
- Engineers exceeding the evolved capacity, or failing transfer, dismount and return to the survivor rally point; a missing rally point falls back to visible `EngineerReady` idle.

## Edge Cases To Check

- `GameManager.Inst` missing when prefabs enable.
- Duplicated survivor or obstacle registration.
- Destroyed or disabled repair target while reserved.
- Empty defense-line obstacle lists.
- Missing retreat/restored point transforms.
- NavMesh path invalid, pending too long, or agent not on NavMesh.
- Obstacle reaches full HP while survivor is moving or repairing.
- Multiple survivors attempting to reserve the same obstacle.
- Boss or normal zombie death state not updated before target selection.
- Game-over panel reference missing when the gate breaks.
- Multiple gate fracture notifications in the same frame.
- Spawner wait coroutine running when game over starts.
- Destroyed or missing stored obstacle definition during defense-line rebuild.
- Engineer assigned turret slot missing or empty during reassign.
- Mounted engineer hidden state not restored after dismount, turret disable, or game-over reset.
