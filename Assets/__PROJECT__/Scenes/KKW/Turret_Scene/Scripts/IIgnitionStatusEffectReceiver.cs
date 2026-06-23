using UnityEngine;

namespace ProjectZDefense.StatusEffects
{
    /// <summary>
    /// Ignition 연소 상태가 다른 3세대 속성과 반응했을 때 사용할 반응 타입이다.
    /// </summary>
    public enum IgnitionReactionType
    {
        None,
        Frost,
        Poison,
        Electro
    }

    /// <summary>
    /// Ignition 상태 효과 적용에 필요한 연소 틱데미지, 지속시간, 중첩 데이터를 전달하는 값 타입이다.
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
        public float reactionDamagePerSecond;
        public float reactionMaxHpDamageRatioPerTick;
        public float reactionTickInterval;
        public float bossDamageMultiplier;
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

    /// <summary>
    /// Ignition 연소 상태가 활성화된 대상에게 다른 속성 공격 반응을 알리는 계약이다.
    /// </summary>
    public interface IIgnitionReactionReceiver
    {
        // 연소 상태가 다른 3세대 속성 공격과 반응했음을 알린다
        void NotifyIgnitionReaction(IgnitionReactionType reactionType);
    }
}
