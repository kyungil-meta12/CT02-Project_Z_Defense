using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 터렛 선택 후 업그레이드, 상세정보, 스킬 화면으로 이동하는 허브 팝업을 제어한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretSelectPopupUI : MonoBehaviour
{
    [Header("표시 루트")]
    [SerializeField] private GameObject popupRoot;

    [Header("버튼")]
    [SerializeField] private Button backgroundButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button detailButton;
    [SerializeField] private Button skillButton;

    [Header("텍스트")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text fireRateText;
    [SerializeField] private TMP_Text noteText;

    [Header("터렛 이미지")]
    [SerializeField] private Image turretImage;

    [Header("스킬 상태")]
    [SerializeField] private bool disableSkillButtonUntilImplemented = true;

    private string levelTextTemplate;
    private string damageTextTemplate;
    private string fireRateTextTemplate;

    public event UnityAction UpgradeRequested;
    public event UnityAction DetailRequested;
    public event UnityAction SkillRequested;
    public event UnityAction CloseRequested;

    // 컴포넌트 추가 시 하위 UI 참조를 자동으로 찾는다
    private void Reset()
    {
        BindChildReferences();
    }

    // 시작 전에 버튼 이벤트를 연결한다
    private void Awake()
    {
        BindChildReferences();
        EnsureBackgroundButton();
        CacheTextTemplates();
        BindButtonListeners();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindButtonListeners();
    }

    // 선택된 터렛 컨텍스트로 선택 허브 팝업을 표시한다
    public void Show(TurretSelectionContext context)
    {
        CacheTextTemplates();
        RefreshTexts(context);
        RefreshSkillState();

        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
        }
    }

    // 선택 허브 팝업을 숨긴다
    public void Hide()
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
    }

    // 업그레이드 버튼 입력을 상위 컨트롤러에 알린다
    public void RequestUpgrade()
    {
        UpgradeRequested?.Invoke();
    }

    // 상세정보 버튼 입력을 상위 컨트롤러에 알린다
    public void RequestDetail()
    {
        DetailRequested?.Invoke();
    }

    // 스킬 버튼 입력을 상위 컨트롤러에 알린다
    public void RequestSkill()
    {
        SkillRequested?.Invoke();
    }

    // 닫기 또는 바깥 배경 클릭 입력을 상위 컨트롤러에 알린다
    public void RequestClose()
    {
        CloseRequested?.Invoke();
    }

    [ContextMenu("참조 다시 연결")]
    // 컨텍스트 메뉴에서 선택 팝업 하위 참조를 다시 찾는다
    public void BindChildReferences()
    {
        if (popupRoot == null)
        {
            popupRoot = gameObject;
        }

        Transform searchRoot = transform;
        backgroundButton = backgroundButton != null ? backgroundButton : FindChildComponent<Button>(searchRoot, "TurretSelectPopupBackground");
        closeButton = closeButton != null ? closeButton : FindChildComponent<Button>(searchRoot, "TurretSelectPopupBackground/HighPanel/ExitFrame/Button");
        upgradeButton = upgradeButton != null ? upgradeButton : FindChildComponent<Button>(searchRoot, "TurretSelectPopupBackground/LowPanel/UpgradeFrame/Upgrade");
        detailButton = detailButton != null ? detailButton : FindChildComponent<Button>(searchRoot, "TurretSelectPopupBackground/LowPanel/InformationFrame/Information");
        skillButton = skillButton != null ? skillButton : FindChildComponent<Button>(searchRoot, "TurretSelectPopupBackground/LowPanel/SkillFrame/Skill");
        nameText = nameText != null ? nameText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/HighPanel/CurrentTurretNameFrame/CurrentTurretName");
        levelText = levelText != null ? levelText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/Panel/Level");
        damageText = damageText != null ? damageText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/Panel/DPS");
        fireRateText = fireRateText != null ? fireRateText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/Panel/FireRate");
        noteText = noteText != null ? noteText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/Panel/NoteFrame/Note");
        turretImage = ResolveTurretIconImage(searchRoot, turretImage);
    }

    // 배경 클릭용 Button 컴포넌트가 없으면 표시 루트에서 보강한다
    private void EnsureBackgroundButton()
    {
        if (backgroundButton != null)
        {
            return;
        }

        GameObject backgroundObject = popupRoot != null ? popupRoot : gameObject;
        backgroundButton = backgroundObject.GetComponent<Button>();
        if (backgroundButton == null)
        {
            backgroundButton = backgroundObject.AddComponent<Button>();
            backgroundButton.transition = Selectable.Transition.None;
        }
    }

    // 선택된 터렛의 간단 정보를 텍스트에 반영한다
    private void RefreshTexts(TurretSelectionContext context)
    {
        if (nameText != null)
        {
            nameText.text = context.GetDisplayName();
        }

        if (levelText != null)
        {
            levelText.text = ApplyTemplate(levelTextTemplate, context.GetLevelText());
        }

        TurretRuntimeStat stat = context.CalculateCurrentStat();
        if (damageText != null)
        {
            damageText.text = ApplyTemplate(damageTextTemplate, $"{CalculateDamagePerSecond(stat):0.##}");
        }

        if (fireRateText != null)
        {
            fireRateText.text = ApplyTemplate(fireRateTextTemplate, $"{stat.fireInterval:0.###}");
        }

        if (noteText != null)
        {
            noteText.text = context.GetShortDescription();
        }

        RefreshTurretImage(context.Definition);
    }

    // 선택된 터렛 정의에 연결된 UI 이미지를 반영한다
    private void RefreshTurretImage(TurretDefinitionSO definition)
    {
        Sprite sprite = definition == null ? null : definition.uiIcon;
        SetTurretIconImage(turretImage, sprite);
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

    // 스킬 버튼의 준비 중 상태를 갱신한다
    private void RefreshSkillState()
    {
        if (skillButton != null)
        {
            skillButton.interactable = !disableSkillButtonUntilImplemented;
        }

    }

    // 현재 스탯 기준 초당 피해량을 계산한다
    private static float CalculateDamagePerSecond(TurretRuntimeStat stat)
    {
        float safeFireInterval = Mathf.Max(0.01f, stat.fireInterval);
        int safeProjectileCount = Mathf.Max(1, stat.projectileCount);
        return Mathf.Max(0.0f, stat.damage) * safeProjectileCount / safeFireInterval;
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
    }

    // 현재 선택 팝업 하위의 터렛 이미지 참조만 사용한다
    private static Image ResolveTurretIconImage(Transform searchRoot, Image currentImage)
    {
        if (currentImage != null && currentImage.name != "TurretImageFrame" && currentImage.transform.IsChildOf(searchRoot))
        {
            return currentImage;
        }

        Image iconImage = FindFirstChildComponent<Image>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/TurretImage", "TurretSelectPopupBackground/MiddlePanel/TurretImageFrame/TurretImage");
        if (iconImage != null)
        {
            return iconImage;
        }

        return null;
    }

    // 버튼 클릭 이벤트를 등록한다
    private void BindButtonListeners()
    {
        UnbindButtonListeners();

        if (backgroundButton != null)
        {
            backgroundButton.onClick.AddListener(RequestClose);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(RequestClose);
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(RequestUpgrade);
        }

        if (detailButton != null)
        {
            detailButton.onClick.AddListener(RequestDetail);
        }

        if (skillButton != null)
        {
            skillButton.onClick.AddListener(RequestSkill);
        }
    }

    // 버튼 클릭 이벤트를 해제한다
    private void UnbindButtonListeners()
    {
        if (backgroundButton != null)
        {
            backgroundButton.onClick.RemoveListener(RequestClose);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(RequestClose);
        }

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveListener(RequestUpgrade);
        }

        if (detailButton != null)
        {
            detailButton.onClick.RemoveListener(RequestDetail);
        }

        if (skillButton != null)
        {
            skillButton.onClick.RemoveListener(RequestSkill);
        }
    }

    // 지정 경로의 하위 컴포넌트를 찾는다
    private static T FindChildComponent<T>(Transform searchRoot, string childPath) where T : Component
    {
        if (searchRoot == null || string.IsNullOrWhiteSpace(childPath))
        {
            return null;
        }

        Transform child = searchRoot.Find(childPath);
        return child == null ? null : child.GetComponent<T>();
    }

    // 여러 경로 중 처음 발견되는 하위 컴포넌트를 반환한다
    private static T FindFirstChildComponent<T>(Transform searchRoot, params string[] childPaths) where T : Component
    {
        if (childPaths == null)
        {
            return null;
        }

        for (int i = 0; i < childPaths.Length; i++)
        {
            T component = FindChildComponent<T>(searchRoot, childPaths[i]);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }
}
