/// <summary>
/// 재화 획득, 보유, 소비, 환불, 보상/비용 SO 구조를 추적하는 설계 문서.
/// README 성격의 문서 전용 .cs 파일.
/// </summary>
public static class REWARD_CURRENCY_SYSTEM_PLAN
{
    /*
     * ============================================================
     * Reward / Currency System Plan
     * ============================================================
     *
     * Purpose
     * - 좀비 처치 보상과 터렛 업그레이드/진화 비용을 같은 재화 모델 위에서 관리한다.
     * - 기존 ItemManager 구조는 즉시 폐기하지 않고, 지급/소비/환불 API를 분리하며 점진적으로 확장한다.
     * - 프리팹 Variant마다 보상 값을 흩뿌리지 않고 ScriptableObject 기반 데이터로 관리한다.
     * - UI, 좀비, 터렛 개별 클래스가 직접 보상/비용 계산식을 소유하지 않도록 책임을 분리한다.
     *
     * Current Problems
     * - NormalZombieSpec.DropCoin 하나로 일반 좀비 코인 보상이 고정되어 있었다.
     * - NormalZombie.OnDespawn()에서 보상을 지급해 풀링 반환과 처치 보상이 섞여 있었다.
     * - ItemManager.AddCoinCount()가 획득과 환불을 구분하지 않아 WaveCollectCoinCount가 오염될 수 있었다.
     * - TryUseCoin(), AddCoinCount() 직접 호출이 여러 시스템에 퍼지면 새 재화와 조건 추가 비용이 커진다.
     * - 보상/비용이 코인 기준으로만 표현되어 화기 부품, 속성 부품, 이벤트 재화 확장이 어렵다.
     *
     * Current Migration Status
     * - RewardCurrencyType, RewardEntry, ResourceCost를 추가했다.
     * - ZombieRewardProfileSO와 ZombieRewardContext를 추가했다.
     * - NormalZombieSpec이 ZombieRewardProfileSO를 참조한다.
     * - NormalZombie가 프리팹별 rewardProfileOverride를 우선 사용하고 없으면 Spec 기본값으로 fallback한다.
     * - BossZombie도 프리팹별 rewardProfileOverride를 우선 사용하고 없으면 BossZombieSpec 기본값으로 fallback한다.
     * - NormalZombie.Die()가 RewardGrantUtility를 통해 처치 보상을 지급한다.
     * - BossZombie.Die()가 RewardGrantUtility를 통해 처치 보상을 지급한다.
     * - ZombieRewardProfileSO.Modifiers로 웨이브 구간, 보스 여부, 라인, 상황 플래그, 재화별 보상 가중치를 적용한다.
     * - NormalZombie.OnDespawn()은 더 이상 처치 보상을 지급하지 않는다.
     * - RewardProfile 미연결 상태에서는 DropCoin을 레거시 fallback 보상으로 사용한다.
     * - ItemManager는 AddReward, CanAfford, TrySpend, Refund API를 제공한다.
     * - TurretUpgradeCostProfileSO를 추가하고 TurretDefinitionSO에서 참조한다.
     * - TurretEvolutionEntry에 ResourceCost[] evolutionCosts를 추가했다.
     * - 터렛 런타임 UI는 TryUpgrade, TryEvolve, TryCreateEvolvedInstance로 비용 성공 후에만 상태를 변경한다.
     *
     * Target Flow - Zombie Reward
     *
     * 1. NormalZombie.TakeDamage()에서 HP가 0 이하가 되면 Die()를 호출한다.
     * 2. Die()는 IsAlive를 false로 바꾸고 킬 카운트를 증가시킨다.
     * 3. Die()에서 프리팹별 rewardProfileOverride를 먼저 확인하고, 없으면 ZombieSpec.rewardProfile로 처치 보상을 요청한다.
     * 4. ZombieRewardContext가 현재 웨이브, 좀비 타입, 방어선/라인, 보스 여부, 이벤트 배율 등을 제공한다.
     * 5. RewardGrantUtility가 RewardProfile + Context + Modifiers로 최종 보상을 계산한다.
     * 6. ItemManager 또는 향후 Wallet이 계산된 재화를 지급하고 UI 이벤트를 발행한다.
     * 7. OnDespawn()은 풀 반환 정리만 담당하고 처치 보상 지급을 하지 않는다.
     *
     * Target Flow - Turret Upgrade / Evolution Cost
     *
     * 1. UI는 선택된 터렛에 업그레이드 또는 진화를 요청한다.
     * 2. TurretDefinitionRuntimeController가 비용/조건을 조회한다.
     * 3. TurretUpgradeCostProfileSO 또는 TurretEvolutionEntry의 비용 데이터를 확인한다.
     * 4. ItemManager 또는 향후 Wallet이 보유 재화를 검사하고 소비를 시도한다.
     * 5. 소비 성공 시에만 실제 레벨업/진화가 실행된다.
     * 6. 실패 시 UI는 부족한 재화 또는 잠긴 조건을 표시하고 런타임 상태를 변경하지 않는다.
     *
     * Core Data Model
     *
     * RewardCurrencyType
     * - Coin
     * - FirePart
     * - SpecialPart
     * - Future values can be added when new currencies are introduced.
     *
     * RewardEntry
     * - RewardCurrencyType currencyType
     * - int amount
     * - float dropChance
     * - Optional min/max random amount for future balancing.
     *
     * ZombieRewardModifier
     * - Conditional reward modifier inside ZombieRewardProfileSO.
     * - Conditions:
     *   target currency
     *   normal or boss zombie
     *   min/max wave
     *   defense line index
     *   required situation flags
     * - Effects:
     *   amountMultiplier
     *   flatAmountBonus
     *   dropChanceMultiplier
     *   additionalDropChance
     *
     * ZombieRewardSituation
     * - Runtime flags for event bonus, fever time, perfect defense, low base health, first kill in wave, or custom triggers.
     * - Future systems should add flags to ZombieRewardContext instead of creating prefab-specific reward code.
     *
     * ResourceCost
     * - RewardCurrencyType currencyType
     * - int amount
     * - Used by turret upgrade, turret evolution, placement, shop, and future skills.
     *
     * ZombieRewardProfileSO
     * - Holds reward entries for a zombie prefab override or zombie spec fallback.
     * - NormalZombie.rewardProfileOverride is used when stats are shared but rewards differ per prefab or Variant.
     * - NormalZombieSpec.RewardProfile remains the shared default fallback.
     * - BossZombie.rewardProfileOverride and BossZombieSpec.RewardProfile follow the same ownership rule.
     *
     * ZombieRewardContext
     * - Runtime-only value data.
     * - Expected fields:
     *   wave
     *   isBoss
     *   defenseLineIndex
     *   situationFlags
     *   sourceSpec
     *   rewardMultiplier
     *
     * TurretUpgradeCostProfileSO
     * - Calculates upgrade cost by current tier level and target tier level.
     * - Can support flat, linear, exponential, or interval-based costs.
     * - Referenced by TurretDefinitionSO or a shared turret economy config.
     *
     * TurretEvolutionEntry Cost Extension
     * - Keep existing requiredLevel and targetDefinition.
     * - Add ResourceCost[] costs for evolution.
     * - Optional requirements can be added later without changing the evolution tree shape.
     *
     * ItemManager Role
     * - Owns current player currency amounts.
     * - Emits value changed events for UI.
     * - Exposes explicit APIs:
     *   AddReward
     *   TrySpend
     *   CanAfford
     *   Refund
     * - Existing AddCoinCount, TryUseCoin, CanUseCoin can remain as compatibility wrappers during migration.
     *
     * Ownership Rules
     * - ScriptableObjects hold data, not scene-specific mutable state.
     * - Reward calculation does not live inside NormalZombie.
     * - Cost calculation does not live inside UI.
     * - UI never directly subtracts currency.
     * - Runtime controllers execute state changes only after cost and condition checks succeed.
     *
     * Initial Implementation Order
     *
     * 1. Done: Add RewardCurrencyType, RewardEntry, ResourceCost.
     * 2. Done: Add ZombieRewardProfileSO.
     * 3. Done: Add rewardProfile reference to NormalZombieSpec.
     * 4. Done: Move normal zombie reward grant from OnDespawn() to Die().
     * 5. Done: Add ItemManager reward/spend/refund APIs while keeping old wrappers.
     * 6. Done: Add prefab-level rewardProfileOverride to NormalZombie.
     * 7. Done: Add prefab-level rewardProfileOverride to BossZombie.
     * 8. Done: Add conditional ZombieRewardModifier support to ZombieRewardProfileSO.
     * 9. Done: Add TurretUpgradeCostProfileSO and connect it to TurretDefinitionSO.
     * 10. Done: Add ResourceCost[] to TurretEvolutionEntry.
     * 11. Done: Replace turret UI direct AddLevel/Evolve calls with TryUpgrade/TryEvolve calls.
     * 12. Next: Balance ZombieRewardProfileSO assets assigned to prefab overrides or spec fallback.
     * 13. Next: Create and assign TurretUpgradeCostProfileSO assets.
     * 14. Next: Fill evolutionCosts on turret evolution progression assets.
     * 15. Ongoing: Update Docs whenever reward or cost ownership changes.
     *
     * Edge Cases To Check
     * - Pooled zombie returned without dying must not grant rewards.
     * - MemoryPool.Prewarm must not grant rewards.
     * - Duplicate Die() calls must not grant duplicate rewards.
     * - Missing ItemManager should log a Korean actionable warning and skip only the reward grant.
     * - Missing rewardProfile should not break zombie death flow.
     * - Zero or negative reward amounts should be ignored or clamped.
     * - Drop chance must be clamped from 0 to 1.
     * - Modifier amount multipliers and chance multipliers must never go below 0.
     * - maxWave 0 means no upper wave cap.
     * - Refund must not increase wave-collected reward tracking.
     * - UI should stop upgrade hold when currency becomes insufficient.
     * - Evolution cost should be consumed once even when prefab replacement is used.
     *
     * Migration Notes
     * - NormalZombieSpec.DropCoin should be treated as legacy once rewardProfile is connected.
     * - Keep one shared NormalZombieSpec when only reward data differs; use NormalZombie.rewardProfileOverride on prefab originals or Variants.
     * - Existing obstacle placement can keep TryUseCoin temporarily, then move to ResourceCost later.
     * - BossZombieSpec item drop percentage fields are legacy balancing data and should move into reward profiles instead of adding more boss-specific code.
     */
}
