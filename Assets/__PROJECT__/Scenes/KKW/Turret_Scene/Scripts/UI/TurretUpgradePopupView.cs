using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 새 터렛 업그레이드 팝업의 순수 표시와 버튼 이벤트 전달을 담당한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretUpgradePopupView : MonoBehaviour
{
    [Header("루트")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backgroundButton;

    [Header("상단 텍스트")]
    [SerializeField] private TMP_Text turretNameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text statusText;

    [Header("스탯")]
    [SerializeField] private TurretUpgradeStatRowView[] statRows = Array.Empty<TurretUpgradeStatRowView>();

    [Header("액션")]
    [SerializeField] private TurretUpgradeActionButtonView upgradeButton;
    [SerializeField] private TurretUpgradeActionButtonView[] evolutionButtons = Array.Empty<TurretUpgradeActionButtonView>();

    [Header("엔지니어")]
    [SerializeField] private GameObject engineerSeatRoot;
    [SerializeField] private TurretEngineerSeatEntryView[] engineerSeatEntries = Array.Empty<TurretEngineerSeatEntryView>();

    public event Action UpgradeRequested;
    public event Action<int> EvolutionRequested;
    public event Action<int> EngineerDismountRequested;
    public event Action CloseRequested;

    private bool isBound;

    // 컴포넌트 추가 시 하위 View 참조를 자동으로 수집한다
    private void Reset()
    {
        AutoBindReferences();
    }

    // 활성화 전에 참조를 보완하고 버튼 이벤트를 연결한다
    private void Awake()
    {
        AutoBindReferences();
        BindStaticButtons();
        Hide();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindStaticButtons();
    }

    // 팝업 전체를 표시하고 최신 모델을 렌더링한다
    public void Show(TurretUpgradePopupViewModel model)
    {
        SetRootActive(true);
        Render(model);
    }

    // 팝업 표시 데이터만 갱신한다
    public void Render(TurretUpgradePopupViewModel model)
    {
        if (model == null)
        {
            Hide();
            return;
        }

        SetText(turretNameText, model.TurretName);
        SetText(levelText, model.LevelText);
        SetText(statusText, model.StatusText);
        RenderStats(model.Stats);
        RenderActions(model.UpgradeAction, model.EvolutionActions);
        RenderEngineerSeats(model.EngineerSeats);
    }

    // 팝업 전체를 숨긴다
    public void Hide()
    {
        SetRootActive(false);
    }

    // 스탯 행 배열에 모델을 순서대로 적용한다
    private void RenderStats(TurretUpgradeStatViewModel[] stats)
    {
        int rowCount = statRows == null ? 0 : statRows.Length;
        int modelCount = stats == null ? 0 : stats.Length;
        for (int i = 0; i < rowCount; i++)
        {
            TurretUpgradeStatRowView row = statRows[i];
            if (row == null)
            {
                continue;
            }

            row.Render(i < modelCount ? stats[i] : null);
        }
    }

    // 업그레이드와 진화 버튼 배열에 모델을 적용한다
    private void RenderActions(TurretUpgradeActionViewModel upgradeAction, TurretUpgradeActionViewModel[] evolutionActions)
    {
        if (upgradeButton != null)
        {
            upgradeButton.Render(upgradeAction, OnActionClicked);
        }

        int buttonCount = evolutionButtons == null ? 0 : evolutionButtons.Length;
        int modelCount = evolutionActions == null ? 0 : evolutionActions.Length;
        for (int i = 0; i < buttonCount; i++)
        {
            TurretUpgradeActionButtonView button = evolutionButtons[i];
            if (button == null)
            {
                continue;
            }

            button.Render(i < modelCount ? evolutionActions[i] : null, OnActionClicked);
        }
    }

    // 엔지니어 좌석 배열에 모델을 적용한다
    private void RenderEngineerSeats(TurretEngineerSeatViewModel[] engineerSeats)
    {
        int entryCount = engineerSeatEntries == null ? 0 : engineerSeatEntries.Length;
        int modelCount = engineerSeats == null ? 0 : engineerSeats.Length;
        bool hasVisibleSeat = false;

        for (int i = 0; i < entryCount; i++)
        {
            TurretEngineerSeatEntryView entry = engineerSeatEntries[i];
            if (entry == null)
            {
                continue;
            }

            TurretEngineerSeatViewModel model = i < modelCount ? engineerSeats[i] : null;
            entry.Render(model, OnEngineerSeatClicked);
            hasVisibleSeat |= model != null && model.IsVisible;
        }

        if (engineerSeatRoot != null)
        {
            engineerSeatRoot.SetActive(hasVisibleSeat);
        }
    }

    // 액션 버튼 클릭을 타입별 외부 이벤트로 변환한다
    private void OnActionClicked(TurretUpgradeActionType actionType, int actionIndex)
    {
        if (actionType == TurretUpgradeActionType.Upgrade)
        {
            UpgradeRequested?.Invoke();
            return;
        }

        EvolutionRequested?.Invoke(actionIndex);
    }

    // 엔지니어 좌석 클릭을 외부 이벤트로 전달한다
    private void OnEngineerSeatClicked(int seatIndex)
    {
        EngineerDismountRequested?.Invoke(seatIndex);
    }

    // 닫기 버튼 입력을 외부 이벤트로 전달한다
    private void OnCloseButtonClicked()
    {
        CloseRequested?.Invoke();
    }

    // 정적 버튼 이벤트를 중복 없이 연결한다
    private void BindStaticButtons()
    {
        if (isBound)
        {
            return;
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }

        if (backgroundButton != null)
        {
            backgroundButton.onClick.AddListener(OnCloseButtonClicked);
        }

        isBound = true;
    }

    // 정적 버튼 이벤트를 해제한다
    private void UnbindStaticButtons()
    {
        if (!isBound)
        {
            return;
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        }

        if (backgroundButton != null)
        {
            backgroundButton.onClick.RemoveListener(OnCloseButtonClicked);
        }

        isBound = false;
    }

    // 주요 하위 View와 텍스트 참조를 자동으로 수집한다
    private void AutoBindReferences()
    {
        popupRoot = popupRoot != null ? popupRoot : gameObject;
        statRows = statRows != null && statRows.Length > 0 ? statRows : GetComponentsInChildren<TurretUpgradeStatRowView>(true);
        AutoBindActionButtons();
        engineerSeatEntries = engineerSeatEntries != null && engineerSeatEntries.Length > 0 ? engineerSeatEntries : GetComponentsInChildren<TurretEngineerSeatEntryView>(true);
        AutoBindTexts();
    }

    // 액션 버튼 참조를 업그레이드와 진화 버튼 배열로 분리한다
    private void AutoBindActionButtons()
    {
        if (upgradeButton != null && evolutionButtons != null && evolutionButtons.Length > 0)
        {
            return;
        }

        TurretUpgradeActionButtonView[] actionButtons = GetComponentsInChildren<TurretUpgradeActionButtonView>(true);
        if (upgradeButton == null)
        {
            upgradeButton = FindActionButtonByName(actionButtons, "Upgrade");
        }

        if (upgradeButton == null && actionButtons.Length > 0)
        {
            upgradeButton = actionButtons[0];
        }

        if (evolutionButtons == null || evolutionButtons.Length == 0)
        {
            evolutionButtons = CollectEvolutionButtons(actionButtons, upgradeButton);
        }
    }

    // 이름 일부가 일치하는 액션 버튼을 찾는다
    private static TurretUpgradeActionButtonView FindActionButtonByName(TurretUpgradeActionButtonView[] buttons, string namePart)
    {
        if (buttons == null)
        {
            return null;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            TurretUpgradeActionButtonView button = buttons[i];
            if (button != null && button.name.Contains(namePart))
            {
                return button;
            }
        }

        return null;
    }

    // 업그레이드 버튼을 제외한 액션 버튼 배열을 만든다
    private static TurretUpgradeActionButtonView[] CollectEvolutionButtons(TurretUpgradeActionButtonView[] buttons, TurretUpgradeActionButtonView excludedButton)
    {
        if (buttons == null || buttons.Length == 0)
        {
            return Array.Empty<TurretUpgradeActionButtonView>();
        }

        int count = 0;
        for (int i = 0; i < buttons.Length; i++)
        {
            TurretUpgradeActionButtonView button = buttons[i];
            if (button != null && button != excludedButton)
            {
                count++;
            }
        }

        TurretUpgradeActionButtonView[] result = new TurretUpgradeActionButtonView[count];
        int writeIndex = 0;
        for (int i = 0; i < buttons.Length; i++)
        {
            TurretUpgradeActionButtonView button = buttons[i];
            if (button == null || button == excludedButton)
            {
                continue;
            }

            result[writeIndex] = button;
            writeIndex++;
        }

        return result;
    }

    // 이름 기반으로 상단 텍스트 참조를 자동으로 보완한다
    private void AutoBindTexts()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        turretNameText = turretNameText != null ? turretNameText : GetTextByName(texts, "Name");
        levelText = levelText != null ? levelText : GetTextByName(texts, "Level");
        statusText = statusText != null ? statusText : GetTextByName(texts, "Status");
    }

    // 이름 일부가 일치하는 텍스트를 반환한다
    private static TMP_Text GetTextByName(TMP_Text[] texts, string namePart)
    {
        if (texts == null)
        {
            return null;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text != null && text.name.Contains(namePart))
            {
                return text;
            }
        }

        return null;
    }

    // 루트 오브젝트 활성 상태를 변경한다
    private void SetRootActive(bool isActive)
    {
        GameObject targetRoot = popupRoot != null ? popupRoot : gameObject;
        targetRoot.SetActive(isActive);
    }

    // TMP_Text가 있을 때만 문자열을 대입한다
    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value ?? string.Empty;
        }
    }
}
