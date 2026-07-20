# Project Architecture

## Purpose

This document is the system-level map of Project Z Defense. It explains runtime ownership, initialization, cross-system data flow, shared contracts, data sources, and extension boundaries.

Use this document when:

- onboarding to the whole project;
- tracing a gameplay event across multiple systems;
- deciding which system should own new runtime state;
- locating the source of truth for scene, prefab, or ScriptableObject data;
- reviewing whether a change creates a parallel implementation.

Detailed tuning and setup rules remain in the feature documents linked throughout this document.

## Architecture At A Glance

The project is a Unity 3D zombie defense game organized around scene-owned runtime controllers, prefab-local actors, project-level shared services, and ScriptableObject configuration.

```text
Input / Runtime UI
        |
        v
Placement, selection, upgrade, transaction controllers
        |
        +----------------------+
        v                      v
GameManager               InventorySystem
wave and defense state     items and currencies
        |                      |
        +----------+-----------+
                   v
        Gameplay runtime actors
 zombies <-> obstacles <-> survivors <-> turrets
                   |
                   v
       shared combat and feedback
 IDamageable, DamageInfo, status runtimes,
 damage meter, popup, audio, pooling
```

The main architectural rule is that configuration and mutable state are separated:

| Concern | Owner |
| --- | --- |
| Designer-authored values | ScriptableObject assets and serialized prefab/scene fields |
| Global wave and defense-line state | `GameManager` |
| Per-actor mutable state | Zombie, obstacle, survivor, turret, and status runtime components |
| Items and currency balances | `InventorySystem`; gameplay spending uses the shared item/cost API |
| Reusable spawned object lifetime | `MemoryPool`, `PoolObject`, and feature-specific returners |
| Presentation policy | Popup, audio, VFX, and UI controllers; gameplay owners expose events or payloads |

## Runtime Layers

### 1. Scene Composition Layer

`Assets/__PROJECT__/Scenes/Main.unity` is the production integration scene. It supplies explicit references for global managers, UI, camera input, placement areas, defense-line slots, spawn points, and NavMesh-dependent actors.

Scene and prefab YAML are integration data, not disposable generated files. Preserve `guid`, `fileID`, component order, prefab instance links, and serialized references.

Read `SCENE_SETUP.md` before modifying scene wiring.

### 2. Global Runtime Services

| Service | Primary responsibility | Must not own |
| --- | --- | --- |
| `GameManager` | Wave number, kill target/progress, defense-line state, obstacle and survivor registration, repair target policy, wave restart coordination | Per-zombie HP, per-survivor movement, UI rendering |
| `InventorySystem` | Item metadata lookup, owned quantities, add/use/force-use operations, affordability, atomic spend, refund, and inventory change notifications | Turret or obstacle upgrade rules |
| `MemoryPool` | Prefab-keyed reusable instances, pool containers, prewarm, spawn lifecycle | Feature-specific payload reset rules |
| `ProjectAudioManager` | Pooled audio sources, buses, volume persistence, cue limits, cooldowns, priority and voice stealing | Turret firing or damage timing |
| `DamagePopupSpawner` | Damage popup policy, target-based accumulation, throttling, spawn budget, backend dispatch and profiling | HP mutation or damage calculation |
| `TurretDamageMeterManager` | Wave-scoped actual-damage attribution and ranking | Damage application |

Singleton consumers must tolerate missing instances during teardown and scene transitions. Production scenes should still wire explicit references where the feature document requires them.

### 3. Gameplay Actor Layer

| Actor area | Runtime owner | Main state |
| --- | --- | --- |
| Normal zombie | `NormalZombie` | HP, movement/attack, target, death, reward handoff, status runtimes, pool reset |
| Boss zombie | `BossZombie` and boss behavior components | HP, behavior, boss skill, target, status restrictions, reward handoff |
| Obstacle/gate | `Obstacle`, `ObstacleBuildSlot`, `ObstacleUpgradeRuntimeController` | HP, fracture, occupancy, stored definition/level, rebuild and upgrade |
| Survivor | `Survivor` | role, navigation, repair reservation, retreat/return, rescue/treatment, engineer assignment, vaulting |
| Turret | `TurretDefinitionRuntimeController` plus firing adapters | definition, tier/total level, calculated stats, VFX/status/audio profiles, evolution |
| Projectile/beam | projectile components and `BeamFiringEvent` | target, damage payload, collision correction, pierce, status delivery, pool return |

