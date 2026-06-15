# Gameplay Runtime Flow

## Purpose

This document summarizes the current runtime flow for waves, zombies, obstacles, defense lines, survivors, repair, and damage.

## Wave And Spawn Flow

1. `GameManager.Awake` applies the configured `startWave` and starting game time scale.
2. `ZombieSpawner.Start` reads the assigned `ZombieWaveSpawnProfileSO`.
3. If no wave spawn profile is assigned, `ZombieSpawner` disables spawning for that wave and logs a warning.
4. `ZombieSpawner` sends the target kill count to `GameManager.InputDestKillCount`.
5. During runtime, `ZombieSpawner` spawns normal zombies from pooled prefabs by interval.
6. With `ZombieWaveSpawnProfileSO`, normal and boss prefab candidates can be weighted and restricted by wave range.
7. The last spawn in the wave can be a boss zombie when the active profile stage enables it.
8. Spawn profile runtime multipliers can adjust spawned zombie HP, attack damage, move/attack speed, and reward amount.
9. Zombies notify kill progress through `GameManager.IncreaseKillCount` when their death flow completes.
10. `GameManager.Update` increases `Wave` when `KillCount == DestKillCount` and invokes `OnWaveIncrease`.
11. `ZombieSpawner` receives the next wave and recalculates spawn settings from the active wave profile.

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
- Weighted normal and boss prefab entries.
- Final-spawn boss toggle.
- Runtime HP, attack damage, move/attack speed, and reward multipliers.

Current first-pass campaign balancing uses wave `1~500`, with the final `451~500` stage using `hpMultiplier = 280`. With current role specs this puts late normal elite zombies around `79,800~100,800` HP before future turret DPS rebalancing.

Runtime zombie HP is calculated from the spawned prefab's combat spec as `Spec.Hp * Random.Range(MinHp, MaxHp) * stage.hpMultiplier`. Stage HP multipliers are intentionally stepped by wave range rather than calculated per wave, so balance reviews should compare turret DPS against the active stage range and weighted zombie composition.

The current 500-wave profile gradually removes early weak/basic roles and narrows late waves toward `Attacker` and `Elite` normal zombies. This means late-wave difficulty is driven by both higher `hpMultiplier` values and a heavier spawn composition, not by HP multiplier alone.

## Damage Contract

All runtime damage receivers that participate in shared combat should follow `IDamageable`:

```csharp
float TotalHp { get; }
float CurrHp { get; }
bool IsAlive { get; }
void TakeDamage(float damage);
```

Current implementations include:

- `NormalZombie`
- `BossZombie`
- `Obstacle`
- turret test targets where needed

Damage rules:

- Dead targets must return `IsAlive == false` before they can be selected or damaged again.
- Damage paths should ignore null targets, dead targets, and duplicate projectile hits.
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
- Current required slot counts are 1st line `3`, 2nd line `3`, 3rd line `1` gate slot.

## Obstacle Placement Flow

1. Scene-placed `ObstaclePlacementSlotUI` buttons reference an `ObstacleBuildEntrySO` and `ObstaclePlacementController` directly.
2. `ObstaclePlacementSlotUI` starts placement by drag or click.
3. `ObstaclePlacementController` raycasts against `ObstacleBuildSlot` hit areas.
4. Preview snaps to the slot `BuildPoint`.
5. If the slot has stored destroyed-obstacle progress for the same `ObstacleDefinitionSO`, preview and placement use the inherited level's prefab.
6. Placement is valid only when the slot is empty, the entry type matches the slot type, and `ItemManager` can afford the entry's `ResourceCost[] buildCosts`.
7. `ObstacleBuildSlot.CanPlaceEntry` checks slot availability, type match, and build cost availability through `ItemManager.CanAfford`.
8. `ObstacleBuildSlot.TryPlace` deducts the build costs using `ItemManager.TrySpend` before instantiating the obstacle under `BuildPoint`.
9. If the obstacle prefab is invalid and placement fails after spending, the deducted costs are refunded.
10. The placed obstacle receives its `ObstacleDefinitionSO` and inherited or initial level through `ObstacleUpgradeRuntimeController`.
11. The placed obstacle is assigned to the slot and `GameManager.NotifyObstaclePlaced` is called.
12. If the line was breached and all required slots are occupied again, `GameManager.NotifyDefenseLineRestored` restores that defense line.

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
- `Vaulting`

Runtime behavior:

1. `Survivor` registers with `GameManager` on enable/start and unregisters on disable.
2. In `Idle`, survivor periodically asks `GameManager.TryGetRepairTarget` for a damaged obstacle.
3. In `MoveToTarget`, survivor moves toward the reserved obstacle using `NavMeshAgent` and throttled destination refresh.
4. In `Repairing`, survivor calls `Obstacle.Repair` until the obstacle is fully repaired or target becomes invalid.
5. In `Retreating` or `ReturningToDefensePoint`, survivor moves to the configured defense point and may vault over `Obstacle` objects.

Repair target policy:

- Only damaged, alive, unreserved obstacles can be reserved.
- Only obstacles currently occupying registered defense-line slots are considered.
- If a survivor has retreated behind a defense line, obstacles at or before that active defense-line index are blocked as repair targets.
- Repair target movement does not trigger vaulting. Vaulting is only for defense-line retreat/return movement.

## Defense Line Retreat And Return

Breach flow:

1. An obstacle fractures.
2. `GameManager.NotifyObstacleFractured` marks the matching line breached.
3. Survivors receive the line index and retreat point.
4. Survivors clear repair targets and move to the retreat point.
5. After arrival, survivors return to `Idle`, but their active defense-line index remains set.

Restore flow:

1. An external rebuild/restore system calls `GameManager.NotifyDefenseLineRestored(index)`.
2. `GameManager` marks the line not breached.
3. Survivors receive the restored point.
4. Survivors move back and clear their active defense-line index after arrival.

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
