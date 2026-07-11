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

## Performance Notes

- Do not add `AudioSource.PlayOneShot` calls directly in projectile, damage tick, beam tick, or status tick hot paths.
- Prefer cue cooldowns, simultaneous limits, and loop handles for high-frequency gameplay.
- Keep normal runtime logs disabled; missing references should be handled by null-safe skips unless profiling audio setup.
