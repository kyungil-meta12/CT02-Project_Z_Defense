using System;

/// <summary>
/// 좀비 처치 보상 계산에 사용할 상황 플래그.
/// </summary>
[Flags]
public enum ZombieRewardSituation
{
    None = 0,
    EventBonus = 1 << 0,
    FeverTime = 1 << 1,
    PerfectDefense = 1 << 2,
    LowBaseHealth = 1 << 3,
    FirstKillInWave = 1 << 4,
    CustomA = 1 << 20,
    CustomB = 1 << 21,
    CustomC = 1 << 22
}
