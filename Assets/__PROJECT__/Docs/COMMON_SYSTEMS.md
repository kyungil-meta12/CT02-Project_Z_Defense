# Common Systems

## Purpose

This document records shared runtime contracts and utility systems used by multiple gameplay modules.

## IDamageable

Path:

- `Assets/__PROJECT__/Prefabs/Damageable/IDamageable.cs`

Contract:

```csharp
float TotalHp { get; }
float CurrHp { get; }
bool IsAlive { get; }
void TakeDamage(float damage);
```

Rules:

- `IsAlive` must become false before dead objects can be targeted or damaged again.
- HP should be clamped between `0` and `TotalHp`.
- Repeated death handling must be guarded.
- Implementations own their mutable HP state; external systems read state and call `TakeDamage` only.

## Status Effect Receivers

Current receiver interfaces:

- `ProjectZDefense.StatusEffects.IFrostStatusEffectReceiver`
- `ProjectZDefense.StatusEffects.IPoisonStatusEffectReceiver`
- `ProjectZDefense.StatusEffects.IElectroStatusEffectReceiver`

Current payload value types:

- `ProjectZDefense.StatusEffects.FrostStatusPayload`
- `ProjectZDefense.StatusEffects.PoisonStatusPayload`
- `ProjectZDefense.StatusEffects.ElectroStatusPayload`

Rules:

- Damage receivers or their delegated status runtime components own mutable status timers and stack state.
- Attack systems should pass immutable payload values and should not mutate enemy runtime state directly.
- Status timers should reset on spawn, despawn, and death.
- Damage-over-time effects should call the same `TakeDamage` path as direct damage so HP UI, death handling, rewards, and damage popups stay consistent.
- Visual controllers should only reflect active/inactive state through `StatusEffectVisualSlot`; they should not own gameplay status logic.
- Repeated status application should avoid allocations and should not search scene objects in hot paths.
- Targeting systems should use `ITargetCandidateFilter` components for status-driven target exclusion instead of hard-coding status-specific checks.

Poison-specific shared contract:

- `ProjectZDefense.StatusEffects.PoisonStatusPayload` carries max-HP tick damage values, stack policy, boss multiplier, and an optional `PoisonDeathBurstProfileSO`.
- `ProjectZDefense.StatusEffects.IPoisonStatusEffectReceiver.IsPoisonLethalPending` exposes whether the receiver's remaining Poison ticks can kill its current HP.
- Targeting systems may read `IsPoisonLethalPending` to avoid wasting Poison shots, but they should not change enemy Poison state.
- Visual systems should receive lethal state from the damage receiver and should not calculate lethal damage themselves.
- Weak area Poison from death burst uses the same receiver contract as direct Poison projectile hits.
- Boss receivers can accept Poison and weak area Poison, but boss death-burst triggering is a zombie implementation rule, not part of the shared interface.

Electro-specific shared contract:

- `ProjectZDefense.StatusEffects.ElectroStatusPayload` carries level-scaled chain lightning count, fixed chain radius/falloff, level-scaled Shock stack duration, Overload trigger policy, level-scaled single-target Overload values, stun values, and chain VFX settings.
- `ProjectZDefense.StatusEffects.IElectroStatusEffectReceiver.ApplyElectroStatus` receives the payload, chain index, and source damage so enemies can apply Shock, Overload, and stun rules consistently for direct and chained hits.
- Chain damage itself is applied by `ElectroChainLightningUtility` through `IDamageable.TakeDamage`; enemy receivers should own only their mutable Electro status state.
- Enemy Electro runtimes should ignore new Shock stack application while an Overload long stun is active, so consumed stacks cannot immediately rebuild during the stun window.
- Electro chain target selection should prefer non-full targets with existing Shock stacks before distance, while excluding Overload-stunned targets. Full-Shock targets should remain fallback candidates so their Shock timer can be refreshed when no better chain target exists.
- Electro chain VFX is turret-owned presentation. Enemy receivers should not instantiate chain-link particle or core-line visuals.

## MemoryPool

Paths:

- `Assets/__PROJECT__/Scripts/Singleton/MemoryPool/MemoryPool.cs`
- `Assets/__PROJECT__/Scripts/PoolObject/PoolObject.cs`

Use when repeatedly spawning:

- zombies
- projectiles
- VFX
- damage popups
- warning popups
- temporary runtime helpers

Rules:

- Poolable component prefabs should inherit `PoolObject` when using `GetInstance<T>(PoolObject prefab)`.
- Runtime objects with gameplay colliders should prefer the position/rotation overload `GetInstance<T>(PoolObject prefab, Vector3 position, Quaternion rotation)` so reused instances are moved before activation.
- Reset mutable runtime state before or during spawn. Do not rely on prefab defaults after reuse.
- `OnBeforeSpawn` is for pre-activation reset work.
- `OnSpawn` is for post-pop initialization.
- `ReturnInstance` must be used instead of `Destroy` for pooled objects.
- Runtime singleton teardown must only clear `Inst` when `Inst == this`.

