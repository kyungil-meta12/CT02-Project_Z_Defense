namespace ProjectZDefense.StatusEffects
{
    /// <summary>
    /// Poison 상태 효과 적용에 필요한 체력비례 틱데미지와 중첩 데이터를 전달하는 값 타입이다.
    /// </summary>
    public struct PoisonStatusPayload
    {
        public bool hasPoisonStatus;
        public float maxHpDamageRatioPerTick;
        public float tickInterval;
        public float duration;
        public int maxStackCount;
        public PoisonStackRefreshMode stackRefreshMode;
        public float bossDamageMultiplier;
        public PoisonDeathBurstProfileSO deathBurstProfile;
        public float deathBurstRadius;
        public float deathBurstMaxHpDamageRatioPerTick;
        public float deathBurstTickInterval;
        public float deathBurstDuration;
        public int deathBurstMaxStackCount;
        public PoisonStackRefreshMode deathBurstStackRefreshMode;
        public float deathBurstBossDamageMultiplier;
        public bool deathBurstAllowChain;
        public global::TurretDamageMeterSource damageSource;
    }

    /// <summary>
    /// Poison 계열 투사체 공격에서 중독 상태 효과를 받을 수 있는 런타임 대상 인터페이스다.
    /// </summary>
    public interface IPoisonStatusEffectReceiver
    {
        bool IsPoisonLethalPending { get; }

        // 대상에게 Poison 상태 효과를 적용한다
        void ApplyPoisonStatus(PoisonStatusPayload payload);
    }
}