Actor components mutate only their own runtime state. Cross-actor effects go through shared contracts, manager APIs, events, or feature payload interfaces.

### 4. Data And Policy Layer

ScriptableObjects are the source of truth for authored gameplay configuration:

| Data family | Examples | Responsibility |
| --- | --- | --- |
| Wave data | `ZombieWaveSpawnProfileSO`, zombie specs, boss schedules | Composition, count, interval, stage multipliers, boss timing |
| Turret data | `TurretDefinitionSO`, stat/growth/cost/VFX/status/evolution profiles | Identity, calculation inputs, presentation selection, branch rules |
| Obstacle data | `ObstacleDefinitionSO`, cost and prefab progression profiles | Identity, HP growth, upgrade cost, level appearance |
| Reward data | `ZombieRewardProfileSO`, modifiers, `ResourceCost[]` | Reward entries, contextual modifiers, shared cost representation |
| Item data | item metadata assets generated or maintained with `ItemData.csv` | Item identity, craft and decomposition relationships, UI metadata |
| Feedback data | damage popup settings/policies, audio cues/profiles | Readability, spawn limits, colors, timing, audio routing |

ScriptableObjects must not hold mutable play-session state. Runtime controllers calculate and cache derived values from them.

### 5. Editor And Validation Layer

Editor-only tooling is part of the production workflow:

- zombie and item CSV tools keep bulk-authored data synchronized;
- turret economy validation detects missing or invalid cost/profile relationships;
- the turret wave balance simulator combines wave HP, obtainable currencies, craft/decompose relations, placement/upgrade/evolution costs, turret DPS, status assumptions, and obstacle survival;
- reports export Korean-header CSV files for review.

Editor code must not become a runtime dependency.

## Startup And Initialization

Unity lifecycle order is supported by defensive registration rather than relying on one large bootstrapper.

```text
Scene load
  |
  +-> global services establish singleton/reference state
  |     GameManager, InventorySystem, MemoryPool, audio and UI managers
  |
  +-> slots and actors register with their owning managers
  |     defense slots, obstacles, survivors, damage-meter sources
  |
  +-> ZombieSpawner reads ZombieWaveSpawnProfileSO
  |     resolves current stage and sends destination kill count
  |
  +-> turret/obstacle runtime controllers apply definition and level
  |
  +-> prewarmers create expected projectile, VFX, popup and audio capacity
  |
  +-> gameplay input and wave spawning become active
```

Initialization rules:

- registration and unregistration must be safe when repeated;
- pooled actors must restore all mutable state before activation;
- missing required data disables or safely skips the owning feature and logs an actionable Korean warning;
- scene-owned references are preferred over runtime object searches;
- do not depend on `Start` order between unrelated GameObjects when an explicit registration or initialization API can express the relationship.

## Core Runtime Flows

### Wave And Spawn Flow

```text
GameManager current wave
        |
        v
ZombieSpawner -> ZombieWaveSpawnProfileSO
        |          |- wave-range stage
        |          |- weighted normal entries
        |          |- boss schedules
        |          `- stat/reward multipliers
        v
MemoryPool -> spawned zombie -> death completion
        ^                              |
        |                              v
next wave <- OnWaveIncrease <- kill target reached
```

The wave profile owns wave-specific composition and scaling. Normal and boss specs own baseline values and per-instance variance. Do not restore the retired `ZombieSpawnData` ownership split.

Beyond the last finite stage, runtime fallback retains the last composition and timing while applying configured post-final HP and attack increments.

### Combat And Damage Flow

```text
Turret stat calculation
        v
Projectile / beam / status payload
        v
