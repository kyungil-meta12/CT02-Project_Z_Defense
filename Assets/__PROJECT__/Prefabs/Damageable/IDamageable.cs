/// <summary>
/// 전투 시스템에서 체력과 생존 상태를 읽고 데미지를 전달하기 위한 공통 피격 대상 계약.
/// </summary>
public interface IDamageable
{
    float TotalHp { get; }
    float CurrHp { get; }
    bool IsAlive { get; }

    // 데미지 값과 표시 정보를 받아 체력에 반영한다
    void TakeDamage(DamageInfo damageInfo);
}
