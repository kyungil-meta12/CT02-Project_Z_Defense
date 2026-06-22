/// <summary>
/// 터렛 런타임 스탯 적용기가 계산한 전투 값을 받을 수 있는 보조 컴포넌트 계약이다.
/// </summary>
public interface ITurretRuntimeStatReceiver
{
    // 터렛 스탯 적용기가 계산한 최종 초당 데미지를 전달한다
    void SetDamagePerSecond(float damagePerSecond);
}