target IDamageable.TakeDamage(DamageInfo)
        |
        +-> target clamps HP and resolves death
        +-> actual HP loss -> turret damage meter
        +-> popup policy -> accumulation/throttle/backend
        +-> death -> reward, kill progress, effects, pool return
```

`IDamageable` is the mutation boundary. External systems read `IsAlive` and call `TakeDamage`; they do not write target HP.

Projectile hit detection combines tracked-target validation, trigger/collision callbacks, and movement raycast correction. It rejects null, dead, duplicate, and already-hit targets. Beams use their own tick cadence and non-alloc target buffers.

Delayed status damage retains its originating `TurretDamageMeterSource`. Receivers report actual HP loss, excluding overkill, so Poison, Frost, Electro, and Ignition remain attributable to the originating turret.

### Status Effect Flow

```text
Turret definition
  -> status profile configuration
  -> projectile/beam payload with source
  -> receiver interface on target
  -> per-target status runtime
       timers, stacks, cooldown, VFX, delayed damage
  -> reset on death/disable/pool reuse
```

Profiles define authored values; per-target runtimes own mutable exposure, stacks, timers, VFX handles, pending damage, and source attribution. Boss targets can apply restricted policies, such as Frost slow without normal-zombie freeze.

### Defense-Line And Obstacle Flow

Defense lines are ordered slot groups owned by `GameManager`, not direct obstacle lists.

```text
Obstacle reaches zero HP
  -> fracture once
  -> slot stores definition and level, then clears occupancy
  -> GameManager marks the line breached
  -> survivors retreat and release invalid repair work
  -> player rebuilds from the rear contiguous restored block
  -> all slots on a line become occupied
  -> line restored and survivors may return
```

Placement is constrained by the front-most line of the contiguous restored block measured from the rear. A player cannot skip a breached inner line and rebuild farther forward.

Obstacle progression is slot-centered. The slot remembers the destroyed obstacle's stable identity and level. Rebuilding the same definition can inherit the level, level-specific prefab, and configured rebuild discount; changing definition starts from level 1.

Spending uses the shared multi-currency `ResourceCost[]` contract. A failed instantiation after payment must refund the same costs.

### Survivor Flow

`Survivor` is a state-driven actor coordinated by `GameManager` policies. Current roles are survivor, construction worker, and engineer.

Important transitions include:

```text
rescue -> treatment -> role selection -> role-ready state
construction idle -> reserve repair target -> move -> repair -> release
line breach -> retreat -> wait -> restored line -> return
engineer ready -> reserve turret seat -> move -> assigned
blocked defense movement -> vault -> resume prior movement
```

Only construction workers request repair targets. `GameManager` selects valid damaged obstacles; obstacle reservations prevent duplicate worker assignment. Disable, death, retreat, target loss, and wave restart must release reservations and assignments.

### Turret Placement, Upgrade, And Evolution

```text
TurretShopEntrySO + placement costs
        v
TurretPlacementController -> TurretBaseSlot
        v
TurretDefinitionRuntimeController.Apply
        |
        +-> calculate and apply stats
        +-> select projectile/beam and scale
        +-> apply status, damage-polish and audio profiles
        +-> register damage-meter source
        `-> expose upgrade/evolution candidates to UI
```

Tier level drives current-form stats and evolution requirements. Total level is lifetime/display progression. Evolution changes the definition, resets tier level to 1, preserves total level, and reapplies all derived runtime configuration.

Placement, upgrade, and evolution all consume the same item-based cost contract through `InventorySystem`; UI is not the source of truth for affordability or mutation.

### Reward, Inventory, Craft, And Transaction Flow

```text
Zombie death
  -> reward profile + runtime context/modifiers
  -> RewardGrantUtility
  -> InventorySystem.AddReward / AddItem
  -> inventory change event
  -> inventory, wallet and related UI refresh

Gameplay purchase/upgrade
  -> CanAfford(ResourceCost[])
  -> TrySpend(ResourceCost[])
  -> perform operation
  -> Refund(ResourceCost[]) if post-spend creation fails
```

