# Damage Popup DNP Migration

## Current Status

- Damage popup rendering is migrated to DamageNumbersPro Mesh.
- Damage popup rendering is DNP-only in runtime code.
- The active project DNP prefab is `Assets/__PROJECT__/Resources/UI/DNP_DamagePopup_RedGlow.prefab`.
- The original DamageNumbersPro assets under `Assets/__PROJECT__/Private Assets` are not modified.
- The old project `DamagePopup.prefab` World Canvas fallback path was removed after DNP Play Mode verification.
- Orthographic camera compatibility is handled by disabling DNP `renderThroughWalls` and `consistentScreenSize`.
- The DNP popup prefab uses `WorldUI / Order 1000` on its root `SortingGroup` and child renderers so it renders above `HpUI` world-space canvases.
- Play Mode verification confirmed popup visibility, scale, position, readability, and runtime stats in high-count zombie scenarios.

## Runtime Flow

1. Damage receivers call `DamagePopupSpawner.SpawnDamage(...)`.
2. `DamagePopupSpawner` applies popup policy before rendering:
   - `Immediate` shows immediately.
   - `Accumulate` merges short-window same-target damage.
   - `Throttled` respects the per-second popup budget.
   - `Suppressed` skips display.
3. `DamagePopupSpawner` applies position offsets, stacked spawn offsets, and rate limiting.
4. The spawner delegates rendering to `DnpDamagePopupBackend`.
5. `DnpDamagePopupBackend` uses the configured `DamageNumberMesh` prefab, or falls back to `Resources/UI/DNP_DamagePopup_RedGlow`.
6. If DNP is unavailable or spawn fails, the spawner records `생성실패` in runtime stats and skips the visual.

## Important Implementation Notes

- `DamagePopupSpawner` owns popup volume control. Do not move accumulation or throttling into DNP prefab settings.
- `DamagePopupSpawner` does not directly depend on DamageNumbersPro types; DNP-specific spawning, prewarm, camera injection, color, prefix, and scale are isolated in `DnpDamagePopupBackend`.
- Gameplay systems should only call `DamagePopupSpawner.SpawnDamage(...)` and must not call `DamageNumberMesh.Spawn(...)` directly.
- Popup policy is centralized through `DamagePopupPolicyResolver` for turret direct hits and high-frequency tick damage.
- `DamagePopupPolicyResolver` uses `DamagePopupPolicyProfile.asset` through `DamagePopupSettings.asset` so policy tuning can be done in the Inspector.
- Beam tick damage always accumulates, including critical/heavy rolls, so DNP does not receive one immediate popup per beam tick.
- DNP owns per-popup animation, mesh rendering, billboard behavior, and its own popup updater/pool.
- `DnpDamagePopupBackend` calls DNP `PrewarmPool()` during initialization for configured DNP prefabs.
- `DnpDamagePopupBackend` injects the current camera into DNP popups through `cameraOverride`, `fovCamera`, and `orthographicCamera`.
- For Orthographic cameras, `renderThroughWalls` and `consistentScreenSize` must stay disabled. Those DNP options are Perspective-oriented and can shrink popups until they are effectively invisible.
- DNP prefab GameObject layer is `WorldUI`.
- DNP prefab root `SortingGroup`, child `MeshRenderer` components, and TMP renderer sorting fields must stay on `WorldUI / Order 1000`. `HpUI.prefab` uses a world-space Canvas on `WorldUI / Order 0`, so changing the DNP sorting layer back to `Default` can make HP bars cover damage numbers.
- DNP scale is controlled by `DamagePopupSettings.asset > Dnp Scale`.

## Tuned Values

- `DamagePopupSettings.asset`
  - `공통 표시 정책 > Popup Policy Profile`: `DamagePopupPolicyProfile`
  - `공통 생성 위치 > Normal Zombie Height Offset`: `4`
  - `공통 생성 위치 > Boss Zombie Height Offset`: `12`
  - `공통 표시량 제어 > Use Accumulated Damage Popup`: enabled
  - `공통 표시량 제어 > Accumulation Window`: `0.15`
  - `공통 표시량 제어 > Max Popups Per Second`: `45`
  - `런타임 계측 > Enable Runtime Stats`: enabled while profiling
  - `런타임 계측 > Runtime Stats Log Interval`: `5`
  - `DNP 렌더링 > Dnp Scale`: `2.5`
  - `DNP 렌더링 > Dnp Use Type Prefix`: enabled
  - `DNP 렌더링 > Dnp Critical Prefix`: `CRIT `
  - `DNP 렌더링 > Dnp Heavy Prefix`: `HEAVY `
