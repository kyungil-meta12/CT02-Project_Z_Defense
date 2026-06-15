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
| `GameManager` | One active instance. Owns wave count, kill count, starting game time scale, defense lines, survivor/obstacle lists. |
| `MemoryPool` | Prefer scene or prefab instance. Runtime fallback exists in some systems but should not be relied on for final setup. |
| `DamagePopupSpawner` | Optional scene instance. Can create itself at runtime, but final scene should use configured Resources assets. |
| `ZombieSpawner` | Requires `ZombieWaveSpawnProfileSO`, spawn positions, normal zombie prefabs, boss zombie prefabs. |
| `ObstaclePlacementController` | Required for runtime obstacle/gate placement UI. |
| Camera | `CameraController` where camera shake is expected by obstacle fracture. |

## Defense Line Setup

`GameManager` defense line entries should contain:

- `lineName`
- `obstacleSlots`
- `retreatPoint`
- `restoredPoint`

`GameManager` also exposes `startWave` and starting game time scale for test runs and balance checks. Keep production scene values at the intended default before sharing main scene changes.

Rules:

- Obstacle slot lists define which defense line is breached when an obstacle fractures.
- Retreat and restored points must be on reachable NavMesh or near valid NavMesh sampling positions.
- Defense-line index order matters. Lower index means earlier/front line.
- A survivor that retreated behind line `N` must not repair obstacles with index `<= N` until return completes.
- Required slot counts are 1st line `3`, 2nd line `3`, 3rd line `1`.
- The 3rd line slot should use `Gate` slot type.

## Obstacle Build Slot Setup

Each fixed obstacle/gate point should have:

- `ObstacleBuildSlot`
- `BuildPoint`
- `PlacementHitArea`
- Collider on `PlacementHitArea`
- `defenseLineIndex`
- `slotIndex`
- `slotType`

Recommended hierarchy:

```text
ObstacleBuildSlot_0_0
- BuildPoint
- PlacementHitArea
```

Slot layout:

| Defense Line | Slot Count | Slot Type |
| --- | --- | --- |
| 1st line, index `0` | 3 | `Obstacle` |
| 2nd line, index `1` | 3 | `Obstacle` |
| 3rd line, index `2` | 1 | `Gate` |

Rules:

- `ObstaclePlacementController.obstacleSlotLayerMask` must include the `PlacementHitArea` layer.
- `BuildPoint` should hold the installed obstacle or gate as a child.
- Existing scene obstacles should be moved under the correct `BuildPoint` or assigned through the slot's runtime reference.
- `GameManager` should reference all seven slots through defense-line `obstacleSlots`.

## Obstacle Placement UI Setup

Manual scene buttons are the default setup.

Each obstacle or gate placement button should have:

- `ObstaclePlacementSlotUI`
- `placementController`
- `buildEntry`
- icon/name/cost UI references when visible labels are required

Rules:

- Use one button for the obstacle build entry and one button for the gate build entry.
- `ObstaclePlacementUI.rebuildOnStart` should stay disabled unless runtime-generated buttons are intentionally needed.
- Runtime rebuild is optional and should not be required for manually placed buttons.
- If a preview appears rotated, adjust the `ObstacleBuildEntrySO.placementLocalEulerAngles` value; preview and actual placement use the same rotation.
- When using obstacle upgrades, assign `ObstacleBuildEntrySO.obstacleDefinition`; the definition can override the entry's prefab, preview, slot type, display name, icon, and level-based prefab progression.

## Obstacle Setup

Each runtime obstacle should have:

- `Obstacle`
- `ObstacleUpgradeRuntimeController` when it participates in upgrade or rebuild-level inheritance
- `ObstacleSpec`
- `HpUI`
- DinoFracture `PreFracturedGeometry` when fracture is expected
- `preFracturedPiecesPrefab` assigned when fracture pieces are required
- Collider on the expected obstacle/vault layer

Notes:

- `Obstacle` also provides vault landing position for survivor defense-line movement.
- Broken obstacles are not repaired by `Repair`; a separate rebuild flow is needed if destroyed defense lines should come back.
- Runtime obstacle/gate rebuilding should use `ObstaclePlacementController` and `ObstacleBuildSlot`.
- HP UI may be hidden at runtime after initialization.
- `ObstacleDefinitionSO` owns upgrade cost, max level, and level-based prefab progression. Rebuilding the same definition in the same slot inherits the slot's stored destroyed-obstacle level.
- Level-based replacement prefabs must include `Obstacle`, `HpUI`, fracture setup, and compatible colliders because the slot may instantiate them directly during upgrade or rebuild.

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

- `ZombieWaveSpawnProfileSO`
- `normalZombiePrefabs`
- `bossZombiePrefabs`
- `spwanPoints`
- `destinations`

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
- Verify all seven defense-line slots are registered in `GameManager`.
- Damage an obstacle and verify HP UI, fracture, and defense-line retreat.
- Rebuild the missing obstacle/gate through placement UI and verify the defense line restores only after required slots are occupied.
- Verify survivors do not select breached previous lines as repair targets after retreat.
- Restore a line and verify survivors return and clear active defense-line index.
- Place a turret on a valid base and verify occupied/invalid preview states.
- Fire projectiles and verify damage, popup, pierce, and pool return behavior.
