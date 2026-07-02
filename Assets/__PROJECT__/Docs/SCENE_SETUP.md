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
- `turretBaseSlots`
- `retreatPoint`
- `restoredPoint`

`GameManager` also exposes `startWave` and starting game time scale for test runs and balance checks. Keep production scene values at the intended default before sharing main scene changes.

Rules:

- Obstacle slot lists define which defense line is breached when an obstacle fractures.
- Turret base slot lists define which turret bases are disabled while that defense line is breached.
- Retreat and restored points must be on reachable NavMesh or near valid NavMesh sampling positions.
- Defense-line index order matters. Lower index means earlier/front line.
- A survivor that retreated behind line `N` must not repair obstacles with index `<= N` until return completes.
- Current main-scene slot counts are 1st line `3`, 2nd line `3`, 3rd line `3`, and 4th line `1`.
- The 4th line slot should use `Gate` slot type.

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
| 3rd line, index `2` | 3 | `Obstacle` |
| 4th line, index `3` | 1 | `Gate` |

Rules:

- `ObstaclePlacementController.obstacleSlotLayerMask` must include the `PlacementHitArea` layer.
- `BuildPoint` should hold the installed obstacle or gate as a child.
- Existing scene obstacles should be moved under the correct `BuildPoint` or assigned through the slot's runtime reference.
- `GameManager` should reference all seven slots through defense-line `obstacleSlots`.
- `GameManager` should reference each line's matching turret bases through defense-line `turretBaseSlots`; disabling a base root also disables any installed turret under its `BuildPoint`.
- For play-test sessions where obstacle destruction should not disable installed turrets, enable `GameManager.keepTurretBasesActiveWhenObstacleBroken`; keep it disabled for normal defense-line rules.
- Startup defense-line initialization keeps turret bases enabled so players can place turrets before rebuilding or completing obstacle lines; explicit breach/restore events still toggle linked turret bases.

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

## Obstacle Upgrade UI Setup

Obstacle upgrade UI is manually maintained in the scene. The legacy `Project Z Defense/UI/Create Obstacle Upgrade Popup UI` menu is disabled by default and should not be used for new UI generation.

The editable `ObstacleUpgradePopupCanvas` and `ObstacleUpgradePopup` hierarchy should attach `ObstacleUpgradePopupUI` to the popup controller root and wire the serialized UI references. The visible background image, layout group, texts, and button live under `ObstacleUpgradePopup/Panel`; only `Panel` is hidden or shown at runtime so the controller object stays active for click detection. The runtime component does not create Canvas or popup objects, so layout and styling should be edited directly in the scene.

Setup notes:

- `ObstacleUpgradePopupUI.selectionLayerMask` must include the layers used by installed obstacle colliders.
- The generated popup uses a full-screen transparent `BackgroundButton`; it should have an alpha-0 `Image` with `raycastTarget` enabled and call `ObstacleUpgradePopupUI.OnBackgroundButtonClicked`.
- Installed obstacles must have an `ObstacleUpgradeRuntimeController` with a valid `ObstacleDefinitionSO`.
- The popup hides while `ObstaclePlacementController` is actively placing an obstacle.
- The first-pass UI supports one-level upgrades, current HP display, next cost display, max-level state, repair-reserved state, and level-based prefab replacement notice.
- If an older standalone `ObstacleUpgradePopupUI` object exists from the previous runtime-generated setup, run the menu again to create the editable hierarchy and remove the old component.

## Survivor Setup

Each survivor should have:

- `Survivor`
- `SurvivorSpec`
- `NavMeshAgent`
- `Animator`
- A world collider for `SurvivorInteractionController` selection
- Animator parameters matching configured names when animations are required
- Valid `vaultObstacleLayerMask`, or an `Obstacle` layer available for fallback
- Optional `visibleRoot` child if treatment should hide the visual while keeping the survivor controller alive
- Optional `roleVisualEntries` entries for role-specific renderer Mesh/Material swaps; index `0 = survivor`, `1 = constructionWorker`, `2 = engineer`
- Each `RoleVisualEntry` owns `normal` and `wounded` visual sets. Connect each set's Mesh and optional Material array in the Inspector.
- `RoleVisualEntry.role` is Inspector display metadata only. Runtime visual lookup uses the list index and `SurvivorRole` enum order.
- `engineerStandbyArriveDistance` large enough for the turret base footprint so engineers stop near the base instead of pushing into the center point

Survivor movement depends on:

- Reachable NavMesh
- Non-zero move speed
- Positive repair range
- Valid retreat/restored points for defense-line movement
- Valid final rear point and hospital point for rescued survivor treatment flow
- Valid `roleVisualEntries` visual set Mesh references when role-based visuals are required; missing `wounded` Mesh falls back to `normal`, and missing both falls back to the renderer's default Mesh/Material cached on `Awake`
- Rescue survivors use the `wounded` visual condition until treatment completes, then switch back to `normal` before role selection.
- `visibleRoot` should reference the survivor visual object with `SkinnedMeshRenderer`; the survivor caches that renderer with `GetComponent` on `Awake`.

Rescue and role UI setup:

