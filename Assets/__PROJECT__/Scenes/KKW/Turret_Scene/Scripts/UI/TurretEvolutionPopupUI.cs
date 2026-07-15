using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택된 터렛의 진화 분기 수에 맞는 패널을 열고 진화 후보 정보와 실행 버튼을 표시한다.
/// </summary>
public class TurretEvolutionPopupUI : TurretPopupPageUI
{
    private const int MAX_EVOLUTION_SLOT_COUNT = 4;
    private const int MAX_RESOURCE_SLOT_COUNT = 8;
    private const string INSUFFICIENT_COST_COLOR = "#FF4040";
    private const string BACKGROUND_PATH = "TurretSelectPopupBackground";
    private const string PANEL_A_PATH = BACKGROUND_PATH + "/MiddlePanel_A";
    private const string PANEL_B_PATH = BACKGROUND_PATH + "/MiddlePanel_B";
    private const string PANEL_C_PATH = BACKGROUND_PATH + "/MiddlePanel_C";
    private const float UI_AUTO_REFRESH_INTERVAL = 1.0f;

    [Header("진화 패널")]
    [SerializeField] private GameObject oneBranchPanel;
    [SerializeField] private GameObject twoBranchPanel;
    [SerializeField] private GameObject fourBranchPanel;

    [Header("선택 표시")]
    [SerializeField] private Color selectedCandidateFrameColor = new Color(1f, 0.12f, 0.12f, 1f);

    [Header("후보 정보 팝업")]
    [SerializeField] private TurretInfoPopupUI turretInfoPopup;
    [SerializeField, Min(0.1f)] private float candidateInfoHoldDuration = 0.5f;

    [Header("필요 재화")]
    [SerializeField] private TMP_Text[] resourceItemNameTexts = System.Array.Empty<TMP_Text>();
    [SerializeField] private TMP_Text[] resourceItemCountTexts = System.Array.Empty<TMP_Text>();
    [SerializeField] private Image[] resourceItemImages = System.Array.Empty<Image>();

    [Header("버튼")]
    [SerializeField] private Button evolutionButton;

    [Header("현재 터렛")]
    [SerializeField] private TMP_Text currentTurretNameText;
    [SerializeField] private TMP_Text nextTurretNameText;

    [Header("진화 실행")]
    [SerializeField] private bool replacePrefabOnEvolution = true;

    private int selectedEvolutionIndex;
    private string currentTurretNameTextTemplate;
    private string nextTurretNameTextTemplate;
    private Image[] nextTurretFrameImages = System.Array.Empty<Image>();
    private Color[] nextTurretFrameDefaultColors = System.Array.Empty<Color>();
    private Button[] evolutionCandidateButtons = System.Array.Empty<Button>();
    private Transform[] resourceSlotFrames = System.Array.Empty<Transform>();
    private Sprite[] resourceItemDefaultSprites = System.Array.Empty<Sprite>();
    private int pressedCandidateIndex = -1;
    private float pressedCandidateElapsedTime;
    private bool hasOpenedInfoByHold;
    private bool suppressNextCandidateClick;
    private float uiRefreshTimer;

    // 컴포넌트 추가 시 현재 팝업 하위 참조를 자동으로 찾는다
    private void Reset()
    {
        BindChildReferences();
    }

    // 활성화 준비 시 하위 참조와 버튼 이벤트를 준비한다
    protected override void Awake()
    {
        base.Awake();
        BindChildReferences();
        ValidateRequiredReferences();
        CacheResourceDefaultSprites();
        CacheTextTemplates();
        BindButtonListeners();
    }

    // 후보 버튼을 누르고 있는 시간을 계산해 정보 팝업을 연다
    private void Update()
    {
        UpdateAutoRefreshTimer();

        if (pressedCandidateIndex < 0 || hasOpenedInfoByHold)
        {
            return;
        }

        pressedCandidateElapsedTime += Time.unscaledDeltaTime;
        if (pressedCandidateElapsedTime < candidateInfoHoldDuration)
        {
            return;
        }

        hasOpenedInfoByHold = true;
        suppressNextCandidateClick = true;
        OpenEvolutionCandidateInfo(pressedCandidateIndex);
    }