`InventorySystem` is the quantity authority. Feature controllers must not directly mutate UI counters or maintain parallel currency balances.

`InventoryUI` presents inventory, crafting, and decomposition. `TransactionUI` presents buying and selling, including held-button repeat behavior. These UI components call inventory APIs; they do not own item quantities.

Item metadata defines craft outputs/inputs, decomposition ranges, display data, and transaction values. `ItemData.csv` is a bulk-authoring source used by editor tooling, while runtime consumes the generated/assigned Unity data.

### Feedback And Audio Flow

Damage popup requests carry a popup type and policy. High-frequency damage can accumulate by target instance ID over a short window, while important direct critical/heavy hits can display immediately. Global and per-target limits keep late-wave feedback bounded.

Audio callers emit semantic events through `AudioCueSO` or turret audio events. `ProjectAudioManager` applies bus volume, cooldown, simultaneous limits, total/bus voice caps, and priority stealing. Damage, DoT, beam, and chain ticks must not create one unbounded sound per tick.

### Pooling Flow

```text
request prefab
  -> MemoryPool finds prefab stack
  -> pop or create PoolObject
  -> position and payload setup
  -> reset/init mutable state
  -> activate and OnSpawn
  -> feature completes/cancels
  -> OnDespawn and ReturnToPool
```

Projectiles, zombies, repeated effects, warning popups, damage popups, and audio sources use pooling or feature-specific reusable instances. Every pooled feature remains responsible for clearing its own target references, timers, event subscriptions, physics state, particles, trails, and pending payloads.

## Shared Contracts

| Contract | Purpose | Main consumers |
| --- | --- | --- |
| `IDamageable` | Unified HP/death mutation boundary | Zombies, obstacles, projectiles, beams, skills |
| `ITargetable` | Explicit live target point | Target finders and attack systems |
| `IProjectileHitHandler` | Optional hit hook beyond direct damage | Projectile collision adapters |
| Status receiver interfaces | Deliver typed Frost, Poison, Electro, or Ignition payloads | Projectiles/beams and zombie runtimes |
| `DamageInfo` | Damage amount, popup type/policy, attribution source | Attackers, receivers, feedback, meter |
| `ResourceCost[]` | Shared multi-item affordability/spend/refund representation | Placement, upgrade, evolution, rebuilding |
| `PoolObject` lifecycle | Reusable object origin and spawn/despawn hooks | Memory pool and pooled gameplay objects |

When adding a cross-system behavior, extend an existing contract or add a small project-level adapter. Do not make one feature reach into another feature's private mutable fields.

## Ownership Boundaries

### GameManager Owns

- current wave and kill progress;
- defense-line registration, breach, restore, and placement-order policy;
- survivor and obstacle registration;
- shared repair-target selection;
- coordinated wave failure/restart state.

It does not own actor movement, HP mutation, turret stat calculation, inventory balances, or presentation.

### InventorySystem Owns

- item metadata lookup and owned quantities;
- add, use, affordability, spend, and refund operations;
- inventory quantity change notification.

It does not decide turret evolution requirements, obstacle level inheritance, reward probabilities, or UI layout.

### Feature Runtime Controllers Own

- their actor's mutable state and validation;
- conversion from authored profiles to cached runtime values;
- cleanup for disable, destruction, cancellation, and pool reuse;
- emitting events or payloads at meaningful gameplay timing.

They should not directly create parallel global managers or duplicate shared item, damage, pooling, popup, or audio policy.

### UI Owns

- player input presentation;
- selection state needed only for the open UI;
- reading authoritative runtime state and invoking public feature APIs;
- restoring popup/navigation context where documented.

UI must not become the authoritative owner of currency, level, HP, wave, or placement validity.

## External Asset Boundary

`Assets/__PROJECT__/Private Assets` contains external or purchased sources and may be a separate repository. Prefer:

1. project-level interfaces and adapters;
2. ScriptableObject profiles;
3. duplicated project-owned prefabs;
4. small generic events exposed by the external component only when unavoidable.

Do not directly edit external originals for project-specific damage, economy, audio, or status rules. Check the nested repository status separately when Private Assets must change.

