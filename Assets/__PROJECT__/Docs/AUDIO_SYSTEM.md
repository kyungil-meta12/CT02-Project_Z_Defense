# Audio System

## Purpose

Project-level runtime audio system for SFX, BGM, UI sounds, and turret audio events.

## Main Types

| Type | Role |
| --- | --- |
| `ProjectAudioManager` | Central audio entry point. Owns pooled `AudioSource` instances, volume settings, voice limits, cue cooldowns, and priority-based source stealing. |
| `AudioCueSO` | Data asset for one sound event: clips, bus, mixer group, volume/pitch randomization, spatial settings, priority, cooldown, and simultaneous count limit. |
| `ProjectAudioHandle` | Safe handle for stopping or scaling long-lived/looping sounds. |
| `ProjectBgmPlayer` | Scene component that plays a configured BGM cue on enable and stops it on disable. |
| `UIButtonAudioFeedback` | UI component that plays click, hover, and select cues through `ProjectAudioManager`. |
| `ProjectAudioVolumeSlider` | UI slider binding for Master, SFX, BGM, or UI volume. |
| `TurretAudioProfileSO` | Turret-specific event-to-cue map for fire, charge, reload, impact, beam, status, skill, evolution, and placement sounds. |
| `TurretAudioController` | Runtime turret adapter that plays profile events and owns loop handles such as beam, projectile, charge, fire, and reload loops. |
| `TurretAudioFireEventRelay` | Project-level adapter that listens to the external turret `Fired` event and forwards it as `TurretAudioEvent.Fire`. |

## Audio Buses

Current volume buses:

- `Sfx`
- `Bgm`
- `Ui`

`ProjectAudioManager` also has a master volume. All volume setters can save to `PlayerPrefs`, so options UI can persist player settings without adding another storage layer.

## Pooling And Limits

- `ProjectAudioManager` creates a reusable pool of `PooledAudioSource` objects.
- Use `prewarmSourceCount` for expected early-game voice count.
- Use `maxSourceCount` as a hard cap to prevent runaway `AudioSource` creation.
- Use `maxTotalVoices`, `maxSfxVoices`, `maxBgmVoices`, and `maxUiVoices` to keep late-wave audio bounded.
- `AudioCueSO.maxSimultaneous` limits duplicate instances of the same cue.
- `AudioCueSO.minInterval` prevents rapid repeated cue spam.
- Priority follows Unity `AudioSource.priority`: lower numbers are more important. Higher-priority cues can steal lower-priority sounds when global or bus limits are full.

## Turret Rules

- Project-owned adapters should call `TurretAudioController.Play(TurretAudioEvent)` instead of playing `AudioSource` directly.
- External turret code should expose gameplay timing events only; audio mapping belongs to `TurretAudioProfileSO`, `TurretAudioController`, and small project-level relay components.
- Turret placement success should play `Placement` from the placed turret's `TurretAudioProfileSO`.
- Turret placement valid-slot feedback should play `PlacementAvailable` from the currently selected turret definition's `TurretAudioProfileSO` only when the preview newly enters a valid slot.
- Paid turret upgrade success should play `LevelUp` from the upgraded turret's current `TurretAudioProfileSO` after the new level has been applied.
- Turret evolution success should play `Evolution` from the evolved result turret's `TurretAudioProfileSO`; the visual evolution effect remains owned by `TurretEvolutionProgressionSO`.
- Projectile impact audio is triggered by `ProjectileHitDetector` through `ProjectileDamageDealer.PlayImpactAudio`. The damage dealer resolves the turret audio player from the turret's `TurretDamageMeterSource`, so gun and firing APIs do not carry audio parameters.
- Beam, flame, or sustained attack sounds should use `BeamStart`, `BeamLoop`, and `BeamStop`.
- Charge attacks should use `ChargeStart`, `ChargeLoop`, and `ChargeRelease`, then stop `ChargeLoop` before firing.
- Repeating attacks can use `FireLoop` and `FireEnd` instead of one one-shot per projectile.
- Fast repeated attacks can keep `FireLoop` alive from repeated `Fire` trigger events, then stop it after a short no-fire grace time and optionally play `FireEnd`.
- Reload-style attacks should use `ReloadStart`, optional `ReloadLoop`, and `ReloadEnd`.
- A profile entry can be configured as a delayed event after another trigger event. Current Sentinel-01 uses `ReloadStart` 0.5 seconds after `Fire`.
- Delayed entries can also use a trigger-interval ratio instead of fixed seconds. Charge-style turrets such as Plasma Yellow should schedule `ChargeStart` from repeated `Fire` using the current runtime fire interval, so level-based attack-speed changes keep the charge timing proportional.
- Damage ticks, DoT ticks, beam ticks, and chain ticks should not play one sound per tick.
- High-frequency turrets should use cooldowns or loop sounds instead of one one-shot per projectile.
- Meaningful events such as evolution, skill burst, status burst, and boss-impact moments should have higher priority than repeated impact sounds.

