using System;
using UnityEngine;

/// <summary>
/// 터렛 업그레이드 팝업이 표시할 전체 읽기 전용 UI 데이터를 담는다.
/// </summary>
public sealed class TurretUpgradePopupViewModel
{
    public string TurretName { get; }
    public string LevelText { get; }
    public string StatusText { get; }
    public TurretUpgradeStatViewModel[] Stats { get; }
    public TurretUpgradeActionViewModel UpgradeAction { get; }
    public TurretUpgradeActionViewModel[] EvolutionActions { get; }
    public TurretEngineerSeatViewModel[] EngineerSeats { get; }

    // 팝업 전체 표시 데이터를 초기화한다
    public TurretUpgradePopupViewModel(
        string turretName,
        string levelText,
        string statusText,
        TurretUpgradeStatViewModel[] stats,
        TurretUpgradeActionViewModel upgradeAction,
        TurretUpgradeActionViewModel[] evolutionActions,
        TurretEngineerSeatViewModel[] engineerSeats)
    {
        TurretName = turretName ?? string.Empty;
        LevelText = levelText ?? string.Empty;
        StatusText = statusText ?? string.Empty;
        Stats = stats ?? Array.Empty<TurretUpgradeStatViewModel>();
        UpgradeAction = upgradeAction;
        EvolutionActions = evolutionActions ?? Array.Empty<TurretUpgradeActionViewModel>();
        EngineerSeats = engineerSeats ?? Array.Empty<TurretEngineerSeatViewModel>();
    }
}

/// <summary>
/// 터렛 스탯 한 줄의 현재값, 다음값, 변화량 표시 데이터를 담는다.
/// </summary>
public sealed class TurretUpgradeStatViewModel
{
    public string Label { get; }
    public string CurrentValue { get; }
    public string NextValue { get; }
    public string DeltaValue { get; }
    public bool HasPositiveDelta { get; }

    // 스탯 행 표시 데이터를 초기화한다
    public TurretUpgradeStatViewModel(string label, string currentValue, string nextValue, string deltaValue, bool hasPositiveDelta)
    {
        Label = label ?? string.Empty;
        CurrentValue = currentValue ?? string.Empty;
        NextValue = nextValue ?? string.Empty;
        DeltaValue = deltaValue ?? string.Empty;
        HasPositiveDelta = hasPositiveDelta;
    }
}

/// <summary>
/// 업그레이드 또는 진화 버튼 하나의 표시 데이터와 실행 가능 상태를 담는다.
/// </summary>
public sealed class TurretUpgradeActionViewModel
{
    public TurretUpgradeActionType ActionType { get; }
    public int ActionIndex { get; }
    public string Title { get; }
    public string CostText { get; }
    public string StateText { get; }
    public Sprite Icon { get; }
    public bool IsVisible { get; }
    public bool IsInteractable { get; }

    // 버튼 표시 데이터를 초기화한다
    public TurretUpgradeActionViewModel(
        TurretUpgradeActionType actionType,
        int actionIndex,
        string title,
        string costText,
        string stateText,
        Sprite icon,
        bool isVisible,
        bool isInteractable)
    {
        ActionType = actionType;
        ActionIndex = actionIndex;
        Title = title ?? string.Empty;
        CostText = costText ?? string.Empty;
        StateText = stateText ?? string.Empty;
        Icon = icon;
        IsVisible = isVisible;
        IsInteractable = isInteractable;
    }
}

/// <summary>
/// 터렛에 탑승한 엔지니어 좌석 하나의 표시 데이터를 담는다.
/// </summary>
public sealed class TurretEngineerSeatViewModel
{
    public int SeatIndex { get; }
    public string Label { get; }
    public string BuffText { get; }
    public bool IsVisible { get; }
    public bool IsInteractable { get; }

    // 엔지니어 좌석 표시 데이터를 초기화한다
    public TurretEngineerSeatViewModel(int seatIndex, string label, string buffText, bool isVisible, bool isInteractable)
    {
        SeatIndex = seatIndex;
        Label = label ?? string.Empty;
        BuffText = buffText ?? string.Empty;
        IsVisible = isVisible;
        IsInteractable = isInteractable;
    }
}

/// <summary>
/// 터렛 업그레이드 팝업에서 실행할 수 있는 버튼 동작 종류를 정의한다.
/// </summary>
public enum TurretUpgradeActionType
{
    Upgrade = 0,
    Evolution = 1,
}
