# Gameplay Runtime Flow

## Purpose

This document summarizes the current runtime flow for waves, zombies, obstacles, defense lines, survivors, repair, and damage.

## Wave And Spawn Flow

1. `ZombieSpawner.Start` reads `ZombieSpawnData`.
2. `ZombieSpawner` sends the target kill count to `GameManager.InputDestKillCount`.
3. During runtime, `ZombieSpawner` spawns normal zombies from pooled prefabs by interval.
4. The last spawn in the wave can be a boss zombie.
5. Zombies notify kill progress through `GameManager.IncreaseKillCount` when their death flow completes.
6. `GameManager.Update` increases `Wave` when `KillCount == DestKillCount` and invokes `OnWaveIncrease`.
7. `ZombieSpawner` receives the next wave and recalculates spawn interval/count from `ZombieSpawnData`.

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
- World-space damage feedback is spawned through `DamagePopupSpawner.SpawnDamage` where visible feedback is required.

## Obstacle And Defense Line Flow

1. `Obstacle` registers with `GameManager` on enable/start and unregisters on disable.
2. `Obstacle.TakeDamage` clamps HP and hides HP UI on death.
3. When HP reaches zero, `Obstacle` marks `IsAlive = false`, clears repair reservation, and calls `Fracture`.
4. `Fracture` triggers DinoFracture and notifies `GameManager.NotifyObstacleFractured` once.
5. `GameManager` finds which defense-line entry contains that obstacle.
6. The defense line is marked breached and all registered survivors receive `StartDefenseLineRetreat`.

Important policy:

- Broken obstacles are not repair targets because `Obstacle.IsDamaged` requires `IsAlive`.
- A breached defense line is a state on `GameManager.DefenseLineEntry`; restoration must call `NotifyDefenseLineRestored`.

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