- `DamagePopupPolicyProfile.asset`
  - `Direct Normal Policy`: `Accumulate`
  - `Direct Critical Policy`: `Immediate`
  - `Direct Heavy Policy`: `Immediate`
  - `High Frequency Normal/Critical/Heavy Policy`: `Accumulate`
  - `Chain`, `Damage Over Time`, `Area Of Effect`, `Status Burst`: `Accumulate`
- `DNP_DamagePopup_RedGlow.prefab`
  - `lifetime`: `0.85`
  - `enablePooling`: enabled
  - `poolSize`: `160`
  - GameObject layer: `WorldUI`
  - Sorting Layer / Order: `WorldUI / 1000`
  - `renderThroughWalls`: disabled
  - `consistentScreenSize`: disabled
  - `enableCombination`: disabled
  - `enableCollision`: disabled
  - `enablePush`: disabled

## Runtime Profiling Baseline

- Recent high-count zombie check over 5 seconds:
  - `요청`: about `400`
  - `생성`: about `200`
  - `생성실패`: `0`
  - `제한폐기`: about `5`
  - `누적합산`: about `190`
  - `대기타겟`: under about `10`
- This is acceptable for the current tuning because roughly half of popup requests are merged, DNP spawn failures are zero, and rate-limit drops are low.
- `poolSize = 160` is expected to be sufficient with the current `lifetime = 0.85` and about `40` spawned popups per second because average concurrent popup count is far below the pool size.
- If Profiler shows repeated runtime `Instantiate`, popup-related `GC.Alloc`, or visible spawn spikes during late waves, raise `poolSize` to `220` first, then up to `300` only if spikes remain.
- Keep `Enable Runtime Stats` on while tuning and turn it off for normal play or release builds unless diagnosing popup behavior.

## Why This Structure

- Project code keeps gameplay policy centralized and predictable.
- The renderer adapter boundary keeps DamageNumbersPro isolated from damage receivers and from popup volume policy.
- DNP is used only as a rendering/animation backend, which reduces coupling to third-party internals.
- The legacy World Canvas path is removed so production behavior does not silently hide missing DNP setup behind a slower fallback.
- Private Assets originals remain clean, so asset updates or reimports are less risky.

## Next Work Plan

1. Enter Play Mode in `Assets/__PROJECT__/Scenes/Main.unity`.
2. Confirm normal zombie hits show DNP popups above the target, not inside the mesh or off-screen.
3. Confirm boss zombie popups use the taller boss offset and remain readable.
4. Confirm normal, critical, and heavy popup colors/prefixes remain distinguishable at the current camera zoom.
5. Confirm rapid-fire, beam, chain, DoT, and AoE damage do not create unreadable popup spam or visible frame spikes.
6. Confirm beam critical/heavy rolls still show the stronger popup style after accumulation instead of spawning every tick.
7. For profiling, temporarily enable `런타임 계측 > Enable Runtime Stats` and inspect request/spawn/spawn-failure/rate-limit counts in Console.
8. Confirm HP bars do not cover damage numbers. If they do, first verify DNP sorting is still `WorldUI / Order 1000`; if sorting is correct, tune height offsets.
9. Tune `Dnp Scale`, height offsets, stacked offsets, and DNP prefab animation style only after the Play Mode checks above.
10. If popup count still feels high, reduce `Max Popups Per Second` or increase `Accumulation Window`.
11. Consider separate DNP prefab variants for normal, critical, and heavy hits if color/punch alone is not enough.
12. If DNP mesh generation becomes a bottleneck, profile spawn spikes and consider stricter accumulation for high-frequency damage types.

## Verification Notes

- Static asset/code check confirmed the DNP backend, project DNP prefab reference, `Dnp Scale = 2.5`, runtime stats enabled for profiling, `renderThroughWalls = false`, `consistentScreenSize = false`, `enablePooling = true`, `poolSize = 160`, and `WorldUI / Order 1000` sorting.
- Legacy World Canvas runtime fallback, `DamagePopup.cs`, `DamagePopupBackend.cs`, and `DamagePopup.prefab` were removed.
- Play Mode check confirmed HP bars no longer cover DNP damage numbers after the DNP sorting layer/order correction.
- Terminal compile check could not be completed in this environment because `dotnet` was not available in PATH.
- Terminal git status could not be checked in this environment because `git` was not available in PATH.
- Unity Play Mode and Profiler verification remain required.
