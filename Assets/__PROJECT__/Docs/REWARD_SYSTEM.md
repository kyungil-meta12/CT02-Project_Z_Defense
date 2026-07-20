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
- `ItemManager` exposes explicit reward, spend, afford, and refund APIs.
- Legacy Coin-only compatibility APIs have been removed.
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
- `ObstacleBuildEntrySO` exposes obstacle and gate placement costs through `ResourceCost[] buildCosts`.
- `ObstacleBuildSlot` spends obstacle and gate placement costs through `ItemManager.TrySpend` and refunds through `ItemManager.Refund`.
- Obstacle placement no longer has an integer Coin fallback path; `ResourceCost[] buildCosts` is the only placement cost source.
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
- Evolution gates: 10,000 -> 60,000 -> 180,000 -> 300,000 -> 450,000 Coin by progression depth.
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

Wave reward growth is split between prefab reward profiles and `ZombieWaveSpawnProfileSO.rewardMultiplier`. Prefab profiles define the base value for each zombie type, while the wave profile scales the value for the active stage. Expected wave income should be reviewed as weighted average reward per spawned zombie multiplied by the stage spawn count.

`ZombieSpawnRuntimeModifiers.Sanitized()` treats zero or negative runtime multipliers as `1.0` at spawn time. If an early stage asset shows `rewardMultiplier = 0`, runtime reward calculation still behaves as `1.0` unless that sanitization rule changes.

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

For current coin-only kill rewards, the practical check is `floor(baseCoin * stageRewardMultiplier * Random.Range(0.8, 1.2))`. Use the weighted spawn entries when estimating a full wave instead of averaging only by role names, because multiple prefabs in the same role can carry different entry weights.

Example:

- Wave 10+ coin reward x1.5: `targetCurrency = Coin`, `minWave = 10`, `maxWave = 0`, `amountMultiplier = 1.5`.
- Fever item drop chance +20%: `requiredSituations = FeverTime`, `additionalDropChance = 0.2`.
- Boss-only special part x2: `zombieTypeFilter = BossOnly`, `targetCurrency = SpecialPart`, `amountMultiplier = 2`.

## ItemManager APIs

Use explicit APIs:

- `AddReward`
- `CanAfford`
- `TrySpend`
- `Refund`

Important rule:

- Reward grants can update wave-collected reward tracking.
- Refunds must not update wave-collected reward tracking.
- Initial wallet grants must not update wave-collected reward tracking.
- Cost checks should use `CanAfford` and confirmed spending should use `TrySpend`, even for Coin-only costs, so future multi-currency costs do not need new gameplay code.
- Legacy Coin-only wrappers were removed after call sites reached zero to prevent new obstacle, turret, or UI code from bypassing the shared currency pipeline.

## Obstacle Placement Cost Migration Rationale

Obstacle rebuild costs used to be represented as a single Coin value on `ObstacleBuildEntrySO`. That was enough for the first rebuild flow, but it diverged from the turret economy path after turret placement, upgrades, and evolution moved to `ResourceCost[]`.

The current obstacle placement path intentionally mirrors turret placement:

1. `ObstacleBuildEntrySO.buildCosts` owns the serialized cost data.
2. `ObstaclePlacementSlotUI` displays the same `ResourceCost[]` data.
3. `ObstacleBuildSlot.CanPlaceEntry` performs a quiet affordance check for placement preview.
4. `ObstacleBuildSlot.TryPlace` spends with `ItemManager.TrySpend` only when placement is confirmed.
5. If prefab validation fails after spending, `ObstacleBuildSlot.TryPlace` refunds with `ItemManager.Refund`.

The old `int cost`, `Cost`, `AddCoinCount`, `CanUseCoin`, and `TryUseCoin` paths should stay removed. Keeping a fallback would make it unclear whether a successful rebuild came from the new data or the old Coin field, which makes economy validation and team review unreliable.