- Create a `SurvivorRescueSpawnProfileSO` asset from `Project Z Defense/Survivor Rescue Spawn Profile` and configure single wave numbers plus spawn chances.
- Add `SurvivorRescueSpawner` to a scene object and assign `survivorPrefab`, `spawnProfile`, zombie-side `spawnPoints`, `finalRearPoint`, `hospitalPoint`, and `treatmentDuration`.
- If `spawnProfile` is missing, `SurvivorRescueSpawner.spawnChancePerWave` is used as a legacy fallback.
- Add `SurvivorInteractionController` to an editor-authored popup UI object and assign `popupPanel`, `TMP_Text` labels, and `Button` references.
- UI button labels should be English, such as `Treat`, `Construction Worker`, and `Engineer`.
- `EngineerBuffTargetPanelUI` should keep eight target buttons and eight `TurretBaseSlot` references; buttons stay visible and become interactable only when the mapped slot has a placed turret.
- Assign survivor and turret slot layer masks so click selection and engineer drag use `Physics.RaycastNonAlloc` only against relevant layers.
- Turrets can receive stackable engineer damage buffs through `TurretEngineerBuffReceiver`; the interaction controller adds it to the selected turret at runtime if it is missing.
- `TurretDefinitionSO.maxEngineerSeatCount` controls how many engineers can mount each turret. Current default setup uses 1st generation `1`, 2nd generation `2`, and 3rd generation `3`; `0` disables engineer mounting for that definition.

## Game Over Panel Setup

Game over UI is manually maintained in the scene. The legacy `Project Z Defense > UI > Create Game Over Panel UI` menu is disabled by default and should not be used for new UI generation.

Expected hierarchy:

- `GameOverPanelCanvas`
- `GameOverPanelController`
- `Panel`
- `Title`
- `Status`

Scene wiring:

- Assign the generated `GameOverPanelUI` to `GameManager.gameOverPanelUI`.
- Keep `GameOverPanelController` active; the generated `Panel` is the object that fades in/out.
- Set `GameManager.gameOverFadeInDuration` and `gameOverFadeOutDuration`; default runtime expectation is 10 seconds each.
- Ensure `ZombieSpawner` exists in the scene so it can register with `GameManager` and participate in pause, despawn, previous-wave prepare, and resume.
- Ensure obstacle slots have stored `ObstacleDefinitionSO` progress if they need to be rebuilt after gate destruction.

## Warning Popup Setup

Warning popups are scene-authored UI and pooled at runtime.

Required setup:

- Create a warning popup prefab manually.
- Add `WarningPopup` to the prefab root.
- Assign the prefab child `Image` to `WarningPopup.iconImage`.
- Assign the prefab child `TMP_Text` to `WarningPopup.messageText`.
- Add `WarningPopupManager` to a scene runtime UI object.
- Assign `WarningPopupManager.popupPrefab` and `popupRoot`.
- Leave `popupIconSprites` empty until message-type icons are ready, then add sprites in the Inspector.

Rules:

- The popup prefab root is pooled through `MemoryPool`; do not instantiate it manually for repeated warnings.
- `WarningPopup` forces child image raycast and child text raycast off so it does not block `CameraTouchHandler`.
- If multiple warnings occur, the manager stacks them under `popupRoot` and returns the oldest popup when `maxVisibleCount` is reached.
- Gameplay and UI code should use `WarningPopupManager.ShowWarning(...)` instead of directly controlling popup instances.

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
- Place `TurretPlacementSlotUI` buttons manually and assign `placementController` plus `TurretShopEntrySO`. The legacy `Project Z Defense > UI > Create Turret Placement UI` menu is disabled by default.
- Keep `TurretPlacementUI.rebuildOnStart` disabled unless runtime-generated legacy buttons are intentionally needed.
- Maintain the turret upgrade/evolution popup manually; the legacy `Project Z Defense > UI > Create Turret Upgrade Popup UI` menu is disabled by default. The runtime popup controller expects serialized scene UI references and a full-screen transparent `BackgroundButton`.
- The transparent `BackgroundButton` should cover the screen, have an alpha-0 `Image` with `raycastTarget` enabled, and call `TurretTemporaryUpgradePopupUI.OnBackgroundButtonClicked`.
- The popup includes `EngineerSeatTriggers` above the main upgrade content. Keep the desired number of `TurretEngineerSeatButton` children in that container and match `engineerSeatTriggerCount` on `TurretTemporaryUpgradePopupUI`.
- `engineerSeatTriggerCount` is only the prepared popup button pool size; actual mount limits come from each selected turret's `TurretDefinitionSO.maxEngineerSeatCount`.
- If a seat button has a right-side buff text, assign it to `TurretEngineerSeatButton.buffValueText`; mounted engineers display their per-engineer damage bonus as `+10%` style text.
- `turretBaseLayerMask` includes only intended turret base hit areas.
- Each `TurretBaseSlot` has `BuildPoint` and `PlacementHitArea`.
- `PlacementHitArea` collider is on the expected layer.
- Installed turret local position and rotation should be reset under `BuildPoint`.

## Resources Setup

Current runtime Resources paths:

| Resource | Expected Path |
| --- | --- |
| DNP damage popup prefab | `Assets/__PROJECT__/Resources/UI/DNP_DamagePopup_RedGlow.prefab` |
| Damage popup policy profile | `Assets/__PROJECT__/Resources/UI/DamagePopupPolicyProfile.asset` |
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
