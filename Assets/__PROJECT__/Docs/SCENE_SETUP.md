# Scene Setup

## Purpose

This checklist prevents broken scene, prefab, layer, NavMesh, and Resources references when editing `Main.unity` or project prefabs.

## Main Scene

Current integration scene:

- `Assets/__PROJECT__/Scenes/Main.unity`

Before editing `Main.unity`:

- Check `git status` and preserve unrelated scene/prefab/asset changes.
- Prefer inspector edits in Unity when possible for serialized references.
- If editing YAML manually, preserve `guid`, `fileID`, component order, prefab instance structure, and object references.

## Required Runtime Managers

| System | Requirement |
| --- | --- |
| `GameManager` | One active instance. Owns wave count, kill count, defense lines, survivor/obstacle lists. |
| `MemoryPool` | Prefer scene or prefab instance. Runtime fallback exists in some systems but should not be relied on for final setup. |
| `DamagePopupSpawner` | Optional scene instance. Can create itself at runtime, but final scene should use configured Resources assets. |
| `ZombieSpawner` | Requires `ZombieSpawnData`, spawn positions, normal zombie prefabs, boss zombie prefabs. |
| Camera | `CameraController` where camera shake is expected by obstacle fracture. |

## Defense Line Setup

`GameManager` defense line entries should contain:

- `lineName`
- `obstacles`
- `retreatPoint`
- `restoredPoint`

Rules:

- Obstacle lists define which defense line is breached when an obstacle fractures.
- Retreat and restored points must be on reachable NavMesh or near valid NavMesh sampling positions.
- Defense-line index order matters. Lower index means earlier/front line.
- A survivor that retreated behind line `N` must not repair obstacles with index `<= N` until return completes.

## Obstacle Setup

Each runtime obstacle should have:

- `Obstacle`
- `ObstacleSpec`
- `HpUI`
- DinoFracture `PreFracturedGeometry` when fracture is expected
- `preFracturedPiecesPrefab` assigned when fracture pieces are required
- Collider on the expected obstacle/vault layer

Notes:

- `Obstacle` also provides vault landing position for survivor defense-line movement.
- Broken obstacles are not repaired by `Repair`; a separate rebuild flow is needed if destroyed defense lines should come back.
- HP UI may be hidden at runtime after initialization.

## Survivor Setup

Each survivor should have:

- `Survivor`
- `SurvivorSpec`
- `NavMeshAgent`
- `Animator`
- Animator parameters matching configured names when animations are required
- Valid `vaultObstacleLayerMask`, or an `Obstacle` layer available for fallback

Survivor movement depends on:

- Reachable NavMesh
- Non-zero move speed
- Positive repair range
- Valid retreat/restored points for defense-line movement

## Zombie Spawner Setup

`ZombieSpawner` requires:

- `ZombieSpawnData`
- `normalZombiePrefabs`
- `bossZombiePrefabs`
- `normalSpawnPositions`
- `bossSpawnPosition`

Spawned zombies should:

- Inherit `PoolObject` when spawned through `MemoryPool.GetInstance<T>`.
- Reset HP, death state, movement, attack state, and cached runtime state in `OnSpawn`.
- Report kill count once per death.

## Turret Placement Setup

For turret placement details, use `TURRET_SYSTEM.md` as the source of truth.

Minimum scene requirements:

- `TurretPlacementController` has a target camera or `Camera.main` exists.
- `turretBaseLayerMask` includes only intended turret base hit areas.
- Each `TurretBaseSlot` has `BuildPoint` and `PlacementHitArea`.
- `PlacementHitArea` collider is on the expected layer.
- Installed turret local position and rotation should be reset under `BuildPoint`.

## Resources Setup

Current runtime Resources paths:

| Resource | Expected Path |
| --- | --- |
| Damage popup prefab | `Assets/__PROJECT__/Resources/UI/DamagePopup.prefab` |
| Damage popup settings | `Assets/__PROJECT__/Resources/UI/DamagePopupSettings.asset` |

Rules:

- Do not move Resources assets unless all `Resources.Load` paths are updated.
- Preserve `.meta` files when moving assets.
- Prefer serialized scene references over `Resources.Load` unless runtime lazy creation is required.

## Layer And Physics Checklist

- Obstacle/vault layer exists and matches survivor `vaultObstacleLayerMask` fallback assumptions.
- Turret base placement layer matches `TurretPlacementController` settings.
- Zombie attack colliders hit intended `IDamageable` targets only.
- Projectile colliders/triggers are compatible with `ProjectileHitDetector` and `ProjectileDamageDealer`.

## Play-Mode Checks After Scene Changes

- Start a wave and verify kill count advances.
- Damage an obstacle and verify HP UI, fracture, and defense-line retreat.
- Verify survivors do not select breached previous lines as repair targets after retreat.
- Restore a line and verify survivors return and clear active defense-line index.
- Place a turret on a valid base and verify occupied/invalid preview states.
- Fire projectiles and verify damage, popup, pierce, and pool return behavior.