Debugging follows the same boundary: preview checks stay silent to avoid per-frame log spam and GC allocation, while confirmed placement attempts log concrete Korean failure reasons such as occupied slot, insufficient currency, missing prefab, or missing `ItemManager`.

## 500웨이브 비코인 드랍 밸런싱

### 목적과 수정 범위

이 절은 터렛/장애물 비용 재화가 추가되거나 제거될 때 500웨이브까지 필요한 `ZombieRewardProfiles.csv`의 비코인 `DropChance`를 다시 계산하는 기준이다. 런타임 코드와 보상 수량은 변경하지 않으며, CSV의 기존 `(ZombieType, CurrencyType)` 행만 조정한다. CSV를 수정한 뒤 보상 SO 임포트는 별도로 실행해야 한다.

### 입력 데이터와 2026-07-20 기준 스냅샷

- 웨이브 구성: `ZombieWaveSpawnProfile.csv`, `ZombieBossSpawnSchedule.csv`
- 보상 수량/확률: `ZombieRewardProfiles.csv`
- 터렛 비용: `TurretData.csv`, `TurretEvolutionCosts.csv`, 연결된 업그레이드 비용 SO와 배치 비용 SO
- 장애물 비용: `ObstacleBuildEntrySO`, `ObstacleDefinitionSO`, `ObstacleUpgradeCostProfileSO`
- 제작/분해 관계: `ItemData.csv`의 `Createable`, `CountPerCraft`, `ItemsToCreate`, `Decomposable`, `ItemsFromDecompose`
- 초기 Coin: Main 씬 `InventorySystem.initialWalletCurrencies`의 Coin 1,500
- 웨이브 클리어 Coin 보너스: Main 씬 `waveClearCoinBonusPercentage` 20%
- 1~500웨이브 처치 기대 Coin: 101,953,308.074
- 초기 지갑과 클리어 보너스를 포함한 사용 가능 Coin: 122,345,469.689

### 누적 보상 계산식

웨이브 `w`에서 좀비 종류 `z`의 기대 처치 수는 일반 좀비의 경우 `SpawnCount × z의 유효 가중치 / 전체 유효 가중치`, 보스의 경우 해당 웨이브에 발동한 보스 스케줄 수다. Coin은 기존 런타임 규칙대로 스테이지 보상 배율을 적용하지만, 비코인 아이템 드랍 목표에는 스테이지 보상 배율을 적용하지 않는다.

```text
KillCount500(z) = Σ(w=1..500) ExpectedKillCount(w, z)
CoinExposure(z) = Σ(w=1..500) ExpectedKillCount(w, z) × StageRewardMultiplier(w)
ExpectedDrop(item) = Σz KillCount500(z) × Amount(z, item)
                     × AverageAmountMultiplier(z, item) × DropChance(item)
KillCoin = Σz CoinExposure(z) × CoinAmount(z)
AvailableCoin = InitialWalletCoin
              + KillCoin
              + KillCoin × WaveClearCoinBonusPercentage / 100
```

현재 모든 보상 행의 수량 배율은 `0.8~1.2`라 평균 배율은 1이다. 향후 modifier가 추가되면 밸런스 리포트의 `ZombieRewardExpectationCalculator`와 동일하게 flat bonus, amount multiplier, drop chance multiplier/addition 순서까지 적용한다.

### 최대 강화 필요량 계산

