using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택된 터렛의 읽기 전용 상세 정보를 표시할 팝업의 1차 표시 연결을 담당한다.
/// </summary>
public class TurretDetailPopupUI : TurretPopupPageUI
{
    private const string DESCRIPTION_BACKGROUND_PATH = "TurretDescriptionPopupBackground";

    [Header("터렛 기본 정보")]
    [SerializeField] private TMP_Text currentTurretNameText;
    [SerializeField] private Image turretImage;

    [Header("상세 수치")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text dpsText;
    [SerializeField] private TMP_Text fireRateText;
    [SerializeField] private TMP_Text bulletSpeedText;
    [SerializeField] private TMP_Text pierceCountText;
    [SerializeField] private TMP_Text rangeText;

    [Header("상세 정보")]
    [SerializeField] private TMP_Text statText;

    private string currentTurretNameTextTemplate;
    private string levelTextTemplate;
    private string dpsTextTemplate;
    private string fireRateTextTemplate;
    private string bulletSpeedTextTemplate;
    private string pierceCountTextTemplate;
    private string rangeTextTemplate;

    // 컴포넌트 추가 시 현재 팝업 하위 참조를 자동으로 찾는다
    private void Reset()
    {
        BindChildReferences();
    }

    // 활성화 준비 시 하위 참조와 템플릿을 준비한다
    protected override void Awake()
    {
        base.Awake();
        BindChildReferences();
        CacheTextTemplates();
    }

    // 선택된 터렛의 현재 기본 스탯을 상세 팝업에 표시한다
    public override void Show(TurretSelectionContext context)
    {
        base.Show(context);
        CacheTextTemplates();
        RefreshHeader();
        RefreshStatText();
    }

    [ContextMenu("참조 다시 연결")]
    // 컨텍스트 메뉴에서 상세 팝업 하위 참조를 다시 찾는다
    public void BindChildReferences()
    {
        Transform searchRoot = transform;
        currentTurretNameText = currentTurretNameText != null ? currentTurretNameText : FindDescriptionComponent<TMP_Text>("CurrentTurretNameFrame/CurrentTurretName", searchRoot);
        turretImage = ResolveTurretIconImage(searchRoot, turretImage);
        levelText = levelText != null ? levelText : FindDescriptionComponent<TMP_Text>("MiddlePanel/DetailInfoPanel/Level", searchRoot);
        dpsText = dpsText != null ? dpsText : FindDescriptionComponent<TMP_Text>("MiddlePanel/DetailInfoPanel/DPS", searchRoot);
        fireRateText = fireRateText != null ? fireRateText : FindDescriptionComponent<TMP_Text>("MiddlePanel/DetailInfoPanel/FireRate", searchRoot);
        bulletSpeedText = bulletSpeedText != null ? bulletSpeedText : FindDescriptionComponent<TMP_Text>("MiddlePanel/DetailInfoPanel/BulletSpeed", searchRoot);
        pierceCountText = pierceCountText != null ? pierceCountText : FindDescriptionComponent<TMP_Text>("MiddlePanel/DetailInfoPanel/PierceCount", searchRoot);
        rangeText = rangeText != null ? rangeText : FindDescriptionComponent<TMP_Text>("MiddlePanel/DetailInfoPanel/Range", searchRoot);
    }

    // TMP 원문 템플릿을 보관해 괄호와 고정 문구를 유지한다
    private void CacheTextTemplates()
    {
        if (currentTurretNameText != null && string.IsNullOrEmpty(currentTurretNameTextTemplate))
        {
            currentTurretNameTextTemplate = currentTurretNameText.text;
        }

        if (levelText != null && string.IsNullOrEmpty(levelTextTemplate))
        {
            levelTextTemplate = levelText.text;
        }

        if (dpsText != null && string.IsNullOrEmpty(dpsTextTemplate))
        {
            dpsTextTemplate = dpsText.text;
        }

        if (fireRateText != null && string.IsNullOrEmpty(fireRateTextTemplate))
        {
            fireRateTextTemplate = fireRateText.text;
        }

        if (bulletSpeedText != null && string.IsNullOrEmpty(bulletSpeedTextTemplate))
        {
            bulletSpeedTextTemplate = bulletSpeedText.text;
        }

        if (pierceCountText != null && string.IsNullOrEmpty(pierceCountTextTemplate))
        {
            pierceCountTextTemplate = pierceCountText.text;
        }

        if (rangeText != null && string.IsNullOrEmpty(rangeTextTemplate))
        {
            rangeTextTemplate = rangeText.text;
        }
    }

    // 현재 터렛의 이름과 대표 이미지를 상세 팝업에 반영한다
    private void RefreshHeader()
    {
        if (!CurrentContext.IsValid)
        {
            SetText(currentTurretNameText, ApplyNameTemplate(currentTurretNameTextTemplate, string.Empty));
            SetTurretIconImage(turretImage, null);
            return;
        }

        SetText(currentTurretNameText, ApplyNameTemplate(currentTurretNameTextTemplate, CurrentContext.GetDisplayName()));
        SetTurretIconImage(turretImage, CurrentContext.Definition == null ? null : CurrentContext.Definition.uiIcon);
    }

    // 현재 터렛 스탯을 상세 정보 문자열로 갱신한다
    private void RefreshStatText()
    {
        if (!CurrentContext.IsValid)
        {
            ClearDetailStatTexts();
            SetText(statText, "선택된 터렛 없음");
            return;
        }

        TurretRuntimeStat stat = CurrentContext.CalculateCurrentStat();
        SetDetailStatTexts(CurrentContext.Turret.CurrentTierLevel, stat);
        SetLegacyStatText(stat);
    }

    // 상세 수치 TMP 템플릿에 현재 터렛 스탯을 반영한다
    private void SetDetailStatTexts(int currentLevel, TurretRuntimeStat stat)
    {
        SetText(levelText, ApplyTemplate(levelTextTemplate, currentLevel.ToString()));
        SetText(dpsText, ApplyTemplate(dpsTextTemplate, FormatDps(stat)));
        SetText(fireRateText, ApplyTemplate(fireRateTextTemplate, FormatValue(stat.fireInterval)));
        SetText(bulletSpeedText, ApplyTemplate(bulletSpeedTextTemplate, FormatValue(stat.projectileSpeed)));
        SetText(pierceCountText, ApplyTemplate(pierceCountTextTemplate, stat.pierceCount.ToString()));
        SetText(rangeText, ApplyTemplate(rangeTextTemplate, FormatValue(stat.range)));
    }

    // 유효하지 않은 선택 상태에서 상세 수치 TMP를 비운다
    private void ClearDetailStatTexts()
    {
        SetText(levelText, ApplyTemplate(levelTextTemplate, "-"));
        SetText(dpsText, ApplyTemplate(dpsTextTemplate, "-"));
        SetText(fireRateText, ApplyTemplate(fireRateTextTemplate, "-"));
        SetText(bulletSpeedText, ApplyTemplate(bulletSpeedTextTemplate, "-"));
        SetText(pierceCountText, ApplyTemplate(pierceCountTextTemplate, "-"));
        SetText(rangeText, ApplyTemplate(rangeTextTemplate, "-"));
    }

    // 기존 단일 상세 텍스트 참조가 남아 있을 때만 호환 표시를 유지한다
    private void SetLegacyStatText(TurretRuntimeStat stat)
    {
        if (statText == null)
        {
            return;
        }

        statText.text =
            $"공격력: {stat.damage:0.##}\n" +
            $"사거리: {stat.range:0.##}\n" +
            $"발사간격: {stat.fireInterval:0.###}\n" +
            $"탄속: {stat.projectileSpeed:0.##}\n" +
            $"투사체 수: {stat.projectileCount}\n" +
            $"관통 횟수: {stat.pierceCount}";
    }

    // 초당 피해량 표시 문자열을 생성한다
    private static string FormatDps(TurretRuntimeStat stat)
    {
        return FormatValue(CalculateDps(stat));
    }

    // 초당 피해량을 계산한다
    private static float CalculateDps(TurretRuntimeStat stat)
    {
        float safeFireInterval = Mathf.Max(0.01f, stat.fireInterval);
        int safeProjectileCount = Mathf.Max(1, stat.projectileCount);
        return Mathf.Max(0.0f, stat.damage) * safeProjectileCount / safeFireInterval;
    }

    // 단일 스탯 값을 소수점 둘째 자리까지 표시한다
    private static string FormatValue(float value)
    {
        return value.ToString("0.##");
    }

    // 템플릿의 중괄호 구간을 값으로 교체한다
    private static string ApplyTemplate(string template, string value)
    {
        if (string.IsNullOrEmpty(template))
        {
            return value;
        }

        int openIndex = template.IndexOf('{');
        int closeIndex = template.IndexOf('}', openIndex + 1);
        if (openIndex < 0 || closeIndex < 0 || closeIndex <= openIndex)
        {
            return value;
        }

        return template.Substring(0, openIndex) + value + template.Substring(closeIndex + 1);
    }

    // 이름 템플릿이 없으면 대괄호 안에 터렛 이름을 표시한다
    private static string ApplyNameTemplate(string template, string value)
    {
        if (string.IsNullOrEmpty(template))
        {
            return "[" + value + "]";
        }

        int openIndex = template.IndexOf('{');
        int closeIndex = template.IndexOf('}', openIndex + 1);
        if (openIndex < 0 || closeIndex < 0 || closeIndex <= openIndex)
        {
            return "[" + value + "]";
        }

        return ApplyTemplate(template, value);
    }

    // 텍스트 참조가 있을 때만 문자열을 적용한다
    private static void SetText(TMP_Text targetText, string value)
    {
        if (targetText != null)
        {
            targetText.text = value;
        }
    }

    // 터렛 대표 이미지를 현재 RectTransform 안에 비율 유지 방식으로 표시한다
    private static void SetTurretIconImage(Image targetImage, Sprite sprite)
    {
        if (targetImage == null)
        {
            return;
        }

        targetImage.sprite = sprite;
        targetImage.enabled = sprite != null;
        targetImage.type = Image.Type.Simple;
        targetImage.preserveAspect = true;
        targetImage.color = Color.white;
    }

    // 현재 상세 팝업 하위의 터렛 이미지 참조만 사용한다
    private static Image ResolveTurretIconImage(Transform searchRoot, Image currentImage)
    {
        if (currentImage != null && currentImage.transform.IsChildOf(searchRoot))
        {
            return currentImage;
        }

        return FindDescriptionComponent<Image>("MiddlePanel/TurretImage", searchRoot);
    }

    // 상세 팝업 배경명 기준으로 하위 컴포넌트를 찾는다
    private static T FindDescriptionComponent<T>(string relativePath, Transform searchRoot) where T : Component
    {
        if (searchRoot == null || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        Transform child = searchRoot.Find(DESCRIPTION_BACKGROUND_PATH + "/" + relativePath);
        return child == null ? null : child.GetComponent<T>();
    }
}
