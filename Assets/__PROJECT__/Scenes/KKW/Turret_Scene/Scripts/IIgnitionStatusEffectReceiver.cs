using UnityEngine;

namespace ProjectZDefense.StatusEffects
{
    /// <summary>
    /// Ignition 상태 효과 적용에 필요한 연소 틱데미지, 지속시간, 중첩, 사망 연출 데이터를 전달하는 값 타입이다.
    /// </summary>
    public struct IgnitionStatusPayload
    {
        public bool hasIgnitionStatus;
        public float damagePerSecond;
        public float maxHpDamageRatioPerTick;
        public float tickInterval;
        public float duration;
        public int maxStackCount;
        public IgnitionStackRefreshMode stackRefreshMode;
        public float bossDamageMultiplier;
        public GameObject burnDeathEffectPrefab;
        public float burnDeathEffectDuration;
        public IgnitionInteractionFlags interactionFlags;
    }

    /// <summary>
    /// Ignition 계열 화염 공격에서 연소 상태 효과를 받을 수 있는 런타임 대상 인터페이스다.
    /// </summary>
    public interface IIgnitionStatusEffectReceiver
    {
        // 대상에게 Ignition 상태 효과를 적용한다
        void ApplyIgnitionStatus(IgnitionStatusPayload payload);
    }
}