## Performance Architecture

The target includes portrait mobile and long idle-defense sessions with many actors. The architecture therefore assumes bounded repeated work:

- pool frequently spawned actors, projectiles, effects, popups, and audio sources;
- prewarm expected concurrency where first-use spikes are visible;
- use non-alloc physics buffers for repeated targeting and AoE checks;
- cache components, animator hashes, target anchors, profile-derived values, and reusable lists;
- use events or throttled polling instead of repeated object searches;
- merge or throttle high-frequency popup and audio feedback;
- use actual target range and boundary colliders rather than short projectile lifetime as normal range control;
- keep Editor reporting and CSV processing out of runtime assemblies and hot paths.

See `TEAM_CODING_CONVENTION.md` and `COMMON_SYSTEMS.md` for mandatory implementation rules.

## Failure And Reset Boundaries

All stateful systems must define cleanup for these transitions:

| Transition | Required cleanup |
| --- | --- |
| Actor death | Mark non-targetable before delayed presentation; stop combat; resolve reward once |
| Disable/destruction | Unregister, unsubscribe, release reservations and handles |
| Pool return | Reset HP/death flags, targets, timers, payloads, physics, VFX and subscriptions |
| Defense breach | Clear invalid repair work and start survivor retreat |
| Wave failure/restart | Clear spawned-wave state, reset kill targets, coordinate survivor/engineer interaction state |
| Placement failure after spend | Refund the exact `ResourceCost[]` charged |
| Missing manager/profile/prefab | Safe skip or disable with actionable one-shot Korean warning |

## Source-Of-Truth Matrix

| Question | Source of truth |
| --- | --- |
| What spawns in a wave? | `ZombieWaveSpawnProfileSO` and its boss schedules |
| What are a zombie's baseline stats? | `NormalZombieSpec` or `BossZombieSpec` |
| Is a defense line breached/restored? | `GameManager.DefenseLineEntry` state |
| What occupies a defense slot and what level was destroyed? | `ObstacleBuildSlot` |
| What does a survivor do now? | `Survivor` runtime state |
| How strong is a turret at this tier? | Definition/profile data calculated and applied by `TurretDefinitionRuntimeController` |
| How many items/currencies does the player own? | `InventorySystem` |
| Can a placement or upgrade be afforded? | Feature cost rules plus `InventorySystem.CanAfford/TrySpend` |
| How much damage did a turret actually deal? | Receiver-reported HP loss accumulated by `TurretDamageMeterManager` |
| How should damage feedback display? | `DamagePopupPolicyResolver` and `DamagePopupSpawner` settings |
| How is a repeated object reused? | `MemoryPool` plus the object's feature-specific reset lifecycle |

## Documentation Routing

| Work area | Continue with |
| --- | --- |
| Repository folders and asset placement | `PROJECT_STRUCTURE.md` |
| Overall game intent and team responsibility | `PROJECT_OVERVIEW.md` |
| Wave, zombie, obstacle, survivor and defense line | `GAMEPLAY_RUNTIME_FLOW.md` |
| Turret, projectile, status payload and turret UI | `TURRET_SYSTEM.md`, `TURRET_UI_REWORK_STATUS.md` |
| Currency, drops and reward calculation | `REWARD_SYSTEM.md` |
| Pooling, damage popup and shared runtime utilities | `COMMON_SYSTEMS.md`, `DAMAGE_POPUP_DNP_MIGRATION.md` |
| Audio buses, cues, loops and turret audio | `AUDIO_SYSTEM.md` |
| Scene/prefab/NavMesh/layer setup | `SCENE_SETUP.md` |
| Style, comments, logging and mobile performance | `TEAM_CODING_CONVENTION.md` |

## Maintenance Rule

Update this document when a change affects any of the following:

- global service ownership;
- a shared cross-system contract;
- startup or reset order;
- a primary runtime data flow;
- the authoritative owner of mutable state or configuration;
- a new major runtime or editor subsystem.

Feature-specific numeric values and Inspector setup belong in the relevant feature document, not here.
