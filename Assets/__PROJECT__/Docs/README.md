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
| Turret, projectile, placement, turret UI, turret skill | `TURRET_SYSTEM.md`, `COMMON_SYSTEMS.md` |
| Reward, currency, zombie drops, upgrade/evolution costs | `REWARD_SYSTEM.md`, `COMMON_SYSTEMS.md`, relevant feature document |
| Damage popup, pooling, shared damage API | `COMMON_SYSTEMS.md`, relevant feature document |
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
| `REWARD_SYSTEM.md` | Reward/currency model, zombie kill rewards, costs, and economy migration plan. |
| `SCENE_SETUP.md` | Main scene and prefab setup checklist. |
| `COMMON_SYSTEMS.md` | `IDamageable`, `MemoryPool`, `PoolObject`, damage popup, Resources, logging, performance. |

## Maintenance Rule

After code changes, check whether the affected feature document needs updates. If no Docs update is needed, report that Docs were checked.
