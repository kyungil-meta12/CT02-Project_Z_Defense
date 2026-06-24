/// <summary>
/// 터렛 데미지 폴리싱 계산 결과를 데미지 적용과 팝업 표시 경로에 전달한다.
/// </summary>
public readonly struct TurretDamagePolishResult
{
    public readonly float Damage;
    public readonly TurretDamagePolishType Type;

    public bool IsSpecial
    {
        get
        {
            return Type != TurretDamagePolishType.Normal;
        }
    }

    // 계산된 데미지와 표시 타입을 저장한다
    public TurretDamagePolishResult(float damage, TurretDamagePolishType type)
    {
        Damage = damage;
        Type = type;
    }
}