- 500웨이브 누적 Coin은 참고값으로만 기록하고 비코인 필요량의 강화 상한으로 사용하지 않는다.
- 터렛은 최대 배치 수 8개가 각각 최종 진화와 최종 레벨에 도달한다고 가정한다. 8개분의 배치비, 모든 중간 폼의 요구 레벨 업그레이드비, 선택한 진화비, 최종 폼 업그레이드비를 누적한다. 재화별 기준량은 해당 재화를 가장 많이 요구하는 최종 진화 경로를 터렛 8개가 동일하게 진행하는 보수적 시나리오다.
- 장애물은 게이트 1개와 장애물 9개가 Coin 제한 없이 각각 MaxLevel 5000에 도달한다고 가정한다.
- 2026-07-20 기준 장애물 완전 강화에는 Concrete 2,698,990이 필요하다. 게이트 업그레이드가 382,399, 장애물 9개 업그레이드가 2,316,591이다.
- 재화별 기준 필요량은 `max(TurretNeed, ObstacleNeed)`다. 제작 가능한 재화는 `ceil(필요량 / CountPerCraft) × 입력 재료 수량`으로 재귀 전개하고 완제품은 다시 합산하지 않는다.
- 제작 전개가 보상 CSV에 직접 없는 원재료에 도달하면 `ItemsFromDecompose`를 역방향으로 탐색한다. 분해 수량 범위는 `(Min + Max) / 2`의 기대값을 사용하며, 여러 기존 드랍 아이템이 같은 재료를 만들 수 있으면 필요한 재료량을 공급원 수로 균등 배분한다.
- 하나의 분해 원본이 여러 필요 재료를 동시에 만드는 경우 요구 원본 수량을 합산하지 않고 재료별 요구량 중 최대값을 사용한다. 분해가 다시 분해 가능한 중간 아이템을 만들면 기존 드랍 아이템까지 재귀 추적한다.
- 최종 목표는 필요량이 있으면 `ceil(기준 필요량 × 1.5)`, 없으면 500웨이브 기대 드랍 100개다.

### 드랍 확률 계산

아이템별 기존 보상 행 범위는 유지하고, 같은 아이템을 가진 모든 좀비 보상 행에 동일한 확률을 적용한다.

```text
p(item) = clamp(TargetAmount(item)
          / Σz(KillCount500(z) × Amount(z, item) × AverageAmountMultiplier(z, item)), 0, 1)
```

계산된 `p(item)`을 해당 아이템의 모든 기존 좀비 행에 그대로 기록한다. 확률이 1이어도 목표가 남으면 보상 수량 변경이나 신규 행 추가 없이는 달성할 수 없으므로 미달 상태를 기록한다. 500웨이브의 일반 좀비 기대 처치 수는 28,355, 보스 기대 처치 수는 180이다.

### 2026-07-20 적용 결과 (500웨이브)

| 아이템 | 터렛/장애물 및 제작 전개 기준량 | 목표량 | 조정 전 기대량 | 조정 후 기대량 | 적용 확률 범위 |
| --- | ---: | ---: | ---: | ---: | ---: |
| Stone | 134,949,500 | 202,424,250 | 500웨이브 재계산 전 기대량 | 567,100 | 1 |
| VodkaBottle / WineBottle / Drink | Water 공급원 각 1,199,551 | 각 1,799,327 | 500웨이브 재계산 전 기대량 | 각 28,355 | 1 |
| Bone | 410 | 615 | 500웨이브 재계산 전 기대량 | 615 | 0.004332359 |
| Wood | 634 | 951 | 500웨이브 재계산 전 기대량 | 951 | 0.011179686 |
| Molotov | 4,160 | 6,240 | 500웨이브 재계산 전 기대량 | 6,240 | 0.220067008 |
| OpenedCan | Aluminium 공급원 759 | 1,138 | 500웨이브 재계산 전 기대량 | 1,138 | 0.040134015 |
| Trumpet | Iron 공급원 690 | 1,034 | 500웨이브 재계산 전 기대량 | 1,034 | 0.036466232 |
| MetalLighter | Iron 공급원 2,585 | 3,877 | 500웨이브 재계산 전 기대량 | 3,877 | 0.135868232 |
| Lead / DuctTape | 각 80 | 각 120 | 500웨이브 재계산 전 기대량 | 각 120 | 아이템별 계산값 적용 |
| Frog / HollyPlant / Clover / GreenEye | 각 40 | 각 60 | 500웨이브 재계산 전 기대량 | 각 60 | 아이템별 계산값 적용 |
| GoldKey | 160 | 240 | 500웨이브 재계산 전 기대량 | 180 | 1 |
| IceFlake | 80 | 120 | 500웨이브 재계산 전 기대량 | 120 | 0.222222222 |
| Flare / Lightning | 각 8 | 각 12 | 500웨이브 재계산 전 기대량 | 각 12 | 0.066666667 |
| 나머지 기존 비코인 보상 아이템 | 0 | 각 100 | 아이템별 기존 기대량 | 각 100 | 아이템별 공통 확률 적용 |

