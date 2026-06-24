/// <summary>
/// 데미지 값과 표시 정보를 함께 전달하는 공통 데미지 컨텍스트.
/// </summary>
public readonly struct DamageInfo
{
    public readonly float Damage;
    public readonly DamagePopupType PopupType;

    // 데미지 값과 팝업 표시 타입을 저장한다
    public DamageInfo(float damage, DamagePopupType popupType = DamagePopupType.Normal)
    {
        Damage = damage;
        PopupType = popupType;
    }
}
