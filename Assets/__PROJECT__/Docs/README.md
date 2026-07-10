# Docs Index

## Fast Start

Read these first for most tasks:

1. `TEAM_CODING_CONVENTION.md`
2. `PROJECT_STRUCTURE.md`
3. This task-specific table below

Read `PROJECT_OVERVIEW.md` when ownership, design intent, or shared API boundaries are unclear.

## Task-Specific Reading

| Task | Read |
| --- | --- |
| General code change | `TEAM_CODING_CONVENTION.md`, `PROJECT_STRUCTURE.md` |
| Architecture or responsibility boundary | `PROJECT_OVERVIEW.md`, `PROJECT_STRUCTURE.md` |
| Survivor, obstacle, defense line, zombie spawn | `GAMEPLAY_RUNTIME_FLOW.md`, `SCENE_SETUP.md` |
| Turret, projectile, placement, turret UI, turret skill | `TURRET_SYSTEM.md`, `TURRET_UI_REWORK_STATUS.md`, `COMMON_SYSTEMS.md` |
| Reward, currency, zombie drops, upgrade/evolution costs | `REWARD_SYSTEM.md`, `COMMON_SYSTEMS.md`, relevant feature document |
| Damage popup, pooling, shared damage API | `COMMON_SYSTEMS.md`, `DAMAGE_POPUP_DNP_MIGRATION.md`, relevant feature document |
| Audio, SFX, BGM, UI sound, turret sound | `AUDIO_SYSTEM.md`, `COMMON_SYSTEMS.md`, and `TURRET_SYSTEM.md` for turret-owned events |
| Main scene, prefab wiring, NavMesh, layer, Resources | `SCENE_SETUP.md` |
| Private Assets or external asset integration | `PROJECT_STRUCTURE.md`, `PROJECT_OVERVIEW.md` |

## Documents

| Document | Purpose |
| --- | --- |
| `TEAM_CODING_CONVENTION.md` | Code style, Unity rules, pooling rules, logging, mobile performance. |
| `PROJECT_OVERVIEW.md` | Game concept, owner boundaries, shared APIs, repository/asset policy. |
| `PROJECT_STRUCTURE.md` | Folder map, runtime areas, where new work belongs. |
| `GAMEPLAY_RUNTIME_FLOW.md` | Wave, spawn, damage, obstacle fracture, defense-line, survivor repair/retreat flow. |
| `TURRET_SYSTEM.md` | Turret data, level/evolution, placement, projectile, VFX, pooling setup. |
| `TURRET_UI_REWORK_STATUS.md` | Current turret UI rework status, known weak points, next test/fix checklist. |
| `REWARD_SYSTEM.md` | Reward/currency model, zombie kill rewards, costs, and economy migration plan. |
| `SCENE_SETUP.md` | Main scene and prefab setup checklist. |
| `COMMON_SYSTEMS.md` | `IDamageable`, `MemoryPool`, `PoolObject`, damage popup, Resources, logging, performance. |
| `DAMAGE_POPUP_DNP_MIGRATION.md` | DamageNumbersPro damage popup backend status, tuned values, and follow-up plan. |
| `AUDIO_SYSTEM.md` | Project audio manager, cue assets, pooling, volume buses, UI/BGM setup, and turret audio event rules. |

## Maintenance Rule

After code changes, check whether the affected feature document needs updates. If no Docs update is needed, report that Docs were checked.

The root-level `.cs` planning documents under `Assets/__PROJECT__` are retained as historical notes. Prefer the Markdown documents in this `Docs` folder as the current source of truth for setup, pooling, runtime ownership, and migration status.
