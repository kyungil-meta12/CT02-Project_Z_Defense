# Reward And Currency System

## Purpose

This document tracks the planned reward and currency pipeline for zombie kill rewards, turret upgrade costs, turret evolution costs, placement costs, and future economy systems.

## Design Goal

- Keep reward and cost data in ScriptableObjects.
- Keep runtime mutable currency amounts in `ItemManager` or a future wallet service.
- Keep UI from directly changing currency.
- Keep zombie, turret, and placement classes from owning reward or cost formulas.
- Support future coin, fire part, special part, event currency, wave scaling, line scaling, and conditional rewards without rewriting prefab logic.

## Current State

- `ItemManager` owns coin, fire part, and special part counts.
- `ItemManager` can apply initial wallet currencies from inspector-configured `ResourceCost[] initialWalletCurrencies`.
- `ItemManager` now exposes explicit reward, spend, afford, and refund APIs.
- `ItemManager.AddCoinCount`, `CanUseCoin`, and `TryUseCoin` remain as compatibility wrappers.
- `ItemManager.AddReward` can optionally update wave-collected coin tracking.
- `ItemManager.Refund` does not update wave-collected reward tracking.
- `NormalZombie` grants kill reward through its prefab-level `ZombieRewardProfileSO`.
- `NormalZombieSpec` owns normal zombie combat stats only and does not own kill reward data.
- `NormalZombie.Die` grants kill reward through `RewardGrantUtility`.
- `NormalZombie.OnDespawn` no longer grants kill reward.
- `BossZombie` can override kill reward with a prefab-level `ZombieRewardProfileSO`.
- `BossZombieSpec` references a default fallback `ZombieRewardProfileSO` and no longer owns legacy item drop percentage fields.
- `BossZombie.Die` grants kill reward through `RewardGrantUtility`.
- `ZombieRewardProfileSO` supports conditional modifiers for wave range, zombie type, defense line, situation flags, currency type, amount multiplier, flat bonus, and drop chance changes.
- `ZombieWaveSpawnProfileSO` can apply a runtime reward multiplier to zombies spawned in a wave stage before their kill reward is granted.
- `TurretShopEntrySO` is a legacy type name for turret placement entry data and defines turret placement costs through `ResourceCost[] placementCosts`.
- `TurretShopEntrySO` supports `placementCostTiers` so Sentinel-01 placement can become more expensive by successful placement count.
- `TurretBaseSlot` spends turret placement costs through `ItemManager.TrySpend`.
- `ObstacleBuildSlot` still spends obstacle placement cost through `ItemManager.TryUseCoin`.
- `TurretDefinitionSO` can reference `TurretUpgradeCostProfileSO`.
- `TurretEvolutionEntry` can define `ResourceCost[] evolutionCosts`.
- Turret runtime UI calls `TryUpgrade`, `TryEvolve`, or `TryCreateEvolvedInstance`, so upgrades/evolutions only execute after cost spend succeeds.
- Turret upgrade costs currently use form-based base Coin values plus additional cost percentage per tier level, while turret evolution costs use fixed branch-entry Coin gates.

## Target Zombie Reward Flow

1. `NormalZombie.TakeDamage` detects HP reaching zero.
2. `NormalZombie.Die` transitions the zombie to dead state.
3. `NormalZombie.Die` requests reward grant from prefab override reward profile, while `BossZombie.Die` requests prefab override first and then `spec.rewardProfile`.
4. `ZombieRewardContext` supplies runtime conditions such as wave, line, boss flag, and event multiplier.
5. `ZombieSpawner` may add a wave-stage reward multiplier through `ZombieWaveSpawnProfileSO`.
6. `RewardGrantUtility` applies matching profile modifiers, calculates the final rewards, and calls `ItemManager`.
7. `NormalZombie.OnDespawn` only handles pool cleanup and never grants kill reward.

## Target Turret Cost Flow

1. Turret UI requests upgrade or evolution.
2. `TurretDefinitionRuntimeController` checks required level, max level, cost, and extra requirements.
3. `TurretUpgradeCostProfileSO` calculates upgrade costs.
4. `TurretEvolutionEntry` supplies evolution costs.
5. `ItemManager` spends currency only after all requirements pass.
6. Runtime level-up or evolution executes only after spend succeeds.

Current turret economy first pass:

