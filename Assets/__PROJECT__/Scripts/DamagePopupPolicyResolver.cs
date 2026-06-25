using UnityEngine;

/// <summary>
/// 데미지 소스 종류에 맞는 데미지 팝업 표시 정책을 결정한다.
/// </summary>
public static class DamagePopupPolicyResolver
{
    private const string DAMAGE_POPUP_SETTINGS_RESOURCE_PATH = "UI/DamagePopupSettings";

    private static DamagePopupSettings cachedSettings;
    private static bool didLoadSettings;

    // 씬에서 로드한 데미지 팝업 설정을 정책 결정 캐시에 등록한다
    public static void SetSettings(DamagePopupSettings settings)
    {
        cachedSettings = settings;
        didLoadSettings = true;
    }

    // 단발 직접 타격의 팝업 표시 정책을 반환한다
    public static DamagePopupPolicy ResolveDirectHit(DamagePopupType popupType)
    {
        DamagePopupPolicyProfileSO profile = GetPolicyProfile();
        if (profile != null)
        {
            return profile.ResolveDirectHit(popupType);
        }

        if (popupType == DamagePopupType.Critical || popupType == DamagePopupType.Heavy)
        {
            return DamagePopupPolicy.Immediate;
        }

        return DamagePopupPolicy.Accumulate;
    }

    // 고빈도 틱 데미지의 팝업 표시 정책을 반환한다
    public static DamagePopupPolicy ResolveHighFrequencyTick(DamagePopupType popupType)
    {
        DamagePopupPolicyProfileSO profile = GetPolicyProfile();
        if (profile != null)
        {
            return profile.ResolveHighFrequencyTick(popupType);
        }

        return DamagePopupPolicy.Accumulate;
    }

    // 체인 데미지의 팝업 표시 정책을 반환한다
    public static DamagePopupPolicy ResolveChain()
    {
        DamagePopupPolicyProfileSO profile = GetPolicyProfile();
        return profile != null ? profile.ChainPolicy : DamagePopupPolicy.Accumulate;
    }

    // 지속 피해의 팝업 표시 정책을 반환한다
    public static DamagePopupPolicy ResolveDamageOverTime()
    {
        DamagePopupPolicyProfileSO profile = GetPolicyProfile();
        return profile != null ? profile.DamageOverTimePolicy : DamagePopupPolicy.Accumulate;
    }

    // 광역 피해의 팝업 표시 정책을 반환한다
    public static DamagePopupPolicy ResolveAreaOfEffect()
    {
        DamagePopupPolicyProfileSO profile = GetPolicyProfile();
        return profile != null ? profile.AreaOfEffectPolicy : DamagePopupPolicy.Accumulate;
    }

    // 상태이상 버스트 피해의 팝업 표시 정책을 반환한다
    public static DamagePopupPolicy ResolveStatusBurst()
    {
        DamagePopupPolicyProfileSO profile = GetPolicyProfile();
        return profile != null ? profile.StatusBurstPolicy : DamagePopupPolicy.Accumulate;
    }

    // Resources 설정에서 팝업 정책 프로필을 캐시해 반환한다
    private static DamagePopupPolicyProfileSO GetPolicyProfile()
    {
        if (!didLoadSettings)
        {
            cachedSettings = Resources.Load<DamagePopupSettings>(DAMAGE_POPUP_SETTINGS_RESOURCE_PATH);
            didLoadSettings = true;
        }

        return cachedSettings != null ? cachedSettings.PopupPolicyProfile : null;
    }
}
