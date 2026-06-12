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
- `ItemManager.AddCoinCount` adds to both total coin and wave-collected coin.
- `ItemManager.TryUseCoin` spends coin directly.
- `NormalZombieSpec.DropCoin` defines a fixed coin amount.
- `NormalZombie.OnDespawn` currently grants `spec.DropCoin`.
- `ObstacleBuildSlot` currently spends placement cost through `ItemManager.TryUseCoin`.
- Turret upgrade and evolution flows currently do not spend currency.

## Target Zombie Reward Flow

1. `NormalZombie.TakeDamage` detects HP reaching zero.
2. `NormalZombie.Die` transitions the zombie to dead state.
3. `NormalZombie.Die` requests reward grant from `spec.rewardProfile`.
4. `ZombieRewardContext` supplies runtime conditions such as wave, line, boss flag, and event multiplier.
5. `RewardGrantUtility` calculates the final rewards and calls `ItemManager`.
6. `NormalZombie.OnDespawn` only handles pool cleanup and never grants kill reward.

## Target Turret Cost Flow

1. Turret UI requests upgrade or evolution.
2. `TurretDefinitionRuntimeController` checks required level, max level, cost, and extra requirements.
3. `TurretUpgradeCostProfileSO` calculates upgrade costs.
4. `TurretEvolutionEntry` supplies evolution costs.
5. `ItemManager` spends currency only after all requirements pass.
6. Runtime level-up or evolution executes only after spend succeeds.

## Planned Data Types

| Type | Responsibility |
| --- | --- |
| `RewardCurrencyType` | Shared currency enum such as `Coin`, `FirePart`, `SpecialPart`. |
| `RewardEntry` | Reward currency, amount, chance, and future random amount options. |
| `ResourceCost` | Cost currency and amount for upgrades, evolution, placement, shop, and skills. |
| `ZombieRewardProfileSO` | Base zombie kill rewards referenced by zombie spec data. |
| `ZombieRewardContext` | Runtime-only reward modifiers such as wave, line, boss, and event multiplier. |
| `TurretUpgradeCostProfileSO` | Calculates turret upgrade cost by current and target tier level. |
| `RewardGrantUtility` | Converts profile + context into concrete currency grants. |

## Placement Of References

Use this ownership:

- `NormalZombie` references `NormalZombieSpec`.
- `NormalZombieSpec` references `ZombieRewardProfileSO`.
- Zombie prefab Variants should not each own reward data unless a specific Variant intentionally needs a different spec.
- `TurretDefinitionSO` may reference an upgrade cost profile.
- `TurretEvolutionEntry` may hold evolution costs because the cost belongs to a specific branch choice.

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

## Implementation Order

1. Add shared reward/cost value types.
2. Add `ZombieRewardProfileSO`.
3. Add `rewardProfile` to `NormalZombieSpec`.
4. Move zombie reward grant from `OnDespawn` to `Die`.
5. Split `ItemManager` reward, spend, and refund APIs.
6. Add turret upgrade cost profile.
7. Add evolution costs.
8. Replace turret UI direct level/evolution calls with `TryUpgrade` and `TryEvolve`.
9. Move obstacle placement cost to `ResourceCost` after turret economy is stable.

## Edge Cases

- Pooled objects must not grant reward on prewarm or non-death return.
- Duplicate death calls must not grant duplicate rewards.
- Missing reward profile must not block zombie death.
- Missing `ItemManager` should log once with an actionable Korean message.
- Zero or negative rewards/costs should be ignored or clamped.
- Drop chances should be clamped between `0` and `1`.
- Upgrade hold input should stop when currency becomes insufficient.
- Evolution prefab replacement must not consume cost twice.

## Related Plan File

- `Assets/__PROJECT__/REWARD_CURRENCY_SYSTEM_PLAN.cs`
