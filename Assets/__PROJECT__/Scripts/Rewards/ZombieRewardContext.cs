using UnityEngine;

/// <summary>
/// 좀비 처치 보상 계산에 필요한 런타임 상황 정보를 담는다.
/// </summary>
public struct ZombieRewardContext
{
    public int wave;
    public bool isBoss;
    public int defenseLineIndex;
    public float rewardMultiplier;
    public ZombieRewardSituation situationFlags;
    public Vector3 deathPosition;
    public ScriptableObject sourceSpec;

    // 일반 좀비 처치 보상 계산용 컨텍스트를 생성한다
    public static ZombieRewardContext CreateNormalZombie(int wave_, ScriptableObject sourceSpec_, Vector3 deathPosition_)
    {
        return new ZombieRewardContext
        {
            wave = Mathf.Max(1, wave_),
            isBoss = false,
            defenseLineIndex = -1,
            rewardMultiplier = 1.0f,
            situationFlags = ZombieRewardSituation.None,
            deathPosition = deathPosition_,
            sourceSpec = sourceSpec_
        };
    }

    // 보스 좀비 처치 보상 계산용 컨텍스트를 생성한다
    public static ZombieRewardContext CreateBossZombie(int wave_, ScriptableObject sourceSpec_, Vector3 deathPosition_)
    {
        return new ZombieRewardContext
        {
            wave = Mathf.Max(1, wave_),
            isBoss = true,
            defenseLineIndex = -1,
            rewardMultiplier = 1.0f,
            situationFlags = ZombieRewardSituation.None,
            deathPosition = deathPosition_,
            sourceSpec = sourceSpec_
        };
    }

    // 방어선 인덱스를 포함한 새 컨텍스트 값을 반환한다
    public ZombieRewardContext WithDefenseLine(int defenseLineIndex_)
    {
        ZombieRewardContext context = this;
        context.defenseLineIndex = defenseLineIndex_;
        return context;
    }

    // 추가 보상 배율을 곱한 새 컨텍스트 값을 반환한다
    public ZombieRewardContext WithRewardMultiplier(float rewardMultiplier_)
    {
        ZombieRewardContext context = this;
        context.rewardMultiplier *= Mathf.Max(0.0f, rewardMultiplier_);
        return context;
    }

    // 상황 플래그를 추가한 새 컨텍스트 값을 반환한다
    public ZombieRewardContext WithSituation(ZombieRewardSituation situationFlags_)
    {
        ZombieRewardContext context = this;
        context.situationFlags |= situationFlags_;
        return context;
    }
}
