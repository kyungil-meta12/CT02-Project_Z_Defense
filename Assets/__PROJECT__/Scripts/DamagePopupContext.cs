/// <summary>
/// 데미지 적용 직전에 계산된 팝업 표시 타입을 IDamageable 구현체가 읽을 수 있도록 짧게 보관한다.
/// </summary>
public static class DamagePopupContext
{
    private static TurretDamagePolishType currentType = TurretDamagePolishType.Normal;
    private static bool hasContext;

    public static bool HasContext
    {
        get
        {
            return hasContext;
        }
    }

    public static TurretDamagePolishType CurrentType
    {
        get
        {
            return hasContext ? currentType : TurretDamagePolishType.Normal;
        }
    }

    // 다음 TakeDamage 호출에서 사용할 팝업 타입을 설정한다
    public static void Begin(TurretDamagePolishType type)
    {
        currentType = type;
        hasContext = true;
    }

    // 팝업 타입 컨텍스트를 기본값으로 되돌린다
    public static void End()
    {
        currentType = TurretDamagePolishType.Normal;
        hasContext = false;
    }
}