    // 팝업이 표시된 동안 1초마다 재화 보유량과 진화 가능 상태를 갱신한다
    private void UpdateAutoRefreshTimer()
    {
        if (!IsVisible || !CurrentContext.IsValid)
        {
            uiRefreshTimer = 0.0f;
            return;
        }

        uiRefreshTimer += Time.unscaledDeltaTime;
        if (uiRefreshTimer < UI_AUTO_REFRESH_INTERVAL)
        {
            return;
        }

        uiRefreshTimer = 0.0f;
        RefreshEvolutionTexts();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    protected override void OnDestroy()
    {
        UnbindButtonListeners();
        base.OnDestroy();
    }

    // 선택된 터렛의 진화 정보를 표시한다
    public override void Show(TurretSelectionContext context)
    {
        base.Show(context);
        uiRefreshTimer = 0.0f;
        CacheTextTemplates();
        ClearCandidatePressState();
        RefreshEvolutionTexts();
    }

    [ContextMenu("참조 다시 연결")]
    // 컨텍스트 메뉴에서 진화 팝업 하위 참조를 다시 찾는다
    public void BindChildReferences()
    {
        Transform searchRoot = transform;
        oneBranchPanel = oneBranchPanel != null ? oneBranchPanel : FindChildGameObject(searchRoot, PANEL_A_PATH);
        twoBranchPanel = twoBranchPanel != null ? twoBranchPanel : FindChildGameObject(searchRoot, PANEL_B_PATH);
        fourBranchPanel = fourBranchPanel != null ? fourBranchPanel : FindChildGameObject(searchRoot, PANEL_C_PATH);
        evolutionButton = evolutionButton != null ? evolutionButton : FindFirstChildComponent<Button>(searchRoot, BACKGROUND_PATH + "/LowPanel/EvolutionFrame/EvolutionTextFrame", BACKGROUND_PATH + "/LowPanel/EvolutionFrame/Evolution");
        currentTurretNameText = currentTurretNameText != null ? currentTurretNameText : FindFirstChildComponent<TMP_Text>(searchRoot, BACKGROUND_PATH + "/HighPanel/CurrentTurretFrame/CurrentTurretName");
        nextTurretNameText = nextTurretNameText != null ? nextTurretNameText : FindFirstChildComponent<TMP_Text>(searchRoot, BACKGROUND_PATH + "/HighPanel/NextTurretFrame/NextTurretName");
        turretInfoPopup = turretInfoPopup != null ? turretInfoPopup : ResolveTurretInfoPopup(searchRoot);
        BindBranchPanelReferences();
        BindResourceSlotReferences(searchRoot);
    }

    // 진화 버튼 입력으로 현재 선택 후보 진화를 실행한다
    public void OnEvolutionButtonClicked()
    {
        Evolve(selectedEvolutionIndex);
    }

    // 현재 선택 터렛 기준으로 진화 표시 정보를 갱신한다
    private void RefreshEvolutionTexts()
    {
        if (!CurrentContext.IsValid)
        {
            selectedEvolutionIndex = 0;
            EvolutionBranchPanelData emptyPanel = SetActiveBranchPanel(0);
            RefreshCurrentTurretHeader(string.Empty);
            RefreshNextTurretHeader(string.Empty);
            SetCostTexts(System.Array.Empty<ResourceCost>());
            SetInteractable(false);
            ApplyCandidateSelectionHighlights(emptyPanel);
            return;
        }

        int availableCount = Mathf.Min(CurrentContext.Turret.GetAvailableEvolutionCount(), MAX_EVOLUTION_SLOT_COUNT);
        selectedEvolutionIndex = Mathf.Clamp(selectedEvolutionIndex, 0, Mathf.Max(0, availableCount - 1));
        EvolutionBranchPanelData activePanel = SetActiveBranchPanel(availableCount);
        RefreshCurrentTurretHeader(CurrentContext.GetDisplayName());
        RefreshCurrentTurretSlots(activePanel);
        RefreshEvolutionCandidateSlots(activePanel, availableCount);
        RefreshSelectedEvolutionDetails(activePanel);
    }

    // 상단 현재 터렛 이름 텍스트를 선택된 터렛 표시명으로 갱신한다
    private void RefreshCurrentTurretHeader(string displayName)
    {
        SetText(currentTurretNameText, ApplyNameTemplate(currentTurretNameTextTemplate, displayName));
    }

    // 상단 다음 터렛 이름 텍스트를 선택된 후보 표시명으로 갱신한다
    private void RefreshNextTurretHeader(string displayName)
    {
        SetText(nextTurretNameText, ApplyNameTemplate(nextTurretNameTextTemplate, displayName));
    }

    // TMP 원문 템플릿을 보관해 괄호와 고정 문구를 유지한다
    private void CacheTextTemplates()
    {
        if (currentTurretNameText != null && string.IsNullOrEmpty(currentTurretNameTextTemplate))
        {
            currentTurretNameTextTemplate = currentTurretNameText.text;
        }

        if (nextTurretNameText != null && string.IsNullOrEmpty(nextTurretNameTextTemplate))
        {
            nextTurretNameTextTemplate = nextTurretNameText.text;
        }
    }

    // 현재 선택된 후보 인덱스로 진화를 실행한다
    private void Evolve(int availableIndex)
    {
        if (!CurrentContext.IsValid)
        {
            return;
        }

        TurretDefinitionRuntimeController evolvedTurret = null;
        if (replacePrefabOnEvolution)
        {
            evolvedTurret = CurrentContext.Turret.TryCreateEvolvedInstance(availableIndex);
        }
        else if (CurrentContext.Turret.TryEvolve(availableIndex))
        {
            evolvedTurret = CurrentContext.Turret;
        }

        if (evolvedTurret == null)
        {
            RefreshEvolutionTexts();
            return;
        }

        TurretBaseSlot evolvedSlot = CurrentContext.Slot != null ? CurrentContext.Slot : evolvedTurret.GetComponentInParent<TurretBaseSlot>();
        if (evolvedSlot != null)
        {
            evolvedSlot.SetCurrentTurret(evolvedTurret);
        }

        CurrentContext = new TurretSelectionContext(evolvedTurret, evolvedSlot);
        RequestSelectionContextUpdate(CurrentContext);
        selectedEvolutionIndex = 0;
        RequestBackToSelectPopup();
    }

    // 분기 수에 맞는 패널만 활성화하고 해당 패널 데이터를 반환한다
    private EvolutionBranchPanelData SetActiveBranchPanel(int availableCount)
    {
        bool useOneBranchPanel = availableCount <= 1;
        bool useTwoBranchPanel = availableCount == 2;
        bool useFourBranchPanel = availableCount > 2;
        SetGameObjectActive(oneBranchPanel, useOneBranchPanel);
        SetGameObjectActive(twoBranchPanel, useTwoBranchPanel);
        SetGameObjectActive(fourBranchPanel, useFourBranchPanel);

        if (useTwoBranchPanel)
        {
            return CreatePanelData(twoBranchPanel);
        }

        if (useFourBranchPanel)
        {
            return CreatePanelData(fourBranchPanel);
        }

        return CreatePanelData(oneBranchPanel);
    }

    // 활성 패널의 현재 터렛 이름과 이미지를 갱신한다
    private void RefreshCurrentTurretSlots(EvolutionBranchPanelData panelData)
    {
        Sprite currentSprite = CurrentContext.Definition == null ? null : CurrentContext.Definition.uiIcon;
        SetText(panelData.CurrentTurretNameText, CurrentContext.GetDisplayName());
        SetImage(panelData.CurrentTurretImage, currentSprite);
    }

    // 활성 패널의 진화 후보 슬롯들을 갱신한다
    private void RefreshEvolutionCandidateSlots(EvolutionBranchPanelData panelData, int availableCount)
    {
        for (int i = 0; i < panelData.CandidateSlots.Length; i++)
        {
            EvolutionCandidateSlot slot = panelData.CandidateSlots[i];
            TurretEvolutionEntry entry = i < availableCount ? CurrentContext.Turret.GetAvailableEvolution(i) : null;
            RefreshEvolutionCandidateSlot(slot, entry, i);
        }
    }

    // 단일 진화 후보 슬롯의 이름, 이미지, 버튼 상태를 갱신한다
    private void RefreshEvolutionCandidateSlot(EvolutionCandidateSlot slot, TurretEvolutionEntry entry, int slotIndex)
    {
        bool hasEntry = entry != null && entry.targetDefinition != null;
        SetGameObjectActive(slot.Root, hasEntry);
        SetText(slot.NameText, hasEntry ? GetEvolutionName(entry) : string.Empty);
        SetImage(slot.Image, hasEntry ? GetEvolutionSprite(entry) : null);

        if (slot.Button != null)
        {
            slot.Button.onClick.RemoveListener(OnCandidate0Clicked);
            slot.Button.onClick.RemoveListener(OnCandidate1Clicked);
            slot.Button.onClick.RemoveListener(OnCandidate2Clicked);
            slot.Button.onClick.RemoveListener(OnCandidate3Clicked);
            slot.Button.onClick.AddListener(GetCandidateClickAction(slotIndex));
            slot.Button.interactable = hasEntry;
            BindCandidatePressForwarder(slot.Button, slotIndex);
        }
    }

    // 선택된 후보의 비용과 실행 가능 상태를 갱신한다
    private void RefreshSelectedEvolutionDetails(EvolutionBranchPanelData panelData)
    {
        TurretEvolutionEntry selectedEntry = CurrentContext.Turret.GetAvailableEvolution(selectedEvolutionIndex);
        RefreshNextTurretHeader(GetEvolutionName(selectedEntry));
        SetCostTexts(selectedEntry == null ? System.Array.Empty<ResourceCost>() : selectedEntry.evolutionCosts);
        SetInteractable(selectedEntry != null && CurrentContext.Turret.CanEvolve(selectedEvolutionIndex));
        ApplyCandidateSelectionHighlights(panelData);
    }

    // 분기 패널 안의 현재 터렛과 후보 슬롯 참조를 구성한다
    private EvolutionBranchPanelData CreatePanelData(GameObject panelObject)
    {
        if (panelObject == null)
        {
            return new EvolutionBranchPanelData(null, null, System.Array.Empty<EvolutionCandidateSlot>());
        }

        Transform panelTransform = panelObject.transform;
        TMP_Text currentNameText = FindFirstDescendantByExactName<TMP_Text>(panelTransform, "CurrentTurretName");
        Image currentImage = FindFirstDescendantByExactName<Image>(panelTransform, "CurrentTurretImage");
        EvolutionCandidateSlot[] candidateSlots = CreateCandidateSlots(panelTransform);
        return new EvolutionBranchPanelData(currentNameText, currentImage, candidateSlots);
    }

    // 분기 패널 안의 다음 진화 후보 슬롯들을 이름 규칙으로 찾는다
    private EvolutionCandidateSlot[] CreateCandidateSlots(Transform panelTransform)
    {
        EvolutionCandidateSlot[] slots = new EvolutionCandidateSlot[MAX_EVOLUTION_SLOT_COUNT];
        bool hasUnsuffixedSlot = FindFirstDescendantTransformByExactName(panelTransform, "NextTurretImageFrame") != null ||
                                 FindFirstDescendantTransformByExactName(panelTransform, "NextTurretImage") != null;
        for (int i = 0; i < MAX_EVOLUTION_SLOT_COUNT; i++)
        {
            string suffix = hasUnsuffixedSlot && i == 0 ? string.Empty : "_" + (i + 1);
            Transform frame = FindFirstDescendantTransformByExactName(panelTransform, "NextTurretImageFrame" + suffix);
            Transform imageTransform = FindFirstDescendantTransformByExactName(panelTransform, "NextTurretImage" + suffix);
            Transform searchRoot = frame != null ? frame : imageTransform;
            TMP_Text nameText = searchRoot == null ? null : FindFirstDescendantByExactName<TMP_Text>(searchRoot, "NextTurretName");
            Image image = imageTransform == null ? null : imageTransform.GetComponent<Image>();
            Image frameImage = frame == null ? null : frame.GetComponent<Image>();
            Button button = ResolveCandidateButton(searchRoot, imageTransform);
            slots[i] = new EvolutionCandidateSlot(searchRoot == null ? null : searchRoot.gameObject, frameImage, nameText, image, button);
        }

        return TrimEmptySlots(slots);
    }

    // 비어 있지 않은 후보 슬롯만 남긴 배열을 반환한다
    private static EvolutionCandidateSlot[] TrimEmptySlots(EvolutionCandidateSlot[] slots)
    {
        int count = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].Root != null || slots[i].FrameImage != null || slots[i].Image != null || slots[i].NameText != null || slots[i].Button != null)
            {
                count++;
            }
        }

        EvolutionCandidateSlot[] trimmedSlots = new EvolutionCandidateSlot[count];
        int writeIndex = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].Root == null && slots[i].FrameImage == null && slots[i].Image == null && slots[i].NameText == null && slots[i].Button == null)
            {
                continue;
            }

            trimmedSlots[writeIndex] = slots[i];
            writeIndex++;
        }

        return trimmedSlots;
    }

    // 후보 슬롯에서 직접 누를 버튼 컴포넌트를 찾는다
    private static Button ResolveCandidateButton(Transform frame, Transform imageTransform)
    {
        if (frame != null)
        {
            Button frameButton = frame.GetComponent<Button>();
            if (frameButton != null)
            {
                return frameButton;
            }

            Button childButton = frame.GetComponentInChildren<Button>(true);
            if (childButton != null)
            {
                return childButton;
            }
        }

        return imageTransform == null ? null : imageTransform.GetComponent<Button>();
    }

    // 모든 분기 패널의 공통 참조 배열과 후보 버튼 이벤트를 갱신한다
    private void BindBranchPanelReferences()
    {
        UnbindCandidateButtonListeners();
        EvolutionBranchPanelData onePanelData = CreatePanelData(oneBranchPanel);
        EvolutionBranchPanelData twoPanelData = CreatePanelData(twoBranchPanel);
        EvolutionBranchPanelData fourPanelData = CreatePanelData(fourBranchPanel);
        nextTurretFrameImages = CollectCandidateFrameImages(onePanelData, twoPanelData, fourPanelData);
        nextTurretFrameDefaultColors = CollectImageColors(nextTurretFrameImages);
        evolutionCandidateButtons = CollectCandidateButtons(onePanelData, twoPanelData, fourPanelData);
        BindCandidateButtonListeners();
    }

    // 후보 버튼 클릭 이벤트를 등록한다
    private void BindCandidateButtonListeners()
    {
        if (evolutionCandidateButtons == null)
        {
            return;
        }

        for (int i = 0; i < evolutionCandidateButtons.Length; i++)
        {
            Button candidateButton = evolutionCandidateButtons[i];
            if (candidateButton == null)
            {
                continue;
            }

            candidateButton.onClick.RemoveListener(OnCandidate0Clicked);
            candidateButton.onClick.RemoveListener(OnCandidate1Clicked);
            candidateButton.onClick.RemoveListener(OnCandidate2Clicked);
            candidateButton.onClick.RemoveListener(OnCandidate3Clicked);
        }
    }

    // 후보 버튼 클릭 이벤트를 해제한다
    private void UnbindCandidateButtonListeners()
    {
        if (evolutionCandidateButtons == null)
        {
            return;
        }

        for (int i = 0; i < evolutionCandidateButtons.Length; i++)
        {
            Button candidateButton = evolutionCandidateButtons[i];
            if (candidateButton == null)
            {
                continue;
            }

            candidateButton.onClick.RemoveListener(OnCandidate0Clicked);
            candidateButton.onClick.RemoveListener(OnCandidate1Clicked);
            candidateButton.onClick.RemoveListener(OnCandidate2Clicked);
            candidateButton.onClick.RemoveListener(OnCandidate3Clicked);
        }
    }

    // 후보 인덱스에 맞는 클릭 콜백을 반환한다
    private UnityEngine.Events.UnityAction GetCandidateClickAction(int index)
    {
        switch (index)
        {
            case 0:
                return OnCandidate0Clicked;
            case 1:
                return OnCandidate1Clicked;
            case 2:
                return OnCandidate2Clicked;
            case 3:
                return OnCandidate3Clicked;
            default:
                return OnCandidate0Clicked;
        }
    }

    // 첫 번째 진화 후보를 선택한다
    private void OnCandidate0Clicked()
    {
        SelectEvolutionCandidate(0);
    }

    // 두 번째 진화 후보를 선택한다
    private void OnCandidate1Clicked()
    {
        SelectEvolutionCandidate(1);
    }

    // 세 번째 진화 후보를 선택한다
    private void OnCandidate2Clicked()
    {
        SelectEvolutionCandidate(2);
    }

    // 네 번째 진화 후보를 선택한다
    private void OnCandidate3Clicked()
    {
        SelectEvolutionCandidate(3);
    }

    // 선택 인덱스를 갱신하고 해당 후보 비용을 표시한다
    private void SelectEvolutionCandidate(int index)
    {
        if (suppressNextCandidateClick)
        {
            suppressNextCandidateClick = false;
            return;
        }

        if (!CurrentContext.IsValid)
        {
            return;
        }

        int availableCount = Mathf.Min(CurrentContext.Turret.GetAvailableEvolutionCount(), MAX_EVOLUTION_SLOT_COUNT);
        if (index < 0 || index >= availableCount)
        {
            return;
        }

        if (index == selectedEvolutionIndex)
        {
            OpenEvolutionCandidateInfo(index);
            return;
        }

        selectedEvolutionIndex = index;
        EvolutionBranchPanelData activePanel = SetActiveBranchPanel(availableCount);
        RefreshSelectedEvolutionDetails(activePanel);
    }

    // 후보 버튼 누르기 시작 상태를 기록한다
    public void NotifyCandidatePointerDown(int index)
    {
        pressedCandidateIndex = index;
        pressedCandidateElapsedTime = 0.0f;
        hasOpenedInfoByHold = false;
    }

    // 후보 버튼 누르기 종료 상태를 초기화한다
    public void NotifyCandidatePointerUp(int index)
    {
        if (pressedCandidateIndex == index)
        {
            ClearCandidatePressState();
        }
    }

    // 후보 버튼 영역을 벗어나면 누르기 상태를 초기화한다
    public void NotifyCandidatePointerExit(int index)
    {
        if (pressedCandidateIndex == index)
        {
            ClearCandidatePressState();
        }
    }

    // 후보 버튼에 누르고 있기 전달 컴포넌트를 연결한다
    private void BindCandidatePressForwarder(Button candidateButton, int index)
    {
        if (candidateButton == null)
        {
            return;
        }

        TurretEvolutionCandidatePressForwarder forwarder = candidateButton.GetComponent<TurretEvolutionCandidatePressForwarder>();
        if (forwarder == null)
        {
            Debug.LogWarning("[TurretEvolutionPopupUI] 진화 후보 버튼에 TurretEvolutionCandidatePressForwarder가 없어 길게 누르기 정보 팝업을 사용할 수 없습니다.", candidateButton);
            return;
        }

        forwarder.Initialize(this, index);
    }

    // 현재 후보 누르기 상태를 초기화한다
    private void ClearCandidatePressState()
    {
        pressedCandidateIndex = -1;
        pressedCandidateElapsedTime = 0.0f;
        hasOpenedInfoByHold = false;
    }

    // 지정 진화 후보의 1레벨 정보 팝업을 표시한다
    private void OpenEvolutionCandidateInfo(int index)
    {
        if (!CurrentContext.IsValid)
        {
            return;
        }

        TurretEvolutionEntry entry = CurrentContext.Turret.GetAvailableEvolution(index);
        if (entry == null || entry.targetDefinition == null)
        {
            return;
        }

        if (turretInfoPopup != null)
        {
            turretInfoPopup.Show(entry.targetDefinition);
            return;
        }

        Debug.LogWarning("[TurretEvolutionPopupUI] Turret Info Popup 참조가 없어 진화 후보 정보를 열 수 없습니다.", this);
    }

    // 필요 재화 텍스트와 개별 슬롯을 갱신한다
    private void SetCostTexts(ResourceCost[] costs)
    {
        if (costs == null)
        {
            ClearResourceSlots(0);
            return;
        }

        int visibleIndex = 0;
        int slotCount = GetResourceSlotCount();
        for (int i = 0; i < costs.Length && visibleIndex < slotCount; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            SetResourceNameText(visibleIndex, GetCurrencyDisplayName(cost.currencyType));
            SetResourceCountText(visibleIndex, FormatCostAmountText(cost));
            SetImage(GetImageAt(resourceItemImages, visibleIndex), GetCurrencySprite(cost.currencyType));
            visibleIndex++;
        }

        ClearResourceSlots(visibleIndex);
    }

    // 선택된 후보 프레임만 강조 색상으로 표시한다
    private void ApplyCandidateSelectionHighlights(EvolutionBranchPanelData panelData)
    {
        EvolutionCandidateSlot[] slots = panelData.CandidateSlots;
        for (int i = 0; i < slots.Length; i++)
        {
            SetCandidateFrameSelected(slots[i], CanHighlightCandidateIndex(i));
        }
    }

    // 지정 후보 인덱스를 선택 하이라이트로 표시할 수 있는지 확인한다
    private bool CanHighlightCandidateIndex(int index)
    {
        if (!CurrentContext.IsValid || index != selectedEvolutionIndex)
        {
            return false;
        }

        int availableCount = Mathf.Min(CurrentContext.Turret.GetAvailableEvolutionCount(), MAX_EVOLUTION_SLOT_COUNT);
        return index >= 0 && index < availableCount;
    }

    // 후보 프레임의 선택 색상 또는 기본 색상을 적용한다
    private void SetCandidateFrameSelected(EvolutionCandidateSlot slot, bool isSelected)
    {
        if (slot.FrameImage == null)
        {
            return;
        }

        slot.FrameImage.color = isSelected ? selectedCandidateFrameColor : GetCandidateFrameDefaultColor(slot.FrameImage);
    }

    // 후보 프레임 이미지의 저장된 기본 색상을 반환한다
    private Color GetCandidateFrameDefaultColor(Image frameImage)
    {
        if (nextTurretFrameImages == null || nextTurretFrameDefaultColors == null)
        {
            return Color.white;
        }

        int count = Mathf.Min(nextTurretFrameImages.Length, nextTurretFrameDefaultColors.Length);
        for (int i = 0; i < count; i++)
        {
            if (nextTurretFrameImages[i] == frameImage)
            {
                return nextTurretFrameDefaultColors[i];
            }
        }

        return Color.white;
    }

    // 하단 진화 버튼 활성 상태를 적용한다
    private void SetInteractable(bool canEvolve)
    {
        if (evolutionButton != null)
        {
            evolutionButton.interactable = canEvolve;
        }
    }

    // 버튼 클릭 이벤트를 등록한다
    private void BindButtonListeners()
    {
        UnbindButtonListeners();

        if (evolutionButton != null)
        {
            evolutionButton.onClick.AddListener(OnEvolutionButtonClicked);
        }

        BindCandidateButtonListeners();
    }

    // 버튼 클릭 이벤트를 해제한다
    private void UnbindButtonListeners()
    {
        if (evolutionButton != null)
        {
            evolutionButton.onClick.RemoveListener(OnEvolutionButtonClicked);
        }

        UnbindCandidateButtonListeners();
    }

    // 개별 재화 이름, 수량, 이미지 배열을 하위 이름 패턴으로 구성한다
    private void BindResourceSlotReferences(Transform searchRoot)
    {
        Transform resourcePanel = searchRoot.Find(BACKGROUND_PATH + "/MiddleLowPanel/RequireSorceImagePanel");
        if (resourcePanel == null)
        {
            EnsureResourceArrays();
            return;
        }

        resourceItemNameTexts = new TMP_Text[MAX_RESOURCE_SLOT_COUNT];
        resourceItemCountTexts = new TMP_Text[MAX_RESOURCE_SLOT_COUNT];
        resourceItemImages = new Image[MAX_RESOURCE_SLOT_COUNT];
        resourceSlotFrames = new Transform[MAX_RESOURCE_SLOT_COUNT];
        resourceItemDefaultSprites = new Sprite[MAX_RESOURCE_SLOT_COUNT];
        for (int i = 0; i < MAX_RESOURCE_SLOT_COUNT; i++)
        {
            int slotNumber = i + 1;
            Transform frame = FindFirstDescendantTransformByExactName(resourcePanel, "RequireSorceImageFrame " + slotNumber);
            resourceSlotFrames[i] = frame;
            resourceItemNameTexts[i] = FindResourceSlotText(resourcePanel, frame, "ItemName", slotNumber);
            resourceItemCountTexts[i] = FindResourceSlotText(resourcePanel, frame, "ItemCount", slotNumber);
            resourceItemImages[i] = FindResourceSlotImage(frame);
            resourceItemDefaultSprites[i] = resourceItemImages[i] == null ? null : resourceItemImages[i].sprite;
        }

        EnsureResourceArrays();
    }

    // 진화 엔트리 표시 이름을 반환한다
    private static string GetEvolutionName(TurretEvolutionEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        TurretDefinitionSO targetDefinition = entry.targetDefinition;
        if (targetDefinition != null && !string.IsNullOrWhiteSpace(targetDefinition.displayName))
        {
            return targetDefinition.displayName;
        }

        if (targetDefinition != null)
        {
            return targetDefinition.name;
        }

        return string.IsNullOrWhiteSpace(entry.displayName) ? string.Empty : entry.displayName;
    }

    // 진화 엔트리의 대표 이미지를 반환한다
    private static Sprite GetEvolutionSprite(TurretEvolutionEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        if (entry.targetDefinition != null && entry.targetDefinition.uiIcon != null)
        {
            return entry.targetDefinition.uiIcon;
        }

        return entry.evolutionIcon;
    }

    // 재화 타입을 UI 표시 이름으로 변환한다
    private static string GetCurrencyLabel(RewardCurrencyType currencyType)
    {
        switch (currencyType)
        {
            case RewardCurrencyType.Coin:
                return "Coin";
            default:
                return currencyType.ToString();
        }
    }

    // 재화 타입을 인벤토리 메타데이터 표시 이름으로 변환한다
    private static string GetCurrencyDisplayName(RewardCurrencyType currencyType)
    {
        if (InventorySystem.Inst != null)
        {
            string itemName = InventorySystem.Inst.GetName(currencyType);
            if (!string.IsNullOrWhiteSpace(itemName))
            {
                return itemName;
            }
        }

        return GetCurrencyLabel(currencyType);
    }

    // 재화 타입을 인벤토리 메타데이터 이미지로 변환한다
    private static Sprite GetCurrencySprite(RewardCurrencyType currencyType)
    {
        if (InventorySystem.Inst == null)
        {
            return null;
        }

        ItemMetaDataSo metadata = InventorySystem.Inst.GetMetaData(currencyType);
        return metadata == null ? null : metadata.ItemImage;
    }

    // 비용 수량을 보유량과 요구량 형식으로 변환하고 부족하면 붉은색을 적용한다
    private static string FormatCostAmountText(ResourceCost cost)
    {
        if (cost == null)
        {
            return string.Empty;
        }

        int requiredAmount = Mathf.Max(0, cost.amount);
        if (InventorySystem.Inst == null)
        {
            return "0/" + requiredAmount;
        }

        string ownedAmount = InventorySystem.Inst.GetCountString(cost.currencyType);
        string amountText = ownedAmount + "/" + requiredAmount;
        if (!InventorySystem.Inst.CanUseItem(cost.currencyType, requiredAmount))
        {
            return "<color=" + INSUFFICIENT_COST_COLOR + ">" + amountText + "</color>";
        }

        return amountText;
    }

    // 지정 인덱스 이후 재화 슬롯 표시를 비운다
    private void ClearResourceSlots(int startIndex)
    {
        int slotCount = GetResourceSlotCount();
        for (int i = startIndex; i < slotCount; i++)
        {
            SetResourceNameText(i, string.Empty);
            SetResourceCountText(i, string.Empty);
            SetImage(GetImageAt(resourceItemImages, i), GetSpriteAt(resourceItemDefaultSprites, i));
        }
    }

    // 재화 슬롯 배열 중 사용할 수 있는 최대 슬롯 수를 반환한다
    private int GetResourceSlotCount()
    {
        int count = resourceItemCountTexts == null ? 0 : resourceItemCountTexts.Length;
        if (resourceItemNameTexts != null && resourceItemNameTexts.Length > count)
        {
            count = resourceItemNameTexts.Length;
        }

        if (resourceItemImages != null && resourceItemImages.Length > count)
        {
            count = resourceItemImages.Length;
        }

        return count;
    }

    // 재화 배열 null 상태를 빈 배열로 보정한다
    private void EnsureResourceArrays()
    {
        resourceItemNameTexts = resourceItemNameTexts ?? System.Array.Empty<TMP_Text>();
        resourceItemCountTexts = resourceItemCountTexts ?? System.Array.Empty<TMP_Text>();
        resourceItemImages = resourceItemImages ?? System.Array.Empty<Image>();
        resourceSlotFrames = resourceSlotFrames ?? System.Array.Empty<Transform>();
        resourceItemDefaultSprites = resourceItemDefaultSprites ?? System.Array.Empty<Sprite>();
    }

    // 재화 기본 스프라이트가 비어 있으면 현재 이미지 스프라이트를 기본값으로 저장한다
    private void CacheResourceDefaultSprites()
    {
        if (resourceItemImages == null)
        {
            return;
        }

        if (resourceItemDefaultSprites == null || resourceItemDefaultSprites.Length != resourceItemImages.Length)
        {
            resourceItemDefaultSprites = new Sprite[resourceItemImages.Length];
        }

        for (int i = 0; i < resourceItemImages.Length; i++)
        {
            if (resourceItemDefaultSprites[i] == null && resourceItemImages[i] != null)
            {
                resourceItemDefaultSprites[i] = resourceItemImages[i].sprite;
            }
        }
    }

    // 진화 팝업에 필요한 수동 연결 참조를 검증한다
    private void ValidateRequiredReferences()
    {
        if (oneBranchPanel == null || twoBranchPanel == null || fourBranchPanel == null)
        {
            Debug.LogWarning("[TurretEvolutionPopupUI] 진화 분기 패널 참조가 일부 비어 있습니다.", this);
        }

        if (turretInfoPopup == null)
        {
            Debug.LogWarning("[TurretEvolutionPopupUI] Turret Info Popup 참조가 비어 있습니다.", this);
        }

        if (evolutionButton == null)
        {
            Debug.LogWarning("[TurretEvolutionPopupUI] Evolution Button 참조가 비어 있습니다.", this);
        }

        if (currentTurretNameText == null || nextTurretNameText == null)
        {
            Debug.LogWarning("[TurretEvolutionPopupUI] 현재/다음 터렛 이름 TMP 참조가 일부 비어 있습니다.", this);
        }

        if (resourceItemCountTexts == null || resourceItemCountTexts.Length == 0)
        {
            Debug.LogWarning("[TurretEvolutionPopupUI] 진화 비용 수량 TMP 배열이 비어 있습니다.", this);
        }
    }

    // 지정 배열에서 안전하게 텍스트 참조를 얻는다
    private static TMP_Text GetTextAt(TMP_Text[] texts, int index)
    {
        return texts != null && index >= 0 && index < texts.Length ? texts[index] : null;
    }

    // 지정 배열에서 안전하게 이미지 참조를 얻는다
    private static Image GetImageAt(Image[] images, int index)
    {
        return images != null && index >= 0 && index < images.Length ? images[index] : null;
    }

    // 지정 배열에서 안전하게 스프라이트 참조를 얻는다
    private static Sprite GetSpriteAt(Sprite[] sprites, int index)
    {
        return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }

    // 재료 이름 슬롯과 같은 프레임 안의 이름 텍스트를 함께 갱신한다
    private void SetResourceNameText(int slotIndex, string value)
    {
        TMP_Text primaryText = GetTextAt(resourceItemNameTexts, slotIndex);
        SetText(primaryText, value);
        SetFrameTextsByPrefix(GetTransformAt(resourceSlotFrames, slotIndex), "ItemName", value);
    }

    // 재료 수량 슬롯과 같은 프레임 안의 수량 텍스트를 함께 갱신한다
    private void SetResourceCountText(int slotIndex, string value)
    {
        TMP_Text primaryText = GetTextAt(resourceItemCountTexts, slotIndex);
        EnableRichText(primaryText);
        SetText(primaryText, value);
        SetFrameTextsByPrefix(GetTransformAt(resourceSlotFrames, slotIndex), "ItemCount", value);
    }

    // 지정 배열에서 안전하게 Transform 참조를 얻는다
    private static Transform GetTransformAt(Transform[] transforms, int index)
    {
        return transforms != null && index >= 0 && index < transforms.Length ? transforms[index] : null;
    }

    // 프레임 안의 지정 접두사 텍스트를 모두 갱신한다
    private static void SetFrameTextsByPrefix(Transform frame, string namePrefix, string value)
    {
        if (frame == null || string.IsNullOrEmpty(namePrefix))
        {
            return;
        }

        TMP_Text[] texts = frame.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null || !text.name.StartsWith(namePrefix, System.StringComparison.Ordinal))
            {
                continue;
            }

            EnableRichText(text);
            SetText(text, value);
        }
    }

    // 텍스트 참조에 리치 텍스트 색상 태그 사용을 허용한다
    private static void EnableRichText(TMP_Text targetText)
    {
        if (targetText != null)
        {
            targetText.richText = true;
        }
    }

    // 재료 슬롯에서 번호가 붙은 텍스트와 프레임 내부 기본 텍스트를 순서대로 찾는다
    private static TMP_Text FindResourceSlotText(Transform resourcePanel, Transform frame, string baseName, int slotNumber)
    {
        TMP_Text text = FindFirstDescendantByExactName<TMP_Text>(frame, baseName + " " + slotNumber);
        if (text != null)
        {
            return text;
        }

        text = FindFirstDescendantByExactName<TMP_Text>(frame, baseName);
        if (text != null)
        {
            return text;
        }

        return FindFirstDescendantByExactName<TMP_Text>(resourcePanel, baseName + " " + slotNumber);
    }

    // 재료 프레임 아래에서 실제 아이템 아이콘 이미지를 찾는다
    private static Image FindResourceSlotImage(Transform frame)
    {
        if (frame == null)
        {
            return null;
        }

        Image[] images = frame.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && image.transform != frame)
            {
                return image;
            }
        }

        return frame.GetComponent<Image>();
    }

    // 이미지 참조에 스프라이트를 적용한다
    private static void SetImage(Image targetImage, Sprite sprite)
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

    // 텍스트 참조가 있을 때만 문자열을 적용한다
    private static void SetText(TMP_Text targetText, string value)
    {
        if (targetText != null)
        {
            targetText.text = value;
        }
    }

    // 템플릿 안의 이름 자리표시자를 실제 터렛 이름으로 교체한다
    private static string ApplyNameTemplate(string template, string value)
    {
        if (string.IsNullOrEmpty(template))
        {
            return value;
        }

        if (TryApplyDelimitedTemplate(template, value, '{', '}', out string braceResult))
        {
            return braceResult;
        }

        if (TryApplyDelimitedTemplate(template, value, '[', ']', out string bracketResult))
        {
            return bracketResult;
        }

        return value;
    }

    // 지정 구분자 사이의 텍스트를 실제 값으로 교체한다
    private static bool TryApplyDelimitedTemplate(string template, string value, char openToken, char closeToken, out string result)
    {
        result = value;
        int openIndex = template.IndexOf(openToken);
        int closeIndex = template.IndexOf(closeToken, openIndex + 1);
        if (openIndex < 0 || closeIndex < 0 || closeIndex <= openIndex)
        {
            return false;
        }

        result = template.Substring(0, openIndex + 1) + value + template.Substring(closeIndex);
        return true;
    }

    // 지정 오브젝트의 활성 상태를 안전하게 변경한다
    private static void SetGameObjectActive(GameObject targetObject, bool isActive)
    {
        if (targetObject != null && targetObject.activeSelf != isActive)
        {
            targetObject.SetActive(isActive);
        }
    }

    // 지정 경로의 하위 게임 오브젝트를 찾는다
    private static GameObject FindChildGameObject(Transform searchRoot, string childPath)
    {
        Transform child = FindChildTransform(searchRoot, childPath);
        return child == null ? null : child.gameObject;
    }

    // 지정 경로의 하위 컴포넌트를 찾는다
    private static T FindChildComponent<T>(Transform searchRoot, string childPath) where T : Component
    {
        Transform child = FindChildTransform(searchRoot, childPath);
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

    // 지정 경로의 하위 Transform을 찾는다
    private static Transform FindChildTransform(Transform searchRoot, string childPath)
    {
        if (searchRoot == null || string.IsNullOrWhiteSpace(childPath))
        {
            return null;
        }

        return searchRoot.Find(childPath);
    }

    // 하위 오브젝트 중 정확한 이름의 첫 Transform을 찾는다
    private static Transform FindFirstDescendantTransformByExactName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        Transform[] candidates = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            Transform candidate = candidates[i];
            if (candidate != null && candidate.name == targetName)
            {
                return candidate;
            }
        }

        return null;
    }

    // 하위 오브젝트 중 정확한 이름의 첫 컴포넌트를 찾는다
    private static T FindFirstDescendantByExactName<T>(Transform root, string targetName) where T : Component
    {
        Transform targetTransform = FindFirstDescendantTransformByExactName(root, targetName);
        return targetTransform == null ? null : targetTransform.GetComponent<T>();
    }

    // 씬에 배치된 터렛 정보 팝업 컴포넌트를 찾는다
    private static TurretInfoPopupUI ResolveTurretInfoPopup(Transform searchRoot)
    {
        Transform root = searchRoot == null ? null : searchRoot.root;
        return root == null ? null : root.GetComponentInChildren<TurretInfoPopupUI>(true);
    }

    // 세 패널의 후보 프레임 이미지 참조를 모은다
    private static Image[] CollectCandidateFrameImages(params EvolutionBranchPanelData[] panelDataArray)
    {
        Image[] images = new Image[CountCandidateSlots(panelDataArray)];
        int writeIndex = 0;
        for (int i = 0; i < panelDataArray.Length; i++)
        {
            EvolutionCandidateSlot[] slots = panelDataArray[i].CandidateSlots;
            for (int j = 0; j < slots.Length; j++)
            {
                images[writeIndex] = slots[j].FrameImage;
                writeIndex++;
            }
        }

        return images;
    }

    // 이미지 배열의 현재 색상을 기본 색상 배열로 복사한다
    private static Color[] CollectImageColors(Image[] images)
    {
        if (images == null)
        {
            return System.Array.Empty<Color>();
        }

        Color[] colors = new Color[images.Length];
        for (int i = 0; i < images.Length; i++)
        {
            colors[i] = images[i] == null ? Color.white : images[i].color;
        }

        return colors;
    }

    // 세 패널의 후보 버튼 참조를 모은다
    private static Button[] CollectCandidateButtons(params EvolutionBranchPanelData[] panelDataArray)
    {
        Button[] buttons = new Button[CountCandidateSlots(panelDataArray)];
        int writeIndex = 0;
        for (int i = 0; i < panelDataArray.Length; i++)
        {
            EvolutionCandidateSlot[] slots = panelDataArray[i].CandidateSlots;
            for (int j = 0; j < slots.Length; j++)
            {
                buttons[writeIndex] = slots[j].Button;
                writeIndex++;
            }
        }

        return buttons;
    }

    // 세 패널의 후보 슬롯 총 개수를 반환한다
    private static int CountCandidateSlots(EvolutionBranchPanelData[] panelDataArray)
    {
        int count = 0;
        for (int i = 0; i < panelDataArray.Length; i++)
        {
            count += panelDataArray[i].CandidateSlots.Length;
        }

        return count;
    }

    // 분기 패널에 필요한 참조 묶음을 보관한다
    private readonly struct EvolutionBranchPanelData
    {
        public readonly TMP_Text CurrentTurretNameText;
        public readonly Image CurrentTurretImage;
        public readonly EvolutionCandidateSlot[] CandidateSlots;

        // 분기 패널 참조 묶음을 초기화한다
        public EvolutionBranchPanelData(TMP_Text currentTurretNameText, Image currentTurretImage, EvolutionCandidateSlot[] candidateSlots)
        {
            CurrentTurretNameText = currentTurretNameText;
            CurrentTurretImage = currentTurretImage;
            CandidateSlots = candidateSlots ?? System.Array.Empty<EvolutionCandidateSlot>();
        }
    }

    // 진화 후보 하나의 표시와 클릭 참조를 보관한다
    private readonly struct EvolutionCandidateSlot
    {
        public readonly GameObject Root;
        public readonly Image FrameImage;
        public readonly TMP_Text NameText;
        public readonly Image Image;
        public readonly Button Button;

        // 진화 후보 슬롯 참조 묶음을 초기화한다
        public EvolutionCandidateSlot(GameObject root, Image frameImage, TMP_Text nameText, Image image, Button button)
        {
            Root = root;
            FrameImage = frameImage;
            NameText = nameText;
            Image = image;
            Button = button;
        }
    }
}
