using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 진화 후보 터렛의 1레벨 기준 요약 정보와 상세 정보를 오버레이 팝업으로 표시한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretInfoPopupUI : MonoBehaviour
{
    private const int PREVIEW_LEVEL = 1;
    private const string BACKGROUND_PATH = "TurretSelectPopupBackground";

    [Header("표시 루트")]
    [SerializeField] private GameObject popupRoot;

    [Header("버튼")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button informationButton;

    [Header("텍스트")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text fireRateText;
    [SerializeField] private TMP_Text noteText;

    [Header("터렛 이미지")]
    [SerializeField] private Image turretImage;

    private TurretDefinitionSO currentDefinition;
    private string levelTextTemplate;
    private string damageTextTemplate;
    private string fireRateTextTemplate;

    // 컴포넌트 추가 시 현재 팝업 하위 참조를 자동으로 찾는다
    private void Reset()
    {
        BindChildReferences();
    }

    // 활성화 준비 시 하위 참조와 버튼 이벤트를 연결한다
    private void Awake()
    {
        BindChildReferences();
        CacheTextTemplates();
        BindButtonListeners();
        if (currentDefinition == null)
        {
            Hide();
        }
    }

    // 파괴 시 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindButtonListeners();
    }

    [ContextMenu("참조 다시 연결")]
    // 컨텍스트 메뉴에서 정보 팝업 하위 참조를 다시 찾는다
    public void BindChildReferences()
    {
        popupRoot = popupRoot != null ? popupRoot : gameObject;
        Transform searchRoot = transform;
        backButton = backButton != null ? backButton : FindFirstChildComponent<Button>(searchRoot, BACKGROUND_PATH + "/LowPanel/BackFrame/Back", BACKGROUND_PATH + "/LowPanel/BackButtonFrame/BackButton");
        closeButton = closeButton != null ? closeButton : FindFirstChildComponent<Button>(searchRoot, BACKGROUND_PATH + "/HighPanel/ExitFrame/Button");
        informationButton = informationButton != null ? informationButton : FindFirstChildComponent<Button>(searchRoot, BACKGROUND_PATH + "/LowPanel/InformationFrame/Information");
        nameText = nameText != null ? nameText : FindFirstChildComponent<TMP_Text>(searchRoot, BACKGROUND_PATH + "/HighPanel/CurrentTurretNameFrame/CurrentTurretName");
        levelText = levelText != null ? levelText : FindFirstChildComponent<TMP_Text>(searchRoot, BACKGROUND_PATH + "/MiddlePanel/Panel/Level");
        damageText = damageText != null ? damageText : FindFirstChildComponent<TMP_Text>(searchRoot, BACKGROUND_PATH + "/MiddlePanel/Panel/DPS");
        fireRateText = fireRateText != null ? fireRateText : FindFirstChildComponent<TMP_Text>(searchRoot, BACKGROUND_PATH + "/MiddlePanel/Panel/FireRate");
        noteText = noteText != null ? noteText : FindFirstChildComponent<TMP_Text>(searchRoot, BACKGROUND_PATH + "/MiddlePanel/Panel/NoteFrame/Note");
        turretImage = ResolveTurretIconImage(searchRoot, turretImage);
    }

    // 지정 터렛 정의의 1레벨 요약 정보를 표시한다
    public void Show(TurretDefinitionSO definition)
    {
        currentDefinition = definition;
        BindChildReferences();
        CacheTextTemplates();
        RefreshSummary();

        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
        }
    }

    // 정보 팝업을 닫는다
    public void Hide()
    {
        BindChildReferences();

        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
    }

    // 정보 버튼 입력으로 현재 후보 터렛의 상세 정보를 노트 영역에 표시한다
    public void ShowDetail()
    {
        if (currentDefinition == null)
        {
            return;
        }

        TurretRuntimeStat stat = TurretStatCalculator.Calculate(currentDefinition, PREVIEW_LEVEL);
        TurretDamagePolishProfileSO damagePolishProfile = currentDefinition.damagePolishProfile;
        SetText(noteText, string.Concat(
            "공격력: ", FormatValue(stat.damage), "\n",
            "사거리: ", FormatValue(stat.range), "\n",
            "발사간격: ", FormatValue(stat.fireInterval), "\n",
            "탄속: ", FormatValue(stat.projectileSpeed), "\n",
            "투사체 수: ", Mathf.Max(1, stat.projectileCount).ToString(), "\n",
            "관통 횟수: ", Mathf.Max(0, stat.pierceCount).ToString(), "\n",
            "치명타 확률: ", FormatChance(GetCriticalChance(damagePolishProfile)), "\n",
            "강타 확률: ", FormatChance(GetHeavyHitChance(damagePolishProfile))));
    }

    // TMP 원문 템플릿을 최초 한 번 보관한다
    private void CacheTextTemplates()
    {
        if (levelText != null && string.IsNullOrEmpty(levelTextTemplate))
        {
            levelTextTemplate = levelText.text;
        }

        if (damageText != null && string.IsNullOrEmpty(damageTextTemplate))
        {
            damageTextTemplate = damageText.text;
        }

        if (fireRateText != null && string.IsNullOrEmpty(fireRateTextTemplate))
        {
            fireRateTextTemplate = fireRateText.text;
        }
    }

    // 현재 터렛 정의의 요약 정보를 텍스트와 이미지에 반영한다
    private void RefreshSummary()
    {
        if (currentDefinition == null)
        {
            SetText(nameText, string.Empty);
            SetText(levelText, ApplyTemplate(levelTextTemplate, "-"));
            SetText(damageText, ApplyTemplate(damageTextTemplate, "-"));
            SetText(fireRateText, ApplyTemplate(fireRateTextTemplate, "-"));
            SetText(noteText, string.Empty);
            SetTurretIconImage(turretImage, null);
            return;
        }

        TurretRuntimeStat stat = TurretStatCalculator.Calculate(currentDefinition, PREVIEW_LEVEL);
        SetText(nameText, GetDisplayName(currentDefinition));
        SetText(levelText, ApplyTemplate(levelTextTemplate, "Lv. " + PREVIEW_LEVEL));
        SetText(damageText, ApplyTemplate(damageTextTemplate, FormatDps(stat)));
        SetText(fireRateText, ApplyTemplate(fireRateTextTemplate, FormatValue(stat.fireInterval)));
        SetText(noteText, currentDefinition.shortDescription);
        SetTurretIconImage(turretImage, currentDefinition.uiIcon);
    }

    // 버튼 클릭 이벤트를 등록한다
    private void BindButtonListeners()
    {
        UnbindButtonListeners();

        if (backButton != null)
        {
            backButton.onClick.AddListener(Hide);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }

        if (informationButton != null)
        {
            informationButton.onClick.AddListener(ShowDetail);
        }
    }

    // 버튼 클릭 이벤트를 해제한다
    private void UnbindButtonListeners()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(Hide);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
        }

        if (informationButton != null)
        {
            informationButton.onClick.RemoveListener(ShowDetail);
        }
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

    // 초당 피해량 표시 문자열을 생성한다
    private static string FormatDps(TurretRuntimeStat stat)
    {
        float safeFireInterval = Mathf.Max(0.01f, stat.fireInterval);
        int safeProjectileCount = Mathf.Max(1, stat.projectileCount);
        float dps = Mathf.Max(0.0f, stat.damage) * safeProjectileCount / safeFireInterval;
        return FormatValue(dps);
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

    // 템플릿의 중괄호 자리만 값으로 교체한다
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

    // 현재 정보 팝업 하위의 터렛 이미지 참조만 사용한다
    private static Image ResolveTurretIconImage(Transform searchRoot, Image currentImage)
    {
        if (currentImage != null && currentImage.name != "TurretImageFrame" && currentImage.transform.IsChildOf(searchRoot))
        {
            return currentImage;
        }

        return FindFirstChildComponent<Image>(searchRoot, BACKGROUND_PATH + "/MiddlePanel/TurretImage", BACKGROUND_PATH + "/MiddlePanel/TurretImageFrame/TurretImage");
    }

    // 여러 경로 중 처음 발견되는 하위 컴포넌트를 반환한다
    private static T FindFirstChildComponent<T>(Transform searchRoot, params string[] childPaths) where T : Component
    {
        if (searchRoot == null || childPaths == null)
        {
            return null;
        }

        for (int i = 0; i < childPaths.Length; i++)
        {
            Transform child = searchRoot.Find(childPaths[i]);
            if (child == null)
            {
                continue;
            }

            T component = child.GetComponent<T>();
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }
}