- Sentinel-01: 233 base Coin per level-up, 1% additional cost per tier level.
- Sentry Pulse / Vector MG: 350 base Coin per level-up, 2% additional cost per tier level.
- Pulse Repeater / Vulcan Node: 640 base Coin per level-up, 3% additional cost per tier level.
- Second-generation `_1`: 3,200 base Coin per level-up, 3% additional cost per tier level.
- Second-generation `_2`: 5,667 base Coin per level-up, 4% additional cost per tier level.
- Second-generation `_3`: 10,571 base Coin per level-up, 5% additional cost per tier level.
- Evolution gates: 20,000 -> 60,000 -> 180,000 -> 300,000 -> 450,000 Coin by progression depth.
- One full path to a second-generation `_3` tier level 100 costs about 7.4M Coin before placement costs.

## Target Turret Placement Cost Flow

1. `TurretPlacementUI` builds slots from placement entries currently typed as `TurretShopEntrySO`.
2. `TurretPlacementSlotUI` displays `placementCosts`.
3. `TurretBaseSlot.TryPlace` checks the selected slot and prefab.
4. `TurretPlacementController` calculates the current placement cost from the successful placement count for that entry.
5. `TurretBaseSlot.TryPlace` spends the calculated placement costs through `ItemManager.TrySpend`.
6. Turret prefab instantiation runs only after placement cost spend succeeds.
7. If placement instantiation fails after spend, the placement costs are refunded.
8. Successful placement increments the entry placement count and refreshes the next UI cost.

## Planned Data Types

| Type | Responsibility |
| --- | --- |
| `RewardCurrencyType` | Shared currency enum such as `Coin`, `FirePart`, `SpecialPart`. |
| `RewardEntry` | Reward currency, amount, chance, and per-entry random amount multiplier range. |
| `ZombieRewardModifier` | Conditional reward adjustment by wave range, zombie type, defense line, situation flag, and currency. |
| `ZombieRewardSituation` | Runtime situation flags such as event bonus, fever time, perfect defense, or custom triggers. |
| `ResourceCost` | Cost currency and amount for upgrades, evolution, placement, shop, and skills. |
| `ZombieRewardProfileSO` | Zombie kill reward data referenced by prefab overrides and boss spec fallback data. |
| `ZombieRewardContext` | Runtime-only reward modifiers such as wave, line, boss, and event multiplier. |
| `TurretUpgradeCostProfileSO` | Calculates turret upgrade cost by current and target tier level. |
| `RewardGrantUtility` | Converts profile + context into concrete currency grants. |

## Placement Of References

Use this ownership:

- `NormalZombie` references `NormalZombieSpec`.
- `NormalZombie.rewardProfileOverride` owns normal zombie kill reward data per prefab or Variant.
- `NormalZombieSpec` owns normal zombie combat stats only.
- Zombie prefab Variants should override `rewardProfileOverride` when rewards differ.
- `BossZombie` follows the same override-first rule through `BossZombie.rewardProfileOverride`.
- `BossZombieSpec` references a default fallback `ZombieRewardProfileSO` and does not own item drop percentages.
- Round and situation scaling should be added to `ZombieRewardProfileSO.Modifiers`, not to prefab scripts.
- `TurretDefinitionSO` may reference an upgrade cost profile.
- `TurretEvolutionEntry` may hold evolution costs because the cost belongs to a specific branch choice.
- `TurretShopEntrySO` holds placement costs because the cost belongs to the placement entry being installed.

## Reward Modifier Rules

Each `ZombieRewardProfileSO` has:

- `Rewards`: base reward entries.
- `Modifiers`: optional conditional adjustments.

Each `RewardEntry` has:

- `currencyType`: reward currency.
- `amount`: base amount before runtime multipliers.
- `dropChance`: chance to grant this reward entry.
- `minAmountMultiplier` and `maxAmountMultiplier`: final random amount range.

Coin reward entries currently use `0.8~1.2` so repeated kills do not always grant the exact same coin amount. Non-coin rewards should usually stay at `1.0~1.0` unless their quantity should intentionally vary.

Modifier conditions:

- `targetFilter`: all currencies or one specific currency.
- `zombieTypeFilter`: any, normal only, or boss only.
- `minWave` and `maxWave`: wave range; `maxWave = 0` means no upper limit.
- `defenseLineIndex`: `-1` means any line.
- `requiredSituations`: all selected situation flags must be present in `ZombieRewardContext`.

Modifier effects:

- `amountMultiplier`: multiplies reward amount.
- `flatAmountBonus`: adds a flat amount before multiplication.
- `dropChanceMultiplier`: multiplies drop chance.
- `additionalDropChance`: adds or subtracts drop chance after multiplication.

Amount calculation order:

1. Start from `RewardEntry.amount`.
2. Apply matching modifier flat bonuses.
3. Apply `ZombieWaveSpawnProfileSO` reward multiplier through `ZombieRewardContext.rewardMultiplier`.
4. Apply matching modifier amount multipliers.
5. Apply `RewardEntry` random amount multiplier.
6. Floor to the final integer amount.

