# Damage Popup DNP Migration

## Current Status

- Damage popup rendering is migrated to DamageNumbersPro Mesh by default.
- `DamagePopupSettings.asset > Popup Backend` is set to `DamageNumbersProMesh`.
- The active project DNP prefab is `Assets/__PROJECT__/Resources/UI/DNP_DamagePopup_RedGlow.prefab`.
- The original DamageNumbersPro assets under `Assets/__PROJECT__/Private Assets` are not modified.
- The old project `DamagePopup.prefab` World Canvas path remains as a fallback through `DamagePopupBackend.ProjectWorldCanvas`.
- Play Mode check confirmed that damage popups display and behave correctly after the Orthographic camera fix.

## Runtime Flow

1. Damage receivers call `DamagePopupSpawner.SpawnDamage(...)`.
2. `DamagePopupSpawner` applies popup policy before rendering:
   - `Immediate` shows immediately.
   - `Accumulate` merges short-window same-target damage.
   - `Throttled` respects the per-second popup budget.
   - `Suppressed` skips display.
3. `DamagePopupSpawner` applies position offsets, stacked spawn offsets, and rate limiting.
4. If the backend is `DamageNumbersProMesh`, the spawner uses a configured `DamageNumberMesh` prefab.
5. If the DNP prefab reference is missing, the spawner falls back to `Resources/UI/DNP_DamagePopup_RedGlow`.
6. If DNP is unavailable or the backend is `ProjectWorldCanvas`, the spawner uses the legacy project `DamagePopup` MemoryPool path.

## Important Implementation Notes

- `DamagePopupSpawner` owns popup volume control. Do not move accumulation or throttling into DNP prefab settings.
- DNP owns per-popup animation, mesh rendering, billboard behavior, and its own popup updater/pool.
- `DamagePopupSpawner` calls DNP `PrewarmPool()` during initialization for configured DNP prefabs.
- `DamagePopupSpawner` injects the current camera into DNP popups through `cameraOverride`, `fovCamera`, and `orthographicCamera`.
- For Orthographic cameras, `renderThroughWalls` and `consistentScreenSize` must stay disabled. Those DNP options are Perspective-oriented and can shrink popups until they are effectively invisible.
- DNP prefab layer is set to `WorldUI`.
- DNP scale is controlled by `DamagePopupSettings.asset > Dnp Scale`.

## Tuned Values

- `DamagePopupSettings.asset`
  - `Popup Backend`: `DamageNumbersProMesh`
  - `Dnp Scale`: `0.35`
  - `Dnp Use Type Prefix`: enabled
  - `Dnp Critical Prefix`: `CRIT `
  - `Dnp Heavy Prefix`: `HEAVY `
  - `Use Accumulated Damage Popup`: enabled
  - `Accumulation Window`: `0.12`
  - `Max Popups Per Second`: `45`
- `DNP_DamagePopup_RedGlow.prefab`
  - `lifetime`: `0.85`
  - `enablePooling`: enabled
  - `poolSize`: `160`
  - `renderThroughWalls`: disabled
  - `consistentScreenSize`: disabled
  - `enableCombination`: disabled
  - `enableCollision`: disabled
  - `enablePush`: disabled

## Why This Structure

- Project code keeps gameplay policy centralized and predictable.
- DNP is used only as a rendering/animation backend, which reduces coupling to third-party internals.
- The project can switch back to `ProjectWorldCanvas` without touching damage callers.
- Private Assets originals remain clean, so asset updates or reimports are less risky.

## Next Work Plan

1. Verify late-wave performance with rapid-fire, beam, chain, DoT, and AoE damage together.
2. Tune `Dnp Scale`, height offsets, stacked offsets, and DNP prefab animation style for readability.
3. If popup count still feels high, reduce `Max Popups Per Second` or increase `Accumulation Window`.
4. Consider separate DNP prefab variants for normal, critical, and heavy hits if color/punch alone is not enough.
5. If DNP mesh generation becomes a bottleneck, profile spawn spikes and consider stricter accumulation for high-frequency damage types.

## Verification Notes

- Terminal compile check could not be completed in this environment because `dotnet` was not available in PATH.
- Terminal git status could not be checked in this environment because `git` was not available in PATH.
- Remaining verification should be done in Unity Play Mode and Profiler.
