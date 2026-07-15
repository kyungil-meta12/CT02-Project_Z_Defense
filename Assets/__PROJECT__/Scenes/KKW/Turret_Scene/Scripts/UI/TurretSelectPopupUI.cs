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
    private const int ENGINEER_SEAT_BUTTON_COUNT = 4;

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

    [Header("엔지니어 좌석")]
    [SerializeField] private GameObject engineerSeatButtonPrefab;
    [SerializeField] private RectTransform engineerSeatButtonRoot;

    private readonly Button[] engineerSeatButtons = new Button[ENGINEER_SEAT_BUTTON_COUNT];
    private readonly TMP_Text[] engineerSeatBuffTexts = new TMP_Text[ENGINEER_SEAT_BUTTON_COUNT];

    private TurretSelectionContext currentContext;
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
        ValidateRequiredReferences();
        CacheTextTemplates();
        BindButtonListeners();
        CreateEngineerSeatButtons();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindButtonListeners();
    }

    // 선택된 터렛 컨텍스트로 선택 허브 팝업을 표시한다
    public void Show(TurretSelectionContext context)
    {
        currentContext = context;
        CacheTextTemplates();
        RefreshTexts(context);
        RefreshSkillState();
        RefreshEngineerSeatButtons();

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
        damageText = damageText != null ? damageText : FindFirstChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/Panel/Damage", "TurretSelectPopupBackground/MiddlePanel/Panel/DPS");
        fireRateText = fireRateText != null ? fireRateText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/Panel/FireRate");
        noteText = noteText != null ? noteText : FindChildComponent<TMP_Text>(searchRoot, "TurretSelectPopupBackground/MiddlePanel/Panel/NoteFrame/Note");
        turretImage = ResolveTurretIconImage(searchRoot, turretImage);
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
            damageText.text = ApplyTemplate(damageTextTemplate, $"{stat.damage:0.##}");
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

    // 게임 시작 시 엔지니어 좌석 버튼을 미리 생성해 배열에 보관한다
    private void CreateEngineerSeatButtons()
    {
        if (engineerSeatButtonPrefab == null || engineerSeatButtonRoot == null)
        {
            Debug.LogWarning("[TurretSelectPopupUI] 엔지니어 좌석 버튼 프리팹 또는 부모 루트 참조가 비어 있습니다.", this);
            return;
        }

        for (int i = 0; i < engineerSeatButtons.Length; i++)
        {
            GameObject seatButtonObject = Instantiate(engineerSeatButtonPrefab, engineerSeatButtonRoot);
            Button seatButton = seatButtonObject.GetComponent<Button>();
            if (seatButton == null)
            {
                Debug.LogWarning("[TurretSelectPopupUI] 엔지니어 좌석 버튼 프리팹에 Button 컴포넌트가 없습니다.", this);
                continue;
            }

            int seatIndex = i;
            seatButton.onClick.AddListener(() => OnEngineerSeatButtonClicked(seatIndex));
            seatButton.gameObject.SetActive(false);
            engineerSeatButtons[i] = seatButton;
            engineerSeatBuffTexts[i] = seatButtonObject.GetComponentInChildren<TMP_Text>(true);
        }
    }

    // 현재 터렛 정의의 최대 좌석 수와 탑승 상태에 맞춰 좌석 버튼을 갱신한다
    private void RefreshEngineerSeatButtons()
    {
        TurretDefinitionRuntimeController turret = currentContext.IsValid ? currentContext.Turret : null;
        TurretDefinitionSO definition = currentContext.Definition;
        TurretEngineerBuffReceiver buffReceiver = turret == null ? null : turret.GetComponent<TurretEngineerBuffReceiver>();
        int maxSeatCount = definition == null ? 0 : Mathf.Max(0, definition.maxEngineerSeatCount);
        int activeSeatCount = Mathf.Min(maxSeatCount, engineerSeatButtons.Length);

        for (int i = 0; i < engineerSeatButtons.Length; i++)
        {
            Button seatButton = engineerSeatButtons[i];
            if (seatButton == null)
            {
                continue;
            }

            bool isSeatVisible = i < activeSeatCount;
            seatButton.gameObject.SetActive(isSeatVisible);
            if (!isSeatVisible)
            {
                continue;
            }

            Survivor engineer = buffReceiver == null ? null : buffReceiver.GetEngineerAt(i);
            seatButton.interactable = engineer != null;
            SetText(engineerSeatBuffTexts[i], FormatEngineerSeatBuffText(engineer, buffReceiver));
        }
    }

    // 좌석 버튼 클릭 시 탑승 중인 엔지니어를 하차시킨다
    private void OnEngineerSeatButtonClicked(int seatIndex)
    {
        if (!currentContext.IsValid)
        {
            return;
        }

        TurretEngineerBuffReceiver buffReceiver = currentContext.Turret.GetComponent<TurretEngineerBuffReceiver>();
        Survivor engineer = buffReceiver == null ? null : buffReceiver.GetEngineerAt(seatIndex);
        if (engineer == null || !engineer.TryDismountEngineerFromTurret())
        {
            Debug.LogWarning("[TurretSelectPopupUI] 엔지니어 하차 요청을 처리하지 못했습니다.", this);
        }

        RefreshEngineerSeatButtons();
    }

    // 좌석에 탑승 중인 엔지니어가 적용하는 버프 수치를 UI 문자열로 변환한다
    private static string FormatEngineerSeatBuffText(Survivor engineer, TurretEngineerBuffReceiver buffReceiver)
    {
        if (engineer == null)
        {
            return "빈 좌석";
        }

        if (buffReceiver == null || buffReceiver.DamageBonusRatioPerEngineer <= 0.0f)
        {
            return string.Empty;
        }

        return $"+{buffReceiver.DamageBonusRatioPerEngineer * 100.0f:0.#}%";
    }

    // 텍스트 참조가 있을 때만 문자열을 적용한다
    private static void SetText(TMP_Text targetText, string value)
    {
        if (targetText != null)
        {
            targetText.text = value;
        }
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

    // 선택 팝업에 필요한 수동 연결 참조를 검증한다
    private void ValidateRequiredReferences()
    {
        if (popupRoot == null)
        {
            Debug.LogWarning("[TurretSelectPopupUI] Popup Root 참조가 비어 있습니다.", this);
        }

        if (backgroundButton == null)
        {
            Debug.LogWarning("[TurretSelectPopupUI] Background Button 참조가 비어 있습니다. 바깥 클릭 닫기가 동작하지 않습니다.", this);
        }

        if (closeButton == null)
        {
            Debug.LogWarning("[TurretSelectPopupUI] Close Button 참조가 비어 있습니다.", this);
        }

        if (upgradeButton == null || detailButton == null || skillButton == null)
        {
            Debug.LogWarning("[TurretSelectPopupUI] 선택 팝업의 기능 버튼 참조가 일부 비어 있습니다.", this);
        }

        if (nameText == null || levelText == null || damageText == null || fireRateText == null || noteText == null)
        {
            Debug.LogWarning("[TurretSelectPopupUI] 선택 팝업 TMP 참조가 일부 비어 있습니다.", this);
        }

        if (turretImage == null)
        {
            Debug.LogWarning("[TurretSelectPopupUI] Turret Image 참조가 비어 있습니다.", this);
        }
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
