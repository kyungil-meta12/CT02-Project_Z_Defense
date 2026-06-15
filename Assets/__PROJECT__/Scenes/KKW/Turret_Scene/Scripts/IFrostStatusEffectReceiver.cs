/// <summary>
/// Frost 계열 빔 공격에서 슬로우와 빙결 상태 효과를 받을 수 있는 런타임 대상 인터페이스다.
/// </summary>
public interface IFrostStatusEffectReceiver
{
    // 대상에게 Frost 상태 효과를 적용한다
    void ApplyFrostStatus(float slowRatio, float slowDuration, float freezeDuration);
}
