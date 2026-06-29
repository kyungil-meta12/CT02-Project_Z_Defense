using System;
using System.Text;
using UnityEngine;

/// <summary>
/// 선택된 터렛의 업그레이드 팝업 데이터를 만들고 View 입력을 런타임 성장 명령으로 변환한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TurretUpgradePopupPresenter : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private TurretUpgradePopupView view;

    [Header("업그레이드 설정")]
    [SerializeField, Min(1)] private int levelUpAmount = 1;
    [SerializeField] private bool replacePrefabOnEvolution = true;
    [SerializeField] private bool spendCurrency = true;

    private readonly TurretUpgradeStatViewModel[] statBuffer = new TurretUpgradeStatViewModel[6];
    private readonly TurretUpgradeActionViewModel[] evolutionBuffer = new TurretUpgradeActionViewModel[4];
    private readonly TurretEngineerSeatViewModel[] engineerSeatBuffer = new TurretEngineerSeatViewModel[4];
    private readonly StringBuilder textBuilder = new StringBuilder(96);

    private TurretDefinitionRuntimeController selectedTurret;
    private TurretBaseSlot selectedSlot;
    private TurretEngineerBuffReceiver selectedBuffReceiver;

    // 컴포넌트 추가 시 같은 오브젝트의 View 참조를 자동으로 찾는다
    private void Reset()
    {
        AutoBindReferences();
    }

    // 시작 전에 View 참조와 이벤트 연결을 준비한다
    private void Awake()
    {
        AutoBindReferences();
        BindViewEvents();
        Hide();
    }

    // 활성화될 때 엔지니어 버프 변경 이벤트를 구독한다
    private void OnEnable()
    {
        TurretEngineerBuffReceiver.OnBuffStateChanged += OnEngineerBuffStateChanged;
    }

    // 비활성화될 때 엔지니어 버프 변경 이벤트를 해제한다
    private void OnDisable()
    {
        TurretEngineerBuffReceiver.OnBuffStateChanged -= OnEngineerBuffStateChanged;
    }

    // 파괴 시 View 이벤트 구독을 정리한다
    private void OnDestroy()
    {
        UnbindViewEvents();
    }

    // 지정 터렛과 슬롯을 대상으로 팝업을 표시한다
    public void Show(TurretDefinitionRuntimeController turret, TurretBaseSlot slot)
    {
        selectedTurret = turret;
        selectedSlot = slot;
        selectedBuffReceiver = selectedTurret == null ? null : selectedTurret.GetComponent<TurretEngineerBuffReceiver>();

        if (selectedTurret == null)
        {
            Hide();
            return;
        }

        Refresh();
    }

    // 현재 선택을 유지한 채 팝업 표시 내용을 다시 만든다
    public void Refresh()
    {
        if (view == null || selectedTurret == null)
        {
            return;
        }

        TurretUpgradePopupViewModel viewModel = BuildViewModel();
        view.Show(viewModel);
    }

    // 팝업을 숨기고 현재 선택을 해제한다
    public void Hide()
    {
        selectedTurret = null;
        selectedSlot = null;
        selectedBuffReceiver = null;

        if (view != null)
        {
            view.Hide();
        }
    }

    // 현재 선택된 터렛 기준으로 전체 ViewModel을 생성한다
    private TurretUpgradePopupViewModel BuildViewModel()
    {
        TurretDefinitionSO definition = selectedTurret.CurrentTurretDefinition;
        int currentLevel = selectedTurret.CurrentTierLevel;
        int maxLevel = selectedTurret.CurrentMaxTierLevel;
        int targetLevel = maxLevel > 0 ? Mathf.Min(maxLevel, currentLevel + levelUpAmount) : currentLevel + levelUpAmount;
        TurretRuntimeStat currentStat = TurretStatCalculator.Calculate(definition, currentLevel);
        TurretRuntimeStat nextStat = TurretStatCalculator.Calculate(definition, targetLevel);
        BuildStats(currentStat, nextStat, currentLevel, targetLevel);
        TurretUpgradeActionViewModel upgradeAction = BuildUpgradeAction();
        int evolutionCount = BuildEvolutionActions();
        int engineerSeatCount = BuildEngineerSeats();

        return new TurretUpgradePopupViewModel(
            ResolveTurretName(definition),
            BuildLevelText(currentLevel, selectedTurret.CurrentTotalLevel, maxLevel),
            BuildStatusText(),
            CopyStats(),
            upgradeAction,
            CopyEvolutionActions(evolutionCount),
            CopyEngineerSeats(engineerSeatCount));
    }

    // 현재 스탯과 다음 스탯 비교 행을 만든다
    private int BuildStats(TurretRuntimeStat currentStat, TurretRuntimeStat nextStat, int currentLevel, int targetLevel)
    {
        bool canCompareNext = targetLevel > currentLevel;
        statBuffer[0] = CreateFloatStat("공격력", currentStat.damage, nextStat.damage, canCompareNext, false);
        statBuffer[1] = CreateFloatStat("사거리", currentStat.range, nextStat.range, canCompareNext, false);
        statBuffer[2] = CreateFloatStat("공격 간격", currentStat.fireInterval, nextStat.fireInterval, canCompareNext, true);
        statBuffer[3] = CreateFloatStat("투사체 속도", currentStat.projectileSpeed, nextStat.projectileSpeed, canCompareNext, false);
        statBuffer[4] = CreateIntStat("투사체 수", currentStat.projectileCount, nextStat.projectileCount, canCompareNext, false);
        statBuffer[5] = CreateIntStat("관통 수", currentStat.pierceCount, nextStat.pierceCount, canCompareNext, false);
        return statBuffer.Length;
    }

    // 소수점 스탯 표시 행을 만든다
    private static TurretUpgradeStatViewModel CreateFloatStat(string label, float currentValue, float nextValue, bool canCompareNext, bool lowerIsBetter)
    {
        float delta = nextValue - currentValue;
        bool hasDelta = canCompareNext && !Mathf.Approximately(delta, 0.0f);
        bool hasPositiveDelta = hasDelta && (lowerIsBetter ? delta < 0.0f : delta > 0.0f);
        string deltaText = hasDelta ? FormatDelta(delta) : string.Empty;
        return new TurretUpgradeStatViewModel(label, FormatNumber(currentValue), canCompareNext ? FormatNumber(nextValue) : "-", deltaText, hasPositiveDelta);
    }

    // 정수 스탯 표시 행을 만든다
    private static TurretUpgradeStatViewModel CreateIntStat(string label, int currentValue, int nextValue, bool canCompareNext, bool lowerIsBetter)
    {
        int delta = nextValue - currentValue;
        bool hasDelta = canCompareNext && delta != 0;
        bool hasPositiveDelta = hasDelta && (lowerIsBetter ? delta < 0 : delta > 0);
        string deltaText = hasDelta ? FormatSignedInteger(delta) : string.Empty;
        return new TurretUpgradeStatViewModel(label, currentValue.ToString(), canCompareNext ? nextValue.ToString() : "-", deltaText, hasPositiveDelta);
    }

    // 일반 업그레이드 버튼 모델을 만든다
    private TurretUpgradeActionViewModel BuildUpgradeAction()
    {
        if (selectedTurret.GetAvailableEvolutionCount() > 0 || selectedTurret.IsMaxTierLevelReached)
        {
            return new TurretUpgradeActionViewModel(TurretUpgradeActionType.Upgrade, 0, "업그레이드", "-", "진화 가능", null, false, false);
        }

        ResourceCost[] costs = selectedTurret.GetUpgradeCosts(levelUpAmount);
        bool canUpgrade = spendCurrency ? selectedTurret.CanUpgrade(levelUpAmount) : levelUpAmount > 0;
        string stateText = canUpgrade ? string.Empty : "업그레이드 불가";
        return new TurretUpgradeActionViewModel(TurretUpgradeActionType.Upgrade, 0, $"업그레이드 +{levelUpAmount}", FormatCosts(costs), stateText, null, true, canUpgrade);
    }

    // 표시 가능한 진화 버튼 모델을 만든다
    private int BuildEvolutionActions()
    {
        int availableCount = selectedTurret.GetAvailableEvolutionCount();
        int visibleCount = Mathf.Min(availableCount, evolutionBuffer.Length);

        for (int i = 0; i < visibleCount; i++)
        {
            TurretEvolutionEntry evolutionEntry = selectedTurret.GetAvailableEvolution(i);
            TurretDefinitionSO targetDefinition = evolutionEntry == null ? null : evolutionEntry.targetDefinition;
            ResourceCost[] costs = selectedTurret.GetEvolutionCosts(i);
            bool canEvolve = spendCurrency ? selectedTurret.CanEvolve(i) : targetDefinition != null;
            string title = targetDefinition == null ? "진화 후보 없음" : ResolveTurretName(targetDefinition);
            string stateText = canEvolve ? string.Empty : "진화 불가";
            Sprite icon = evolutionEntry == null ? null : evolutionEntry.evolutionIcon;
            evolutionBuffer[i] = new TurretUpgradeActionViewModel(TurretUpgradeActionType.Evolution, i, title, FormatCosts(costs), stateText, icon, true, canEvolve);
        }

        return visibleCount;
    }

    // 현재 탑승 중인 엔지니어 좌석 모델을 만든다
    private int BuildEngineerSeats()
    {
        if (selectedBuffReceiver == null)
        {
            return 0;
        }

        int engineerCount = Mathf.Min(selectedBuffReceiver.EngineerCount, engineerSeatBuffer.Length);
        for (int i = 0; i < engineerCount; i++)
        {
            Survivor engineer = selectedBuffReceiver.GetEngineerAt(i);
            string engineerName = engineer == null ? "비어 있음" : engineer.name;
            string buffText = $"+{selectedBuffReceiver.DamageBonusRatioPerEngineer * 100.0f:0.#}% 공격력";
            engineerSeatBuffer[i] = new TurretEngineerSeatViewModel(i, engineerName, buffText, engineer != null, engineer != null);
        }

        return engineerCount;
    }

    // 스탯 버퍼를 ViewModel 전용 배열로 복사한다
    private TurretUpgradeStatViewModel[] CopyStats()
    {
        TurretUpgradeStatViewModel[] result = new TurretUpgradeStatViewModel[statBuffer.Length];
        Array.Copy(statBuffer, result, statBuffer.Length);
        return result;
    }

    // 진화 버튼 버퍼에서 표시할 수량만 복사한다
    private TurretUpgradeActionViewModel[] CopyEvolutionActions(int count)
    {
        int safeCount = Mathf.Clamp(count, 0, evolutionBuffer.Length);
        TurretUpgradeActionViewModel[] result = new TurretUpgradeActionViewModel[safeCount];
        if (safeCount > 0)
        {
            Array.Copy(evolutionBuffer, result, safeCount);
        }

        return result;
    }

    // 엔지니어 좌석 버퍼에서 표시할 수량만 복사한다
    private TurretEngineerSeatViewModel[] CopyEngineerSeats(int count)
    {
        int safeCount = Mathf.Clamp(count, 0, engineerSeatBuffer.Length);
        TurretEngineerSeatViewModel[] result = new TurretEngineerSeatViewModel[safeCount];
        if (safeCount > 0)
        {
            Array.Copy(engineerSeatBuffer, result, safeCount);
        }

        return result;
    }

    // 업그레이드 버튼 입력을 처리한다
    private void OnUpgradeRequested()
    {
        if (selectedTurret == null)
        {
            Hide();
            return;
        }

        bool upgraded = spendCurrency ? selectedTurret.TryUpgrade(levelUpAmount) : TryUpgradeWithoutCurrency();
        if (!upgraded)
        {
            Refresh();
            return;
        }

        Refresh();
    }

    // 진화 버튼 입력을 처리한다
    private void OnEvolutionRequested(int availableIndex)
    {
        if (selectedTurret == null || availableIndex < 0)
        {
            Hide();
            return;
        }

        TurretDefinitionRuntimeController evolvedTurret = replacePrefabOnEvolution
            ? TryCreateEvolvedInstance(availableIndex)
            : TryEvolveInPlace(availableIndex);

        if (evolvedTurret == null)
        {
            Refresh();
            return;
        }

        selectedTurret = evolvedTurret;
        selectedBuffReceiver = selectedTurret.GetComponent<TurretEngineerBuffReceiver>();
        UpdateSelectedSlot(evolvedTurret);
        Refresh();
    }

    // 엔지니어 하차 버튼 입력을 처리한다
    private void OnEngineerDismountRequested(int seatIndex)
    {
        if (selectedBuffReceiver == null || seatIndex < 0)
        {
            Refresh();
            return;
        }

        Survivor engineer = selectedBuffReceiver.GetEngineerAt(seatIndex);
        if (engineer == null)
        {
            Refresh();
            return;
        }

        if (!engineer.TryDismountEngineerFromTurret())
        {
            Debug.LogWarning("[TurretUpgradePopupPresenter] 엔지니어 하차 요청을 처리하지 못했습니다.", engineer);
        }

        Refresh();
    }

    // 닫기 입력을 처리한다
    private void OnCloseRequested()
    {
        Hide();
    }

    // 선택 터렛의 엔지니어 버프가 변경되면 표시를 갱신한다
    private void OnEngineerBuffStateChanged(TurretEngineerBuffReceiver buffReceiver)
    {
        if (buffReceiver == null || selectedBuffReceiver != buffReceiver)
        {
            return;
        }

        Refresh();
    }

    // 비용 없이 업그레이드를 적용한다
    private bool TryUpgradeWithoutCurrency()
    {
        if (selectedTurret.IsMaxTierLevelReached)
        {
            return false;
        }

        selectedTurret.AddLevel(levelUpAmount);
        return true;
    }

    // 설정에 따라 진화 프리팹 인스턴스를 생성한다
    private TurretDefinitionRuntimeController TryCreateEvolvedInstance(int availableIndex)
    {
        return spendCurrency
            ? selectedTurret.TryCreateEvolvedInstance(availableIndex)
            : selectedTurret.CreateEvolvedInstance(availableIndex);
    }

    // 설정에 따라 현재 인스턴스에서 바로 진화한다
    private TurretDefinitionRuntimeController TryEvolveInPlace(int availableIndex)
    {
        bool evolved = spendCurrency ? selectedTurret.TryEvolve(availableIndex) : selectedTurret.Evolve(availableIndex);
        return evolved ? selectedTurret : null;
    }

    // 진화 후 슬롯의 현재 터렛 참조를 갱신한다
    private void UpdateSelectedSlot(TurretDefinitionRuntimeController evolvedTurret)
    {
        if (evolvedTurret == null)
        {
            return;
        }

        if (selectedSlot == null)
        {
            selectedSlot = evolvedTurret.GetComponentInParent<TurretBaseSlot>();
        }

        if (selectedSlot != null)
        {
            selectedSlot.SetCurrentTurret(evolvedTurret);
        }
    }

    // View 이벤트를 Presenter 처리 함수에 연결한다
    private void BindViewEvents()
    {
        if (view == null)
        {
            return;
        }

        view.UpgradeRequested += OnUpgradeRequested;
        view.EvolutionRequested += OnEvolutionRequested;
        view.EngineerDismountRequested += OnEngineerDismountRequested;
        view.CloseRequested += OnCloseRequested;
    }

    // View 이벤트 구독을 해제한다
    private void UnbindViewEvents()
    {
        if (view == null)
        {
            return;
        }

        view.UpgradeRequested -= OnUpgradeRequested;
        view.EvolutionRequested -= OnEvolutionRequested;
        view.EngineerDismountRequested -= OnEngineerDismountRequested;
        view.CloseRequested -= OnCloseRequested;
    }

    // 누락된 View 참조를 같은 오브젝트 또는 하위 오브젝트에서 찾는다
    private void AutoBindReferences()
    {
        if (view == null)
        {
            view = GetComponent<TurretUpgradePopupView>();
        }

        if (view == null)
        {
            view = GetComponentInChildren<TurretUpgradePopupView>(true);
        }
    }

    // 터렛 정의에서 표시 이름을 얻는다
    private static string ResolveTurretName(TurretDefinitionSO definition)
    {
        if (definition == null)
        {
            return "터렛 없음";
        }

        return string.IsNullOrEmpty(definition.displayName) ? definition.name : definition.displayName;
    }

    // 현재 레벨 표시 문자열을 만든다
    private string BuildLevelText(int tierLevel, int totalLevel, int maxTierLevel)
    {
        textBuilder.Clear();
        textBuilder.Append("티어 Lv. ");
        textBuilder.Append(tierLevel);

        if (maxTierLevel > 0)
        {
            textBuilder.Append(" / ");
            textBuilder.Append(maxTierLevel);
        }

        textBuilder.Append("  총 Lv. ");
        textBuilder.Append(totalLevel);
        return textBuilder.ToString();
    }

    // 현재 성장 상태 표시 문자열을 만든다
    private string BuildStatusText()
    {
        if (selectedTurret == null)
        {
            return string.Empty;
        }

        if (selectedTurret.GetAvailableEvolutionCount() > 0)
        {
            return "진화 가능";
        }

        if (selectedTurret.IsMaxTierLevelReached)
        {
            return "현재 티어 최대 레벨";
        }

        return spendCurrency ? "재화를 사용해 업그레이드" : "테스트 모드: 비용 미사용";
    }

    // 비용 배열을 UI 표시용 문자열로 변환한다
    private static string FormatCosts(ResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0)
        {
            return "무료";
        }

        StringBuilder builder = new StringBuilder(48);
        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(" / ");
            }

            builder.Append(cost.currencyType);
            builder.Append(' ');
            builder.Append(cost.amount);
        }

        return builder.Length == 0 ? "무료" : builder.ToString();
    }

    // 소수점 값을 짧은 문자열로 변환한다
    private static string FormatNumber(float value)
    {
        return value.ToString("0.##");
    }

    // 소수점 변화량을 부호 포함 문자열로 변환한다
    private static string FormatDelta(float delta)
    {
        return delta > 0.0f ? $"+{delta:0.##}" : delta.ToString("0.##");
    }

    // 정수 변화량을 부호 포함 문자열로 변환한다
    private static string FormatSignedInteger(int delta)
    {
        return delta > 0 ? $"+{delta}" : delta.ToString();
    }
}
