using UnityEngine;

/// <summary>
/// 일반 좀비의 기본 전투 스탯과 처치 보상 프로필을 정의하는 ScriptableObject.
/// </summary>
[CreateAssetMenu(fileName = "NormalZombieSpec", menuName = "Scriptable Objects/NormalZombieSpec")]
public class NormalZombieSpec : ScriptableObject
{
    [Header("일반 좀비 스펙(웨이브 1 기준)")]

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

    [Header("처치 보상 프로필")] public ZombieRewardProfileSO RewardProfile;
    [Header("레거시 코인 보상 - RewardProfile 연결 전 임시 fallback")] public int DropCoin;

    // 인스펙터 입력값을 유효한 범위로 보정한다
    private void OnValidate()
    {
        DropCoin = Mathf.Max(0, DropCoin);
    }
}