Example:

- Wave 10+ coin reward x1.5: `targetCurrency = Coin`, `minWave = 10`, `maxWave = 0`, `amountMultiplier = 1.5`.
- Fever item drop chance +20%: `requiredSituations = FeverTime`, `additionalDropChance = 0.2`.
- Boss-only special part x2: `zombieTypeFilter = BossOnly`, `targetCurrency = SpecialPart`, `amountMultiplier = 2`.

## ItemManager Migration

Keep old APIs temporarily:

- `AddCoinCount`
- `CanUseCoin`
- `TryUseCoin`

Add explicit APIs:

- `AddReward`
- `CanAfford`
- `TrySpend`
- `Refund`

Important rule:

- Reward grants can update wave-collected reward tracking.
- Refunds must not update wave-collected reward tracking.
- Initial wallet grants must not update wave-collected reward tracking.

## Implementation Order

1. Done: Add shared reward/cost value types.
2. Done: Add `ZombieRewardProfileSO`.
3. Done: Add prefab-level reward profile support to `NormalZombie`.
4. Done: Move zombie reward grant from `OnDespawn` to `Die`.
5. Done: Split `ItemManager` reward, spend, afford, and refund APIs.
6. Done: Add prefab-level reward profile override to `NormalZombie`.
7. Done: Add prefab-level reward profile override to `BossZombie`.
8. Done: Add conditional reward modifiers to `ZombieRewardProfileSO`.
9. Done: Add `TurretUpgradeCostProfileSO`.
10. Done: Add evolution costs to `TurretEvolutionEntry`.
11. Done: Replace turret UI direct level/evolution calls with `TryUpgrade` and `TryEvolve`.
12. Done: Move turret placement/shop entry cost to `ResourceCost[] placementCosts`.
13. Done: Add placement count based cost tiers for turret placement entries.
14. Done: Remove turret placement legacy `cost` fallback from runtime code.
15. Next: Move obstacle placement cost to `ResourceCost`.
16. Done: Remove normal zombie spec reward fallback fields after active reward profiles were verified.
17. Next: Remove `ItemManager` compatibility wrappers after old call sites reach zero.

## Edge Cases

- Pooled objects must not grant reward on prewarm or non-death return.
- Duplicate death calls must not grant duplicate rewards.
- Missing reward profile must not block zombie death, but should result in no reward grant.
- Missing `ItemManager` should log once with an actionable Korean message.
- Zero or negative rewards/costs should be ignored or clamped.
- Drop chances should be clamped between `0` and `1`.
- Upgrade hold input should stop when currency becomes insufficient.
- Evolution prefab replacement must not consume cost twice.
- Turret placement should not instantiate a turret when placement cost spend fails.
- Turret placement should refund costs if prefab instantiation fails after spend.
- Turret placement count should increase only after successful prefab installation.

## Next Cleanup Plan

1. Move obstacle placement cost to `ResourceCost`.
2. Remove `ItemManager.AddCoinCount`, `CanUseCoin`, and `TryUseCoin` after call sites reach zero.

## Tomorrow Handoff

Start with obstacle placement cost migration.

Read before editing:

- `Assets/__PROJECT__/Docs/README.md`
- `Assets/__PROJECT__/Docs/TEAM_CODING_CONVENTION.md`
- `Assets/__PROJECT__/Docs/PROJECT_STRUCTURE.md`
- `Assets/__PROJECT__/Docs/GAMEPLAY_RUNTIME_FLOW.md`
- `Assets/__PROJECT__/Docs/REWARD_SYSTEM.md`

Main files:

- `Assets/__PROJECT__/Prefabs/Damageable/Obstacle/ObstacleBuildEntrySO.cs`
- `Assets/__PROJECT__/Prefabs/Damageable/Obstacle/ObstacleBuildSlot.cs`
- `Assets/__PROJECT__/Prefabs/Damageable/Obstacle/ObstaclePlacementSlotUI.cs`
- `Assets/__PROJECT__/Scripts/UI/Singleton/ItemManager/ItemManager.cs`

Goal:

- Add `ResourceCost[] buildCosts` or equivalent to `ObstacleBuildEntrySO`.
- Spend obstacle build costs through `ItemManager.TrySpend`.
- Refund through `ItemManager.Refund`, not `AddCoinCount`.
- Update obstacle placement UI to display `ResourceCost[]`.
- Keep old `cost` only as a temporary asset fallback if YAML migration is not done in the same pass.

## Related Plan File

- `Assets/__PROJECT__/REWARD_CURRENCY_SYSTEM_PLAN.cs`
