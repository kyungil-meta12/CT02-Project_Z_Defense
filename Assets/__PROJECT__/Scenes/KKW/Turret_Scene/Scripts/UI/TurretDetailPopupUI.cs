using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 선택된 터렛의 읽기 전용 상세 수치와 데미지 폴리싱 확률 표시를 담당한다.
/// </summary>
public class TurretDetailPopupUI : TurretPopupPageUI
{
    private const int PREVIEW_LEVEL = 1;
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
    [SerializeField] private TMP_Text criticalChanceText;
    [SerializeField] private TMP_Text heavyHitChanceText;

    [Header("레거시 상세 정보")]
    [SerializeField] private TMP_Text statText;

    [Header("버튼")]
    [SerializeField] private Button detailUpgradeButton;
    [SerializeField] private GameObject detailUpgradeFrame;

    private string currentTurretNameTextTemplate;
    private string levelTextTemplate;
    private string dpsTextTemplate;
    private string fireRateTextTemplate;
    private string bulletSpeedTextTemplate;
    private string pierceCountTextTemplate;
    private string rangeTextTemplate;
    private string criticalChanceTextTemplate;
    private string heavyHitChanceTextTemplate;
    private bool isPreviewMode;
    private TurretDefinitionSO previewDefinition;

    public event UnityAction UpgradeRequested;

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
        BindButtonListeners();
    }

    // 파괴 시 상세 팝업 전용 버튼 이벤트를 해제한다
    protected override void OnDestroy()
    {
        UnbindButtonListeners();
        base.OnDestroy();
    }

    // 선택된 터렛의 현재 기본 스탯을 상세 팝업에 표시한다
    public override void Show(TurretSelectionContext context)
    {
        isPreviewMode = false;
        previewDefinition = null;
        base.Show(context);
        CacheTextTemplates();
        SetPreviewButtonState(false);
        RefreshHeader();
        RefreshStatText();
    }

    // 진화 후보 터렛을 1레벨 기준 상세 팝업으로 표시한다
    public void ShowPreview(TurretDefinitionSO definition)
    {
        if (definition == null)
        {
            return;
        }

        isPreviewMode = true;
        previewDefinition = definition;
        base.Show(default);
        BindChildReferences();
        CacheTextTemplates();
        SetPreviewButtonState(true);
        RefreshPreviewHeader();
        RefreshPreviewStatText();
    }

    [ContextMenu("참조 다시 연결")]
    // 컨텍스트 메뉴에서 상세 팝업 하위 참조를 다시 찾는다
    public void BindChildReferences()
    {
        Transform searchRoot = transform;
        currentTurretNameText = currentTurretNameText != null ? currentTurretNameText : FindFirstDescriptionComponent<TMP_Text>(searchRoot, "HighPanel/CurrentTurretNameFrame/CurrentTurretName", "CurrentTurretNameFrame/CurrentTurretName");
        turretImage = ResolveTurretIconImage(searchRoot, turretImage);
        levelText = levelText != null ? levelText : FindDescriptionComponent<TMP_Text>("MiddlePanel/DetailInfoPanel/Level", searchRoot);
        dpsText = dpsText != null ? dpsText : FindFirstDescriptionComponent<TMP_Text>(searchRoot, "MiddlePanel/DetailInfoPanel/Damage", "MiddlePanel/DetailInfoPanel/DPS");
        fireRateText = fireRateText != null ? fireRateText : FindDescriptionComponent<TMP_Text>("MiddlePanel/DetailInfoPanel/FireRate", searchRoot);
        bulletSpeedText = bulletSpeedText != null ? bulletSpeedText : FindDescriptionComponent<TMP_Text>("MiddlePanel/DetailInfoPanel/BulletSpeed", searchRoot);
        pierceCountText = pierceCountText != null ? pierceCountText : FindDescriptionComponent<TMP_Text>("MiddlePanel/DetailInfoPanel/PierceCount", searchRoot);
        rangeText = rangeText != null ? rangeText : FindDescriptionComponent<TMP_Text>("MiddlePanel/DetailInfoPanel/Range", searchRoot);
        criticalChanceText = criticalChanceText != null ? criticalChanceText : FindDescriptionComponent<TMP_Text>("MiddlePanel/DetailInfoPanel/CriticalChance", searchRoot);
        heavyHitChanceText = heavyHitChanceText != null ? heavyHitChanceText : FindDescriptionComponent<TMP_Text>("MiddlePanel/DetailInfoPanel/HeavyHitChance", searchRoot);
        detailUpgradeButton = detailUpgradeButton != null ? detailUpgradeButton : FindFirstDescriptionComponent<Button>(searchRoot, "LowPanel/UpgradeFrame/Upgrade", "LowPanel/UpgradeButton", "LowPanel/Upgrade");
        detailUpgradeFrame = detailUpgradeFrame != null ? detailUpgradeFrame : FindDescriptionGameObject("LowPanel/UpgradeFrame", searchRoot);
    }

    // 상세 팝업의 업그레이드 버튼 입력을 상위 컨트롤러에 알린다
    public void RequestUpgrade()
    {
        if (isPreviewMode)
        {
            return;
        }

        if (!CurrentContext.IsValid)
        {
            return;
        }

        UpgradeRequested?.Invoke();
    }

    // 닫기 버튼 입력을 현재 모드에 맞게 처리한다
    private void RequestDetailClose()
    {
        if (isPreviewMode)
        {
            Hide();
            return;
        }

        RequestCloseSelection();
    }

    // 뒤로가기 버튼 입력을 현재 모드에 맞게 처리한다
    private void RequestDetailBack()
    {
        if (isPreviewMode)
        {
            Hide();
            return;
        }

        RequestBackToSelectPopup();
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

        if (criticalChanceText != null && string.IsNullOrEmpty(criticalChanceTextTemplate))
        {
            criticalChanceTextTemplate = criticalChanceText.text;
        }

        if (heavyHitChanceText != null && string.IsNullOrEmpty(heavyHitChanceTextTemplate))
        {
            heavyHitChanceTextTemplate = heavyHitChanceText.text;
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

    // 미리보기 대상 터렛의 이름과 대표 이미지를 상세 팝업에 반영한다
    private void RefreshPreviewHeader()
    {
        TMP_Text previewNameText = FindFirstDescriptionComponent<TMP_Text>(transform, "HighPanel/CurrentTurretNameFrame/CurrentTurretName", "CurrentTurretNameFrame/CurrentTurretName");
        if (previewNameText != null)
        {
            currentTurretNameText = previewNameText;
        }

        if (previewDefinition == null)
        {
            SetText(currentTurretNameText, ApplyNameTemplate(currentTurretNameTextTemplate, string.Empty));
            SetTurretIconImage(turretImage, null);
            return;
        }

        SetText(currentTurretNameText, ApplyNameTemplate(currentTurretNameTextTemplate, GetDisplayName(previewDefinition)));
        SetTurretIconImage(turretImage, previewDefinition.uiIcon);
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
        TurretDamagePolishProfileSO damagePolishProfile = CurrentContext.Definition == null ? null : CurrentContext.Definition.damagePolishProfile;
        SetDetailStatTexts(CurrentContext.Turret.CurrentTierLevel, stat, damagePolishProfile);
        SetLegacyStatText(stat, damagePolishProfile);
    }

    // 미리보기 대상 터렛의 1레벨 상세 수치를 표시한다
    private void RefreshPreviewStatText()
    {
        if (previewDefinition == null)
        {
            ClearDetailStatTexts();
            SetText(statText, "선택된 터렛 없음");
            return;
        }

        TurretRuntimeStat stat = TurretStatCalculator.Calculate(previewDefinition, PREVIEW_LEVEL);
        TurretDamagePolishProfileSO damagePolishProfile = previewDefinition.damagePolishProfile;
        SetDetailStatTexts(PREVIEW_LEVEL, stat, damagePolishProfile);
        SetLegacyStatText(stat, damagePolishProfile);
    }

    // 상세 수치 TMP 템플릿에 현재 터렛 스탯을 반영한다
    private void SetDetailStatTexts(int currentLevel, TurretRuntimeStat stat, TurretDamagePolishProfileSO damagePolishProfile)
    {
        SetText(levelText, ApplyTemplate(levelTextTemplate, currentLevel.ToString()));
        SetText(dpsText, ApplyTemplate(dpsTextTemplate, FormatValue(stat.damage)));
        SetText(fireRateText, ApplyTemplate(fireRateTextTemplate, FormatValue(stat.fireInterval)));
        SetText(bulletSpeedText, ApplyTemplate(bulletSpeedTextTemplate, FormatValue(stat.projectileSpeed)));
        SetText(pierceCountText, ApplyTemplate(pierceCountTextTemplate, stat.pierceCount.ToString()));
        SetText(rangeText, ApplyTemplate(rangeTextTemplate, FormatValue(stat.range)));
        SetText(criticalChanceText, ApplyTemplate(criticalChanceTextTemplate, FormatChance(GetCriticalChance(damagePolishProfile))));
        SetText(heavyHitChanceText, ApplyTemplate(heavyHitChanceTextTemplate, FormatChance(GetHeavyHitChance(damagePolishProfile))));
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
        SetText(criticalChanceText, ApplyTemplate(criticalChanceTextTemplate, "-"));
        SetText(heavyHitChanceText, ApplyTemplate(heavyHitChanceTextTemplate, "-"));
    }

    // 기존 단일 상세 텍스트 참조가 남아 있을 때만 호환 표시를 유지한다
    private void SetLegacyStatText(TurretRuntimeStat stat, TurretDamagePolishProfileSO damagePolishProfile)
    {
        if (statText == null)
        {
            return;
        }

        statText.text = string.Concat(
            "공격력: ", FormatValue(stat.damage), "\n",
            "사거리: ", FormatValue(stat.range), "\n",
            "발사간격: ", stat.fireInterval.ToString("0.###"), "\n",
            "탄속: ", FormatValue(stat.projectileSpeed), "\n",
            "투사체 수: ", stat.projectileCount.ToString(), "\n",
            "관통 횟수: ", stat.pierceCount.ToString(), "\n",
            "치명타 확률: ", FormatChance(GetCriticalChance(damagePolishProfile)), "\n",
            "강타 확률: ", FormatChance(GetHeavyHitChance(damagePolishProfile)));
    }

    // 단일 스탯 값을 소수점 둘째 자리까지 표시한다
    private static string FormatValue(float value)
    {
        return value.ToString("0.##");
    }

    // 확률 값을 백분율 문자열로 변환한다
    private static string FormatChance(float chance)
    {
        return Mathf.Clamp01(chance).ToString("0.#%");
    }

    // 데미지 폴리싱 프로필에서 치명타 확률을 반환한다
    private static float GetCriticalChance(TurretDamagePolishProfileSO damagePolishProfile)
    {
        return damagePolishProfile == null ? 0.0f : damagePolishProfile.CriticalChance;
    }

    // 데미지 폴리싱 프로필에서 강타 확률을 반환한다
    private static float GetHeavyHitChance(TurretDamagePolishProfileSO damagePolishProfile)
    {
        return damagePolishProfile == null ? 0.0f : damagePolishProfile.HeavyHitChance;
    }

    // 터렛 정의의 표시 이름을 반환한다
    private static string GetDisplayName(TurretDefinitionSO definition)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(definition.displayName) ? definition.name : definition.displayName;
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

    // 상세 팝업 전용 버튼 클릭 이벤트를 등록한다
    private void BindButtonListeners()
    {
        UnbindButtonListeners();

        if (CloseButton != null)
        {
            CloseButton.onClick.AddListener(RequestDetailClose);
        }

        if (BackButton != null)
        {
            BackButton.onClick.AddListener(RequestDetailBack);
        }

        if (detailUpgradeButton != null)
        {
            detailUpgradeButton.onClick.AddListener(RequestUpgrade);
        }
    }

    // 상세 팝업 전용 버튼 클릭 이벤트를 해제한다
    private void UnbindButtonListeners()
    {
        if (CloseButton != null)
        {
            CloseButton.onClick.RemoveListener(RequestCloseSelection);
            CloseButton.onClick.RemoveListener(RequestDetailClose);
        }

        if (BackButton != null)
        {
            BackButton.onClick.RemoveListener(RequestBackToSelectPopup);
            BackButton.onClick.RemoveListener(RequestDetailBack);
        }

        if (detailUpgradeButton != null)
        {
            detailUpgradeButton.onClick.RemoveListener(RequestUpgrade);
        }
    }

    // 미리보기 모드에서 업그레이드 진입 버튼을 비활성화한다
    private void SetPreviewButtonState(bool isPreview)
    {
        if (detailUpgradeFrame != null)
        {
            detailUpgradeFrame.SetActive(!isPreview);
        }

        if (detailUpgradeButton != null)
        {
            detailUpgradeButton.interactable = !isPreview;
        }
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

    // 상세 팝업 배경명 기준으로 하위 게임 오브젝트를 찾는다
    private static GameObject FindDescriptionGameObject(string relativePath, Transform searchRoot)
    {
        if (searchRoot == null || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        Transform child = searchRoot.Find(DESCRIPTION_BACKGROUND_PATH + "/" + relativePath);
        return child == null ? null : child.gameObject;
    }

    // 여러 상세 팝업 경로 중 처음 발견되는 하위 컴포넌트를 반환한다
    private static T FindFirstDescriptionComponent<T>(Transform searchRoot, params string[] relativePaths) where T : Component
    {
        if (relativePaths == null)
        {
            return null;
        }

        for (int i = 0; i < relativePaths.Length; i++)
        {
            T component = FindDescriptionComponent<T>(relativePaths[i], searchRoot);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }
}
