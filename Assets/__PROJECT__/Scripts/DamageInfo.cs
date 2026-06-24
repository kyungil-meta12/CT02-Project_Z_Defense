/// <summary>
/// 데미지 값과 표시 정보를 함께 전달하는 공통 데미지 컨텍스트.
/// </summary>
public readonly struct DamageInfo
{
    public readonly float Damage;
    public readonly DamagePopupType PopupType;
    public readonly DamagePopupPolicy PopupPolicy;

    // 데미지 값, 팝업 표시 타입, 팝업 표시 정책을 저장한다
    public DamageInfo(float damage, DamagePopupType popupType = DamagePopupType.Normal, DamagePopupPolicy popupPolicy = DamagePopupPolicy.Immediate)
    {
        Damage = damage;
        PopupType = popupType;
        PopupPolicy = popupPolicy;
    }
}
