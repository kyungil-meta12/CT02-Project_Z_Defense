using UnityEngine;

/// <summary>
/// 보스 좀비의 공통 스탯, 스킬 밸런스, 기본 처치 보상 프로필을 정의하는 ScriptableObject.
/// </summary>
[CreateAssetMenu(fileName = "BossZombieSpec", menuName = "Scriptable Objects/BossZombieSpec")]
public class BossZombieSpec : ScriptableObject
{
    [Header("보스 좀비 스펙(웨이브 1 기준)")]

    [Header("기본 이동 속도")] public float MoveSpeed;
    [Header("기본 공격 속도")] public float AttackSpeed;
    [Header("이동/공격 속도 가중치")] public float MoveAttackSpeedWeight;
    [Header("이동/공격 속도 랜덤 범위")] 
    public float MinMoveAttackSpeed;
    public float MaxMoveAttackSpeed;
    
    [Header("기본 공격 대미지")] public float AttackDamage;
    [Header("공격 대미지 가중치")] public float AttackDamageWeight;
    [Header("공격 대미지 랜덤 범위")] 
    public float MinAttackDamage;
    public float MaxAttackDamage;

    [Header("공격 가능 사거리")] public float AttackDistance;

    [Header("기본 HP")] public float Hp;
    [Header("Hp 가중치")] public float HpWeight;
    [Header("Hp 랜덤 범위")]
    public float MinHp;
    public float MaxHp;

    [Header("기본 처치 보상 프로필")] public ZombieRewardProfileSO RewardProfile;
    [Header("레거시 기본 아이템 드랍률(int)")] public float NormalItemDropPercentage;
    [Header("레거시 레어 아이템 드랍률(int)")] public float RareItemDropPercentage;

    // 인스펙터 입력값을 유효한 보스 스펙 범위로 보정한다
    private void OnValidate()
    {
        NormalItemDropPercentage = Mathf.Max(0.0f, NormalItemDropPercentage);
        RareItemDropPercentage = Mathf.Max(0.0f, RareItemDropPercentage);
    }
}