Stone은 500웨이브까지 모든 기존 일반 좀비 Stone 행을 100%로 올려도 목표의 약 0.28%만 공급되어 포화 미달이다. 완전 강화 Concrete 목표에 필요한 Water 40,484,850개는 `VodkaBottle`, `WineBottle`, `Drink` 분해 기대량 7.5개를 기준으로 세 공급원에 균등 배분했다. 각 원본의 목표는 1,799,327개지만 기존 행을 모두 100%로 올려도 각각 28,355개만 획득되어 약 1.58%에서 포화된다. GoldKey도 보스 180회 전부 드랍해도 목표 240개 중 180개만 공급된다. 스테이지 보상 배율을 비코인에서 제거한 현재 규칙에서는 이 병목들을 드랍률만으로 해소할 수 없다.

### 비용 데이터 변경 후 재계산 순서

1. Unity에서 터렛/장애물 비용 CSV를 관련 SO에 먼저 임포트하고 밸런스 리포트를 새로고침한다.
2. 500웨이브 스폰 구성과 보상 배율을 다시 읽는다. `AvailableCoin`은 참고값만 갱신한다.
3. 터렛 8개의 최종 진화·최종 레벨 전체 비용과 게이트 1개·장애물 9개의 MaxLevel 전체 비용을 다시 구한다.
4. 비코인 소비량을 재화별로 병합하고 제작 가능한 항목을 입력 재료로 재귀 전개한 뒤, 직접 드랍되지 않는 재료는 분해 관계를 역추적해 기존 드랍 원본 수량으로 환산한다.
5. 필요량의 1.5배 또는 미사용 아이템 최소 100개 목표를 적용한다.
6. 기존 보상 행만 대상으로 아이템별 단일 공통 확률을 다시 풀고 포화/누락 재화를 기록한다.
7. `ZombieRewardProfiles.csv`에서 비코인 `DropChance`만 갱신한다.
8. CSV 행 수, `(ZombieType, CurrencyType)` 중복, Coin 행 불변, 확률 `0~1`을 검증한 뒤 사용자가 보상 SO 임포트를 실행한다.

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
15. Done: Move obstacle placement cost spending to `ResourceCost[]`.
16. Done: Remove normal zombie spec reward fallback fields after active reward profiles were verified.
17. Done: Remove legacy Coin-only `ItemManager` wrappers after old call sites reached zero.
18. Done: Add obstacle placement logs at confirmed placement time while keeping preview validation quiet.

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
- Obstacle placement should not instantiate an obstacle when build cost spend fails.
- Obstacle placement should refund costs if prefab instantiation fails after spend.
- Obstacle placement preview should not emit logs every frame.

## Next Cleanup Plan

1. Keep new cost data in `ResourceCost[]` fields.
2. Do not reintroduce Coin-only spend or refund wrappers.

## Tomorrow Handoff

Start with obstacle placement regression checks and any future multi-currency cost tuning.

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

- Keep obstacle build costs in `ObstacleBuildEntrySO.buildCosts`.
- Spend obstacle build costs through `ItemManager.TrySpend`.
- Refund through `ItemManager.Refund`.
- Keep obstacle placement UI displaying `ResourceCost[]`.

## Related Plan File

- `Assets/__PROJECT__/REWARD_CURRENCY_SYSTEM_PLAN.cs`