## Pooling Edge Cases

Check these before finishing pooled-object work:

- Object returned twice.
- Object disabled by scene or hierarchy before returning to pool.
- Missing `OriginStack` fallback.
- Stale target, damage, speed, animator, particle, trail, or collider state from previous use.
- Pooled damage receivers that disable colliders on death should restore collider and Rigidbody simulation state before reuse.
- Event subscriptions not removed before return.
- Components on child objects left active or detached incorrectly.

## Damage Popup

Paths:

- `Assets/__PROJECT__/Scripts/DamagePopup.cs`
- `Assets/__PROJECT__/Scripts/DamagePopupSpawner.cs`
- `Assets/__PROJECT__/Scripts/DamagePopupSettings.cs`
- `Assets/__PROJECT__/Resources/UI/DamagePopup.prefab`
- `Assets/__PROJECT__/Resources/UI/DamagePopupSettings.asset`

Flow:

1. Damage receiver applies damage and updates HP/alive state.
2. Damage receiver calls `DamagePopupSpawner.SpawnDamage(targetTransform, damage)` when visual feedback is needed.
3. Spawner loads settings and prefab from `Resources/UI` if needed.
4. Spawner gets a pooled `DamagePopup` instance.
5. `DamagePopup.Init` applies text/settings/camera each spawn.
6. Popup animates and returns to pool.

Rules:

- Do not allocate popup prefabs per hit manually.
- Always reinitialize pooled popup text, color, scale, lifetime, and camera-dependent values.
- Avoid logs per damage tick.

## Warning Popup

Paths:

- `Assets/__PROJECT__/Scripts/UI/WarningPopup.cs`
- `Assets/__PROJECT__/Scripts/UI/WarningPopupManager.cs`

Flow:

1. Gameplay or UI code calls `WarningPopupManager.ShowWarning(...)`.
2. The manager resolves the optional icon by `popupIconSprites` index.
3. The manager gets a pooled `WarningPopup` instance from `MemoryPool`.
4. `WarningPopup.Init` applies the message, icon sprite, and display duration.
5. The popup disables UI interaction/raycast targets and returns to the pool when its lifetime ends.

Rules:

- The warning popup prefab is authored in Unity and assigned to `WarningPopupManager.popupPrefab`.
- The prefab root must include `WarningPopup`; its child `Image` and `TMP_Text` references are assigned on that component.
- The manager does not create popup visuals or load warning assets from `Resources`.
- `popupIconSprites` starts empty; designers add message-type icons in the Inspector.
- Warning popups must not block `CameraTouchHandler` input, so child `Image.raycastTarget` and `TMP_Text.raycastTarget` stay disabled.

## PooledObjectUtility

Path:

- `Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Scripts/PooledObjectUtility.cs`

Current role:

- Project-level wrapper for pooled GameObject spawning used by turret/projectile/effect/skill systems.
- Provides fallback behavior when `MemoryPool` is missing.

Use it for:

- turret evolution effects
- projectile VFX
- Frost freeze explosion effects
- Poison lethal-death burst effects

Do not use it to bypass proper prefab setup or pooling reset rules.

Follow-up pooling rules:

- Repeated gameplay effects should have a `PoolObject`-compatible prefab setup instead of relying on fallback `Instantiate/Destroy`.
- Chain-capable effects such as Poison death burst should be treated as repeated gameplay effects, not rare one-off effects.
- Long-lived effects that follow a runtime target must clear target references, cached transforms, timers, pending damage flags, and payload data before reuse.
- Followed effects should cache their target transform, collider, or explicit anchor during initialization; avoid repeated child hierarchy scans in `Update`.
- Particle and trail state must be cleared or restarted on reuse so old particles do not leak into the next spawn.
- Cancel paths must be safe when the owner dies, is disabled, or returns to a pool before a delayed effect finishes.

## Resources.Load Usage

Allowed current Resources loads:

- `UI/DamagePopup`
- `UI/DamagePopupSettings`

Rules:

- Keep Resources paths stable.
- Prefer serialized references for scene-owned systems.
- If a runtime-created singleton uses Resources fallback, still configure the production scene explicitly when practical.

## Logging

Runtime debug logs should be:

- Korean
- actionable
- throttled or one-shot where possible
- not emitted every frame or per projectile hit unless explicitly debugging

Examples of useful logs:

- missing required prefab/reference
- invalid defense-line index
- unreachable NavMesh destination
- missing animator parameter once per object
- missing Resources asset fallback

## Mobile/Idle Performance Rules

Hot paths include:

- `Update`
- AI ticks
- projectile movement
- targeting
- damage loops
- UI refresh loops

Avoid in hot paths:

- LINQ
- closures
- repeated `FindObjectOfType`
- `FindGameObjectsWithTag`
- allocating physics APIs
- string formatting/logging
- new temporary collections

Prefer:

- cached components
- cached animator hashes
- squared-distance checks
- throttled polling
- NonAlloc physics APIs
- reusable buffers/lists
- pooling and explicit reset