## Editor Setup

1. Add one `ProjectAudioManager` to the main scene and tune pool and voice limit values.
2. Create `AudioCueSO` assets from `Project Z Defense/Audio/Audio Cue`.
3. For BGM, set the cue bus to `Bgm` and playback mode to `Loop`, then assign it to `ProjectBgmPlayer`.
4. For UI buttons, set cues to bus `Ui` and assign them to `UIButtonAudioFeedback`.
5. For turrets, create `TurretAudioProfileSO` assets from `Project Z Defense/Audio/Turret Audio Profile` and assign event entries.
6. For settings UI, add `ProjectAudioVolumeSlider` to each Slider and choose Master, SFX, BGM, or UI.

## Current Turret Audio Setup

This section records the current in-progress setup so audio work can continue without rediscovering event timing.

| Turret or cue | Current behavior |
| --- | --- |
| `Sentinel-01` | `Fire` plays at the actual shot timing. `ReloadStart` is scheduled as a delayed event 0.5 seconds after `Fire`. |
| `Vector MG` | `Fire` and `ReloadStart` cue/profile mappings were corrected so fire and reload clips are not swapped. |
| `Machinegun_Blue` | `Fire` plays on shot. `ProjectileLoop` is currently used as a delayed one-shot flyby event about 0.08 seconds after `Fire`; the cue playback mode should stay `One Shot` for `bullet_flyby_slow_03`. |
| `Laser_Blue_1` | Uses sustained `FireLoop` driven by repeated `Fire` trigger events. `FireLoop` stays alive while shots continue and stops after the configured no-fire grace time. |
| `Plasma_Yellow` | `ChargeStart` should be scheduled from `Fire` using trigger-interval ratio instead of a fixed 0.7 second delay, so level-based attack-speed changes keep timing proportional. |
| `Plasma_Yellow Impact` | `Impact` should play only when the projectile reaches its final impact/despawn path, not as a delayed event after `Fire`. |
| `Poison_Turret StatusApply` | `StatusApply` uses `water_bubbling_02_loop` as a followed loop on enemies that newly enter Poison. `PoisonStatusRuntime` fades the loop in and out over about 0.5 seconds, and weak Poison spread by Poison death burst uses the same path. |
| `Poison_Turret StatusBurst` | `StatusBurst` uses `explosion_far_distant_05` and plays once at the Poison death-burst position when a lethally poisoned enemy contaminates nearby enemies. |
| `Poison_Turret StatusLethal` | `StatusLethal` uses `lighter_gas_sparks_flame_06` and plays once when the Poison lethal marker first turns on for a target. |
| `Ignition_Turret BeamStart` | `BeamStart` uses `lighter_gas_flame_ignite_01` as a one-shot ignition sound. `BeamFiringEvent` triggers it only when the flame VFX turns on from an inactive state, not on damage ticks or repeated fire requests while the flame stays active. |
| `Ignition_Turret BeamLoop` | `BeamLoop` uses `gas_leak_03_loop` and stays active while the flame VFX is visible. `BeamFiringEvent` stops the loop when the beam/flame objects are hidden. |
| `Ignition_Turret StatusApply` | `StatusApply` uses `fire_burning_flames_crackle_loop_01` as a followed loop on enemies that newly enter Ignition burn. `IgnitionStatusRuntime` fades the loop in and out over about 0.5 seconds. |
| `Frost_Turret BeamStart` | `BeamStart` uses `wind_blizzard_storm_spell_light_01` as a one-shot beam start sound through the shared `BeamFiringEvent` beam-start trigger. |
| `Frost_Turret BeamLoop` | `BeamLoop` uses `dark_portal_wind_loop_01` and stays active while the Frost beam VFX is visible, stopping through the shared `BeamFiringEvent` beam hide path. |
| `Frost_Turret StatusFreeze` | `StatusFreeze` uses `casting_charge_matter_grow_01` as a one-shot pre-freeze sound on the target. `FrostStatusRuntime` starts it when the estimated remaining buildup time is within the configured cue clip length so the sound can lead into the freeze moment. |
| `Frost_Turret StatusBurst` | `StatusBurst` uses `ice_spell_impact_shatter_03` at the delayed ice cube burst damage timing from `FrostFreezeExplosionDamageTimer`. |
| `Electro_Turret Fire` | `Fire` uses `electric_surge_blast_01` through the standard `TurretAudioFireEventRelay` path when the Electro projectile is fired. |
| `Electro_Turret Impact` | `Impact` randomizes between `taser_stun_gun_zap_electricity_03` and `taser_stun_gun_zap_electricity_04`. Electro projectiles bypass the generic projectile final-impact audio path and instead play Impact only on successfully damaged enemies, including every chain lightning target. Chain impact audio is staggered by a short per-jump delay so multiple linked hits do not collapse into one perceived sound. |
| `Electro_Turret StatusApply` | `StatusApply` uses `electric_sparks_lightning_loop1` as a followed loop while a target is fully charged with the required Electro Shock stack count. `ElectroStatusRuntime` starts it when the stack threshold is reached and stops it when stacks expire, reset, or are consumed by Overload. |
| `Electro_Turret StatusBurst` | `StatusBurst` uses `taser_stun_gun_zap_electricity_04` as a one-shot sound at the target center when Electro Overload consumes the charged Shock stacks. |
| Turret placement | `Placement` plays after a turret is successfully instantiated and its definition/audio profile has been applied. `PlacementAvailable` plays when placement preview newly enters a valid build slot, and does not repeat while staying on the same valid slot. |
| Turret upgrade and evolution | `LevelUp` plays after a paid upgrade succeeds and the new level is applied. `Evolution` plays after the target evolution definition is applied, so the resulting turret profile owns the evolution sound. |

