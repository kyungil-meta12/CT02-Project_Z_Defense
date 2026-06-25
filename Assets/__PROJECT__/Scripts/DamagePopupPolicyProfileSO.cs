using UnityEngine;

/// <summary>
/// 데미지 소스 종류와 팝업 타입별 표시 정책을 에셋에서 조정한다.
/// </summary>
[CreateAssetMenu(fileName = "DamagePopupPolicyProfile", menuName = "__PROJECT__/UI/Damage Popup Policy Profile")]
public class DamagePopupPolicyProfileSO : ScriptableObject
{
    [Header("직접 타격")]
    [Tooltip("일반 직접 타격 팝업 정책")]
    [SerializeField] private DamagePopupPolicy directNormalPolicy = DamagePopupPolicy.Accumulate;
    [Tooltip("치명타 직접 타격 팝업 정책")]
    [SerializeField] private DamagePopupPolicy directCriticalPolicy = DamagePopupPolicy.Immediate;
    [Tooltip("강타 직접 타격 팝업 정책")]
    [SerializeField] private DamagePopupPolicy directHeavyPolicy = DamagePopupPolicy.Immediate;

    [Header("고빈도 틱")]
    [Tooltip("일반 고빈도 틱 데미지 팝업 정책")]
    [SerializeField] private DamagePopupPolicy highFrequencyNormalPolicy = DamagePopupPolicy.Accumulate;
    [Tooltip("치명타 고빈도 틱 데미지 팝업 정책")]
    [SerializeField] private DamagePopupPolicy highFrequencyCriticalPolicy = DamagePopupPolicy.Accumulate;
    [Tooltip("강타 고빈도 틱 데미지 팝업 정책")]
    [SerializeField] private DamagePopupPolicy highFrequencyHeavyPolicy = DamagePopupPolicy.Accumulate;

    [Header("범위 및 상태이상")]
    [Tooltip("체인 데미지 팝업 정책")]
    [SerializeField] private DamagePopupPolicy chainPolicy = DamagePopupPolicy.Accumulate;
    [Tooltip("지속 피해 팝업 정책")]
    [SerializeField] private DamagePopupPolicy damageOverTimePolicy = DamagePopupPolicy.Accumulate;
    [Tooltip("광역 피해 팝업 정책")]
    [SerializeField] private DamagePopupPolicy areaOfEffectPolicy = DamagePopupPolicy.Accumulate;
    [Tooltip("상태이상 폭발 또는 버스트 피해 팝업 정책")]
    [SerializeField] private DamagePopupPolicy statusBurstPolicy = DamagePopupPolicy.Accumulate;

    public DamagePopupPolicy ChainPolicy => chainPolicy;
    public DamagePopupPolicy DamageOverTimePolicy => damageOverTimePolicy;
    public DamagePopupPolicy AreaOfEffectPolicy => areaOfEffectPolicy;
    public DamagePopupPolicy StatusBurstPolicy => statusBurstPolicy;

    // 직접 타격의 팝업 타입에 맞는 표시 정책을 반환한다
    public DamagePopupPolicy ResolveDirectHit(DamagePopupType popupType)
    {
        switch (popupType)
        {
            case DamagePopupType.Heavy:
                return directHeavyPolicy;
            case DamagePopupType.Critical:
                return directCriticalPolicy;
            default:
                return directNormalPolicy;
        }
    }

    // 고빈도 틱 데미지의 팝업 타입에 맞는 표시 정책을 반환한다
    public DamagePopupPolicy ResolveHighFrequencyTick(DamagePopupType popupType)
    {
        switch (popupType)
        {
            case DamagePopupType.Heavy:
                return highFrequencyHeavyPolicy;
            case DamagePopupType.Critical:
                return highFrequencyCriticalPolicy;
            default:
                return highFrequencyNormalPolicy;
        }
    }
}
