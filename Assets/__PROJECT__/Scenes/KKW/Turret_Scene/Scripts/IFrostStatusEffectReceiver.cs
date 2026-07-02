using UnityEngine;

namespace ProjectZDefense.StatusEffects
{
    /// <summary>
    /// Frost 상태 효과 적용에 필요한 누적 슬로우, 빙결, 폭발 데이터를 전달하는 값 타입이다.
    /// </summary>
    public struct FrostStatusPayload
    {
        public float tickInterval;
        public float slowBuildUpDuration;
        public float maxSlowRatio;
        public float slowHoldDuration;
        public float freezeDuration;
        public float freezeTriggerRatio;
        public bool canTriggerFreeze;
        public GameObject freezeEffectPrefab;
        public float freezeEffectDuration;
        public GameObject freezeDeathEffectPrefab;
        public float freezeDeathEffectDuration;
        public float freezeExplosionDamageDelay;
        public float freezeExplosionRadius;
        public float freezeExplosionDamage;
        public float freezePrimaryTargetMaxHpDamageRatio;
        public LayerMask freezeExplosionLayerMask;
        public float freezeCooldownPerTarget;
        public float freezeExplosionSlowRatio;
        public float freezeExplosionSlowDuration;
        public global::TurretDamageMeterSource damageSource;
    }

    /// <summary>
    /// Frost 계열 빔 공격에서 슬로우와 빙결 상태 효과를 받을 수 있는 런타임 대상 인터페이스다.
    /// </summary>
    public interface IFrostStatusEffectReceiver
    {
        // 대상에게 Frost 상태 효과를 적용한다
        void ApplyFrostStatus(FrostStatusPayload payload);
    }
}