## Handoff Notes

- `TurretAudioProfileSO` supports direct cue lookup, delayed events after a trigger event, trigger-interval-ratio delays, and sustained loop entries.
- `TurretAudioController` owns delayed event queues and loop handles. It should stay the only runtime component that maps `TurretAudioEvent` to `AudioCueSO`.
- `TurretDefinitionRuntimeController` applies the turret audio profile, updates the current `Fire` interval, and ensures `TurretAudioFireEventRelay` exists when an audio profile is assigned.
- `Private Assets/KKW/Modular Turrets Pro/Scripts/Turret/Turret.cs` now exposes only a generic `Fired` event for fire timing. The Private Assets repository has a local commit `ba58512d 터렛 Fire 이벤트 릴레이 구조 정리` and is ahead of `origin/main` by one commit.
- The root project repository still contains many audio asset/profile changes and project-level audio code changes that should be reviewed and committed separately from the Private Assets repository.

## Next Verification Checklist

- In Unity Play Mode, verify `Sentinel-01` still plays `Fire` exactly on shot and `ReloadStart` 0.5 seconds later.
- Verify `Machinegun_Blue` flyby remains a short one-shot after fire and does not loop.
- Verify `Laser_Blue_1` sustained `FireLoop` starts once during continuous firing and stops cleanly when firing stops.
- Verify `Plasma_Yellow Impact` plays on projectile explosion/final impact, not on projectile launch.
- Verify `TurretAudioFireEventRelay` is present or auto-added on turrets with an audio profile after `TurretDefinitionRuntimeController.Apply`.
- Run `dotnet build Assembly-CSharp.csproj --no-restore` on a machine with the .NET SDK or use Unity compile, because the current shell environment did not have `dotnet` installed during the last check.

## Performance Notes

- Do not add `AudioSource.PlayOneShot` calls directly in projectile, damage tick, beam tick, or status tick hot paths.
- Prefer cue cooldowns, simultaneous limits, and loop handles for high-frequency gameplay.
- Keep enemy-followed status loops conservative. Current Poison, Ignition, and Electro status loop cues use `maxSimultaneous = 8` so late-wave status audio does not consume most SFX voices.
- Keep normal runtime logs disabled; missing references should be handled by null-safe skips unless profiling audio setup.
