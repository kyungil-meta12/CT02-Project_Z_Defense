using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

// 그래프 세로축 그룹을 구분한다.
internal enum TurretBalanceReportGraphAxisGroup
{
    Hp,
    Ratio,
    Currency
}

// 터렛 웨이브 밸런스 리포트 그래프 탭에서 사용할 표시 상태를 보관한다.
internal sealed class TurretBalanceReportGraphState
{
    public bool ShowTotalWaveHp;
    public bool ShowTotalZombieHp;
    public bool ShowAverageZombieHp;
    public bool ShowCumulativeCurrency;
    public bool ShowTopRankDps;
    public bool ShowAllTurretDps;
    public bool PreferAllTurretDps = true;
    public bool ShowClearTimeRatio;
    public bool ShowObstacleDestructionRatio;
    public bool ShowZombieArrivalRatio;
    public TurretBalanceReportGraphAxisGroup ActiveVerticalAxisGroup = TurretBalanceReportGraphAxisGroup.Hp;
    public float WaveAxisZoom = 1.0f;
    public float WaveAxisOffset01;
    public float HpAxisZoom = 1.0f;
    public float HpAxisOffset01;
    public float RatioAxisZoom = 1.0f;
    public float RatioAxisOffset01;
    public float CurrencyAxisZoom = 1.0f;
    public float CurrencyAxisOffset01;
    public bool IsDraggingGraph;
    public Vector2 LastDragMousePosition;
    public Vector2 TopConditionToggleScroll;
    public Vector2 CurrencyToggleScroll;
    public Vector2 ZombieToggleScroll;
    public Vector2 TurretToggleScroll;
    public readonly Dictionary<RewardCurrencyType, bool> CurrencyVisibility = new Dictionary<RewardCurrencyType, bool>();
    public readonly Dictionary<NormalZombieType, bool> NormalZombieVisibility = new Dictionary<NormalZombieType, bool>();
    public readonly Dictionary<BossZombieType, bool> BossZombieVisibility = new Dictionary<BossZombieType, bool>();
    public readonly Dictionary<string, bool> TurretVisibility = new Dictionary<string, bool>();

    // 활성 세로축 그룹의 확대 배율을 반환한다
    public float GetActiveVerticalZoom()
    {
        switch (ActiveVerticalAxisGroup)
        {
            case TurretBalanceReportGraphAxisGroup.Ratio:
                return RatioAxisZoom;
            case TurretBalanceReportGraphAxisGroup.Currency:
                return CurrencyAxisZoom;
            default:
                return HpAxisZoom;
        }
    }

    // 활성 세로축 그룹의 확대 배율을 갱신한다
    public void SetActiveVerticalZoom(float zoom)
    {
        switch (ActiveVerticalAxisGroup)
        {
            case TurretBalanceReportGraphAxisGroup.Ratio:
                RatioAxisZoom = zoom;
                break;
            case TurretBalanceReportGraphAxisGroup.Currency:
                CurrencyAxisZoom = zoom;
                break;
            default:
                HpAxisZoom = zoom;
                break;
        }
    }

    // 활성 세로축 그룹의 스크롤 위치를 반환한다
    public float GetActiveVerticalOffset01()
    {
        switch (ActiveVerticalAxisGroup)
        {
            case TurretBalanceReportGraphAxisGroup.Ratio:
                return RatioAxisOffset01;
            case TurretBalanceReportGraphAxisGroup.Currency:
                return CurrencyAxisOffset01;
            default:
                return HpAxisOffset01;
        }
    }

    // 활성 세로축 그룹의 스크롤 위치를 갱신한다
    public void SetActiveVerticalOffset01(float offset01)
    {
        float clampedOffset = Mathf.Clamp01(offset01);
        switch (ActiveVerticalAxisGroup)
        {
            case TurretBalanceReportGraphAxisGroup.Ratio:
                RatioAxisOffset01 = clampedOffset;
                break;
            case TurretBalanceReportGraphAxisGroup.Currency:
                CurrencyAxisOffset01 = clampedOffset;
                break;
            default:
                HpAxisOffset01 = clampedOffset;
                break;
        }
    }

    // 활성 세로축 그룹의 확대와 스크롤을 초기화한다
    public void ResetActiveVerticalAxis()
    {
        SetActiveVerticalZoom(1.0f);
        SetActiveVerticalOffset01(0.0f);
    }

    // 웨이브 가로축 확대와 스크롤을 초기화한다
    public void ResetWaveAxis()
    {
        WaveAxisZoom = 1.0f;
        WaveAxisOffset01 = 0.0f;
    }

    // 모든 그래프 축 확대와 스크롤을 초기화한다
    public void ResetAllAxes()
    {
        ResetWaveAxis();
        HpAxisZoom = 1.0f;
        HpAxisOffset01 = 0.0f;
        RatioAxisZoom = 1.0f;
        RatioAxisOffset01 = 0.0f;
        CurrencyAxisZoom = 1.0f;
        CurrencyAxisOffset01 = 0.0f;
    }

    // 재화별 표시 상태가 없으면 기본값으로 등록한다
    public bool GetCurrencyVisible(RewardCurrencyType currencyType)
    {
        if (!CurrencyVisibility.TryGetValue(currencyType, out bool visible))
        {
            visible = true;
            CurrencyVisibility[currencyType] = visible;
        }

        return visible;
    }

    // 재화별 표시 상태를 갱신한다
    public void SetCurrencyVisible(RewardCurrencyType currencyType, bool visible)
    {
        CurrencyVisibility[currencyType] = visible;
    }

    // 일반 좀비 타입별 표시 상태가 없으면 기본값으로 등록한다
    public bool GetNormalZombieVisible(NormalZombieType zombieType)
    {
        if (!NormalZombieVisibility.TryGetValue(zombieType, out bool visible))
        {
            visible = true;
            NormalZombieVisibility[zombieType] = visible;
        }

        return visible;
    }

    // 일반 좀비 타입별 표시 상태를 갱신한다
    public void SetNormalZombieVisible(NormalZombieType zombieType, bool visible)
    {
        NormalZombieVisibility[zombieType] = visible;
    }

    // 보스 좀비 타입별 표시 상태가 없으면 기본값으로 등록한다
    public bool GetBossZombieVisible(BossZombieType bossType)
    {
        if (!BossZombieVisibility.TryGetValue(bossType, out bool visible))
        {
            visible = true;
            BossZombieVisibility[bossType] = visible;
        }

        return visible;
    }

    // 보스 좀비 타입별 표시 상태를 갱신한다
    public void SetBossZombieVisible(BossZombieType bossType, bool visible)
    {
        BossZombieVisibility[bossType] = visible;
    }

    // 터렛별 표시 상태가 없으면 기본값으로 등록한다
    public bool GetTurretVisible(string turretName)
    {
        string safeName = turretName ?? string.Empty;
        if (!TurretVisibility.TryGetValue(safeName, out bool visible))
        {
            visible = true;
            TurretVisibility[safeName] = visible;
        }

        return visible;
    }

    // 터렛별 표시 상태를 갱신한다
    public void SetTurretVisible(string turretName, bool visible)
    {
        TurretVisibility[turretName ?? string.Empty] = visible;
    }
}

// 터렛 웨이브 밸런스 리포트 결과를 웨이브 기준 선 그래프로 그린다.
internal static class TurretBalanceReportGraphRenderer
{
    private const float GRAPH_MIN_HEIGHT = 360.0f;
    private const float GRAPH_LEFT_PADDING = 58.0f;
    private const float GRAPH_RIGHT_PADDING = 18.0f;
    private const float GRAPH_TOP_PADDING = 14.0f;
    private const float GRAPH_BOTTOM_PADDING = 38.0f;
    private const float HOVER_DISTANCE = 7.0f;
    private const float AXIS_MIN_ZOOM = 1.0f;
    private const float AXIS_MAX_ZOOM = 20.0f;
    private const float AXIS_ZOOM_STEP = 1.12f;
    private const float SCROLLBAR_SIZE = 13.0f;
    private const float CONTROL_HINT_WIDTH = 390.0f;
    private const float TOGGLE_ROW_HEIGHT = 20.0f;
    private const float TOGGLE_SCROLL_HEIGHT = 38.0f;
    private const float TOGGLE_LABEL_PADDING = 34.0f;
    private const float TOGGLE_MIN_WIDTH = 84.0f;
    private const float TOGGLE_MAX_WIDTH = 190.0f;

    private static readonly Color[] SeriesColors =
    {
        new Color(0.92f, 0.24f, 0.22f),
        new Color(0.16f, 0.56f, 0.95f),
        new Color(0.18f, 0.72f, 0.38f),
        new Color(0.95f, 0.58f, 0.14f),
        new Color(0.58f, 0.32f, 0.86f),
        new Color(0.12f, 0.70f, 0.72f),
        new Color(0.86f, 0.30f, 0.58f),
        new Color(0.55f, 0.55f, 0.18f)
    };

    private static readonly Color EvolutionStartColor = new Color(0.20f, 0.72f, 0.32f);
    private static readonly Color EvolutionMiddleColor = new Color(0.18f, 0.56f, 0.95f);
    private static readonly Color EvolutionEndColor = new Color(0.68f, 0.34f, 0.92f);
    private static readonly string[] AxisGroupLabels = { "HP", "배율", "재화" };
    private static readonly Dictionary<int, GUIStyle> ColoredToggleStyleCache = new Dictionary<int, GUIStyle>();
    private static readonly GUIContent ReusableToggleLabelContent = new GUIContent();
    private static Vector3[] reusablePointBuffer = new Vector3[0];
    private static bool[] reusableValidPointBuffer = new bool[0];

    // 그래프 탭 전체 UI를 그린다
    public static void Draw(TurretBalanceReportResult report, TurretBalanceReportGraphState state, float targetClearSeconds, float targetClearSecondsIncrement, float obstacleTargetTimeMultiplier, float zombieArrivalSeconds, float zombieArrivalTimeMultiplier, List<ObstacleWaveRow> obstacleRows)
    {
        if (state == null)
        {
            return;
        }

        EditorGUILayout.HelpBox("가로축은 웨이브입니다. 표시 조건은 HP, 배율, 재화 세로축 그룹으로 나뉘며 각 그룹은 독립적으로 확대/스크롤됩니다. 터렛 선은 총 DPS × 기준 클리어 시간으로 변환한 처리 가능 HP입니다.", MessageType.Info);
        if (report == null || report.WaveRows.Count == 0)
        {
            EditorGUILayout.LabelField("표시할 리포트 데이터가 없습니다.", EditorStyles.boldLabel);
            return;
        }

        List<RewardCurrencyType> currencyTypes = BuildCurrencyTypeList(report);
        DrawSeriesToggles(report, state, currencyTypes);

        Rect graphRect = GUILayoutUtility.GetRect(0.0f, GRAPH_MIN_HEIGHT, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        DrawGraph(report, state, currencyTypes, graphRect, Mathf.Max(1.0f, targetClearSeconds), Mathf.Max(0.0f, targetClearSecondsIncrement), Mathf.Max(0.1f, obstacleTargetTimeMultiplier), Mathf.Max(0.0f, zombieArrivalSeconds), Mathf.Max(0.01f, zombieArrivalTimeMultiplier), obstacleRows);
    }

    // 리포트에 등장한 누적 재화 종류 목록을 정렬해서 만든다
    private static List<RewardCurrencyType> BuildCurrencyTypeList(TurretBalanceReportResult report)
    {
        List<RewardCurrencyType> currencyTypes = new List<RewardCurrencyType>(4);
        HashSet<RewardCurrencyType> currencyScope = TurretBalanceReportCurrencyProjector.BuildTurretCurrencyScope(report);
        for (int i = 0; i < report.ItemBalanceRows.Count; i++)
        {
            Dictionary<RewardCurrencyType, float> rewards = TurretBalanceReportCurrencyProjector.FilterItemAmounts(report.ItemBalanceRows[i], currencyScope);
            if (rewards == null)
            {
                continue;
            }

            foreach (KeyValuePair<RewardCurrencyType, float> pair in rewards)
            {
                if (!currencyTypes.Contains(pair.Key))
                {
                    currencyTypes.Add(pair.Key);
                }
            }
        }

        currencyTypes.Sort((left, right) => ((int)left).CompareTo((int)right));
        return currencyTypes;
    }

    // 그래프 선 표시 토글을 그린다
    private static void DrawSeriesToggles(TurretBalanceReportResult report, TurretBalanceReportGraphState state, List<RewardCurrencyType> currencyTypes)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("표시 조건", EditorStyles.boldLabel);

        DrawTopConditionToggles(state);

        DrawAxisControlBar(state);

        if (state.ShowCumulativeCurrency && currencyTypes.Count > 0)
        {
            DrawCurrencyVisibilityToggles(state, currencyTypes);
        }

        if (state.ShowTotalWaveHp)
        {
            DrawZombieVisibilityToggles(report, state);
        }

        if (state.ShowAllTurretDps)
        {
            DrawTurretVisibilityToggles(report, state);
        }

        EditorGUILayout.EndVertical();
    }

    // 표시 조건 최상위 토글을 한 줄 가로 스크롤로 그린다
    private static void DrawTopConditionToggles(TurretBalanceReportGraphState state)
    {
        state.TopConditionToggleScroll = BeginToggleScrollRow("조건", state.TopConditionToggleScroll);
        bool allVisible = IsAllTopConditionsVisible(state);
        bool nextAllVisible = DrawColoredToggle("전체", allVisible, Color.white);
        if (nextAllVisible != allVisible)
        {
            SetAllTopConditionsVisible(state, nextAllVisible);
        }

        state.ShowTotalWaveHp = DrawColoredToggle("좀비 HP 스택", state.ShowTotalWaveHp, GetTopConditionColor(0));
        state.ShowTotalZombieHp = DrawColoredToggle("총 좀비 HP", state.ShowTotalZombieHp, GetTopConditionColor(1));
        state.ShowAverageZombieHp = DrawColoredToggle("일반 좀비 평균 HP", state.ShowAverageZombieHp, GetTopConditionColor(2));
        bool nextShowTopRankDps = DrawColoredToggle("1순위 터렛 DPS", state.ShowTopRankDps, GetTopConditionColor(3));
        if (nextShowTopRankDps)
        {
            state.ShowAllTurretDps = false;
            state.PreferAllTurretDps = false;
        }

        state.ShowTopRankDps = nextShowTopRankDps;
        bool nextShowAllTurretDps = DrawColoredToggle("모든 터렛 DPS", state.ShowAllTurretDps, GetTopConditionColor(4));
        if (nextShowAllTurretDps)
        {
            state.ShowTopRankDps = false;
            state.PreferAllTurretDps = true;
        }

        state.ShowAllTurretDps = nextShowAllTurretDps;
        state.ShowClearTimeRatio = DrawColoredToggle("클리어 시간 배율", state.ShowClearTimeRatio, GetTopConditionColor(5));
        state.ShowObstacleDestructionRatio = DrawColoredToggle("장애물 파괴시간 배율", state.ShowObstacleDestructionRatio, GetTopConditionColor(6));
        state.ShowZombieArrivalRatio = DrawColoredToggle("좀비 도달시간 배율", state.ShowZombieArrivalRatio, GetTopConditionColor(7));
        state.ShowCumulativeCurrency = DrawColoredToggle("누적 재화", state.ShowCumulativeCurrency, GetTopConditionColor(8));
        EndToggleScrollRow();
    }

    // 그래프 축 선택과 초기화 컨트롤을 그린다
    private static void DrawAxisControlBar(TurretBalanceReportGraphState state)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("활성 축", GUILayout.Width(48.0f));
        int selectedGroup = GUILayout.Toolbar((int)state.ActiveVerticalAxisGroup, AxisGroupLabels, GUILayout.Width(170.0f));
        TurretBalanceReportGraphAxisGroup nextGroup = (TurretBalanceReportGraphAxisGroup)Mathf.Clamp(selectedGroup, 0, 2);
        if (nextGroup != state.ActiveVerticalAxisGroup)
        {
            state.ActiveVerticalAxisGroup = nextGroup;
            ApplyActiveAxisFilter(state, nextGroup);
        }

        EditorGUILayout.LabelField($"세로 {state.GetActiveVerticalZoom():0.##}x", EditorStyles.miniLabel, GUILayout.Width(76.0f));
        EditorGUILayout.LabelField($"웨이브 {state.WaveAxisZoom:0.##}x", EditorStyles.miniLabel, GUILayout.Width(88.0f));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("웨이브 축 초기화", EditorStyles.miniButton, GUILayout.Width(108.0f)))
        {
            state.ResetWaveAxis();
        }

        if (GUILayout.Button("세로 축 초기화", EditorStyles.miniButton, GUILayout.Width(98.0f)))
        {
            state.ResetActiveVerticalAxis();
        }

        if (GUILayout.Button("전체 축 초기화", EditorStyles.miniButton, GUILayout.Width(98.0f)))
        {
            state.ResetAllAxes();
        }

        EditorGUILayout.EndHorizontal();
    }

    // 누적 재화 토글을 한 줄 가로 스크롤로 그린다
    private static void DrawCurrencyVisibilityToggles(TurretBalanceReportGraphState state, List<RewardCurrencyType> currencyTypes)
    {
        state.CurrencyToggleScroll = BeginToggleScrollRow("재화", state.CurrencyToggleScroll);
        bool allVisible = AreAllCurrenciesVisible(state, currencyTypes);
        bool nextAllVisible = DrawColoredToggle("전체", allVisible, Color.white);
        if (nextAllVisible != allVisible)
        {
            SetAllCurrenciesVisible(state, currencyTypes, nextAllVisible);
        }

        for (int i = 0; i < currencyTypes.Count; i++)
        {
            RewardCurrencyType currencyType = currencyTypes[i];
            string label = GetCurrencyLabel(currencyType);
            bool visible = DrawColoredToggle(label, state.GetCurrencyVisible(currencyType), GetCurrencyGraphColor(i));
            state.SetCurrencyVisible(currencyType, visible);
        }

        EndToggleScrollRow();
    }

    // 좀비 타입별 HP 스택 표시 토글을 한 줄 가로 스크롤로 그린다
    private static void DrawZombieVisibilityToggles(TurretBalanceReportResult report, TurretBalanceReportGraphState state)
    {
        state.ZombieToggleScroll = BeginToggleScrollRow("좀비", state.ZombieToggleScroll);
        bool allVisible = AreAllZombiesVisible(report, state);
        bool nextAllVisible = DrawColoredToggle("전체", allVisible, Color.white);
        if (nextAllVisible != allVisible)
        {
            SetAllZombiesVisible(report, state, nextAllVisible);
        }

        Array normalTypes = Enum.GetValues(typeof(NormalZombieType));
        int colorIndex = 0;
        for (int i = 0; i < normalTypes.Length; i++)
        {
            NormalZombieType normalType = (NormalZombieType)normalTypes.GetValue(i);
            if (!HasHpStackSegment(report, false, normalType, default))
            {
                continue;
            }

            bool visible = DrawColoredToggle(normalType.ToString(), state.GetNormalZombieVisible(normalType), GetSeriesColor(colorIndex));
            state.SetNormalZombieVisible(normalType, visible);
            colorIndex++;
        }

        Array bossTypes = Enum.GetValues(typeof(BossZombieType));
        for (int i = 0; i < bossTypes.Length; i++)
        {
            BossZombieType bossType = (BossZombieType)bossTypes.GetValue(i);
            if (!HasHpStackSegment(report, true, default, bossType))
            {
                continue;
            }

            bool visible = DrawColoredToggle(bossType.ToString(), state.GetBossZombieVisible(bossType), GetSeriesColor(colorIndex));
            state.SetBossZombieVisible(bossType, visible);
            colorIndex++;
        }

        EndToggleScrollRow();
    }

    // 터렛별 DPS 표시 토글을 한 줄 가로 스크롤로 그린다
    private static void DrawTurretVisibilityToggles(TurretBalanceReportResult report, TurretBalanceReportGraphState state)
    {
        List<TurretGraphEntry> turretEntries = BuildTurretGraphEntries(report);
        int maxTier = GetMaxTurretTier(turretEntries);
        state.TurretToggleScroll = BeginToggleScrollRow("터렛", state.TurretToggleScroll);
        bool allVisible = AreAllTurretsVisible(state, turretEntries);
        bool nextAllVisible = DrawColoredToggle("전체", allVisible, Color.white);
        if (nextAllVisible != allVisible)
        {
            SetAllTurretsVisible(state, turretEntries, nextAllVisible);
        }

        for (int i = 0; i < turretEntries.Count; i++)
        {
            string turretName = turretEntries[i].TurretName;
            if (string.IsNullOrEmpty(turretName))
            {
                continue;
            }

            bool visible = DrawColoredToggle(turretName, state.GetTurretVisible(turretName), GetTurretEvolutionColor(turretEntries[i].Tier, maxTier, i));
            state.SetTurretVisible(turretName, visible);
        }

        EndToggleScrollRow();
    }

    // 가로 스크롤 토글 행을 시작한다
    private static Vector2 BeginToggleScrollRow(string label, Vector2 scroll)
    {
        EditorGUILayout.BeginHorizontal(GUILayout.Height(TOGGLE_SCROLL_HEIGHT));
        EditorGUILayout.LabelField(label, GUILayout.Width(36.0f));
        Vector2 nextScroll = EditorGUILayout.BeginScrollView(scroll, false, false, GUILayout.Height(TOGGLE_SCROLL_HEIGHT));
        EditorGUILayout.BeginHorizontal();
        return nextScroll;
    }

    // 가로 스크롤 토글 행을 종료한다
    private static void EndToggleScrollRow()
    {
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndHorizontal();
    }

    // 지정 색상의 글자로 토글을 그린다
    private static bool DrawColoredToggle(string label, bool value, Color color)
    {
        GUIStyle style = GetColoredToggleStyle(color);
        float width = CalculateToggleWidth(label);
        return EditorGUILayout.ToggleLeft(label, value, style, GUILayout.Width(width), GUILayout.Height(TOGGLE_ROW_HEIGHT));
    }

    // 토글 글자 색상용 GUIStyle을 캐시해서 반환한다
    private static GUIStyle GetColoredToggleStyle(Color color)
    {
        int colorKey = GetColorKey(color);
        if (ColoredToggleStyleCache.TryGetValue(colorKey, out GUIStyle cachedStyle))
        {
            return cachedStyle;
        }

        GUIStyle style = new GUIStyle(EditorStyles.label);
        style.normal.textColor = color;
        style.onNormal.textColor = color;
        style.hover.textColor = color;
        style.onHover.textColor = color;
        style.active.textColor = color;
        style.onActive.textColor = color;
        style.focused.textColor = color;
        style.onFocused.textColor = color;
        ColoredToggleStyleCache[colorKey] = style;
        return style;
    }

    // 색상 캐시 키를 만든다
    private static int GetColorKey(Color color)
    {
        Color32 color32 = color;
        return color32.r << 24 | color32.g << 16 | color32.b << 8 | color32.a;
    }

    // 토글 라벨 길이에 맞는 폭을 계산한다
    private static float CalculateToggleWidth(string label)
    {
        ReusableToggleLabelContent.text = label;
        float width = EditorStyles.label.CalcSize(ReusableToggleLabelContent).x + TOGGLE_LABEL_PADDING;
        return Mathf.Clamp(width, TOGGLE_MIN_WIDTH, TOGGLE_MAX_WIDTH);
    }

    // 상위 표시조건이 모두 켜진 상태인지 확인한다
    private static bool IsAllTopConditionsVisible(TurretBalanceReportGraphState state)
    {
        return state.ShowTotalWaveHp
            && state.ShowTotalZombieHp
            && state.ShowAverageZombieHp
            && (state.ShowTopRankDps || state.ShowAllTurretDps)
            && state.ShowClearTimeRatio
            && state.ShowObstacleDestructionRatio
            && state.ShowZombieArrivalRatio
            && state.ShowCumulativeCurrency;
    }

    // 상위 표시조건 전체를 켜거나 끈다
    private static void SetAllTopConditionsVisible(TurretBalanceReportGraphState state, bool visible)
    {
        state.ShowTotalWaveHp = visible;
        state.ShowTotalZombieHp = visible;
        state.ShowAverageZombieHp = visible;
        if (visible)
        {
            state.ShowTopRankDps = !state.PreferAllTurretDps;
            state.ShowAllTurretDps = state.PreferAllTurretDps;
        }
        else
        {
            state.ShowTopRankDps = false;
            state.ShowAllTurretDps = false;
        }

        state.ShowClearTimeRatio = visible;
        state.ShowObstacleDestructionRatio = visible;
        state.ShowZombieArrivalRatio = visible;
        state.ShowCumulativeCurrency = visible;
    }

    // 재화 표시 항목이 모두 켜진 상태인지 확인한다
    private static bool AreAllCurrenciesVisible(TurretBalanceReportGraphState state, List<RewardCurrencyType> currencyTypes)
    {
        if (currencyTypes.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < currencyTypes.Count; i++)
        {
            if (!state.GetCurrencyVisible(currencyTypes[i]))
            {
                return false;
            }
        }

        return true;
    }

    // 재화 표시 항목 전체를 켜거나 끈다
    private static void SetAllCurrenciesVisible(TurretBalanceReportGraphState state, List<RewardCurrencyType> currencyTypes, bool visible)
    {
        for (int i = 0; i < currencyTypes.Count; i++)
        {
            state.SetCurrencyVisible(currencyTypes[i], visible);
        }
    }

    // 좀비 HP 스택 항목이 모두 켜진 상태인지 확인한다
    private static bool AreAllZombiesVisible(TurretBalanceReportResult report, TurretBalanceReportGraphState state)
    {
        bool hasAny = false;
        Array normalTypes = Enum.GetValues(typeof(NormalZombieType));
        for (int i = 0; i < normalTypes.Length; i++)
        {
            NormalZombieType normalType = (NormalZombieType)normalTypes.GetValue(i);
            if (!HasHpStackSegment(report, false, normalType, default))
            {
                continue;
            }

            hasAny = true;
            if (!state.GetNormalZombieVisible(normalType))
            {
                return false;
            }
        }

        Array bossTypes = Enum.GetValues(typeof(BossZombieType));
        for (int i = 0; i < bossTypes.Length; i++)
        {
            BossZombieType bossType = (BossZombieType)bossTypes.GetValue(i);
            if (!HasHpStackSegment(report, true, default, bossType))
            {
                continue;
            }

            hasAny = true;
            if (!state.GetBossZombieVisible(bossType))
            {
                return false;
            }
        }

        return hasAny;
    }

    // 좀비 HP 스택 항목 전체를 켜거나 끈다
    private static void SetAllZombiesVisible(TurretBalanceReportResult report, TurretBalanceReportGraphState state, bool visible)
    {
        Array normalTypes = Enum.GetValues(typeof(NormalZombieType));
        for (int i = 0; i < normalTypes.Length; i++)
        {
            NormalZombieType normalType = (NormalZombieType)normalTypes.GetValue(i);
            if (HasHpStackSegment(report, false, normalType, default))
            {
                state.SetNormalZombieVisible(normalType, visible);
            }
        }

        Array bossTypes = Enum.GetValues(typeof(BossZombieType));
        for (int i = 0; i < bossTypes.Length; i++)
        {
            BossZombieType bossType = (BossZombieType)bossTypes.GetValue(i);
            if (HasHpStackSegment(report, true, default, bossType))
            {
                state.SetBossZombieVisible(bossType, visible);
            }
        }
    }

    // 터렛 표시 항목이 모두 켜진 상태인지 확인한다
    private static bool AreAllTurretsVisible(TurretBalanceReportGraphState state, List<TurretGraphEntry> turretEntries)
    {
        if (turretEntries.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < turretEntries.Count; i++)
        {
            if (!state.GetTurretVisible(turretEntries[i].TurretName))
            {
                return false;
            }
        }

        return true;
    }

    // 터렛 표시 항목 전체를 켜거나 끈다
    private static void SetAllTurretsVisible(TurretBalanceReportGraphState state, List<TurretGraphEntry> turretEntries, bool visible)
    {
        for (int i = 0; i < turretEntries.Count; i++)
        {
            state.SetTurretVisible(turretEntries[i].TurretName, visible);
        }
    }

    // 활성 세로축 그룹에 맞춰 상위 표시조건을 필터링한다
    private static void ApplyActiveAxisFilter(TurretBalanceReportGraphState state, TurretBalanceReportGraphAxisGroup axisGroup)
    {
        state.ShowTotalWaveHp = axisGroup == TurretBalanceReportGraphAxisGroup.Hp;
        state.ShowTotalZombieHp = axisGroup == TurretBalanceReportGraphAxisGroup.Hp;
        state.ShowAverageZombieHp = axisGroup == TurretBalanceReportGraphAxisGroup.Hp;
        state.ShowClearTimeRatio = axisGroup == TurretBalanceReportGraphAxisGroup.Ratio;
        state.ShowObstacleDestructionRatio = axisGroup == TurretBalanceReportGraphAxisGroup.Ratio;
        state.ShowZombieArrivalRatio = axisGroup == TurretBalanceReportGraphAxisGroup.Ratio;
        state.ShowCumulativeCurrency = axisGroup == TurretBalanceReportGraphAxisGroup.Currency;

        if (axisGroup == TurretBalanceReportGraphAxisGroup.Hp)
        {
            if (!state.ShowTopRankDps && !state.ShowAllTurretDps)
            {
                state.ShowTopRankDps = !state.PreferAllTurretDps;
                state.ShowAllTurretDps = state.PreferAllTurretDps;
            }
        }
        else
        {
            if (state.ShowTopRankDps || state.ShowAllTurretDps)
            {
                state.PreferAllTurretDps = state.ShowAllTurretDps;
            }

            state.ShowTopRankDps = false;
            state.ShowAllTurretDps = false;
        }
    }

    // 시리즈 팔레트에서 인덱스에 해당하는 색상을 반환한다
    private static Color GetSeriesColor(int index)
    {
        Color color = SeriesColors[Mathf.Abs(index) % SeriesColors.Length];
        color.a = 1.0f;
        return color;
    }

    // 상위 표시조건 토글과 단일 그래프 선의 색상을 반환한다
    private static Color GetTopConditionColor(int index)
    {
        return GetSeriesColor(index);
    }

    // 누적 재화 그래프 색상을 반환한다
    private static Color GetCurrencyGraphColor(int index)
    {
        return GetSeriesColor(index);
    }

    // 그래프 영역 안에 배경, 축, 선, 툴팁을 그린다
    private static void DrawGraph(TurretBalanceReportResult report, TurretBalanceReportGraphState state, List<RewardCurrencyType> currencyTypes, Rect graphRect, float targetClearSeconds, float targetClearSecondsIncrement, float obstacleTargetTimeMultiplier, float zombieArrivalSeconds, float zombieArrivalTimeMultiplier, List<ObstacleWaveRow> obstacleRows)
    {
        if (graphRect.width <= GRAPH_LEFT_PADDING + GRAPH_RIGHT_PADDING || graphRect.height <= GRAPH_TOP_PADDING + GRAPH_BOTTOM_PADDING)
        {
            return;
        }

        List<GraphSeries> seriesList = BuildSeriesList(report, state, currencyTypes, targetClearSeconds, targetClearSecondsIncrement, obstacleTargetTimeMultiplier, zombieArrivalSeconds, zombieArrivalTimeMultiplier, obstacleRows);
        GUI.Box(graphRect, GUIContent.none, EditorStyles.helpBox);

        Rect plotRect = new Rect(
            graphRect.x + GRAPH_LEFT_PADDING,
            graphRect.y + GRAPH_TOP_PADDING,
            graphRect.width - GRAPH_LEFT_PADDING - GRAPH_RIGHT_PADDING,
            graphRect.height - GRAPH_TOP_PADDING - GRAPH_BOTTOM_PADDING);

        GraphViewport viewport = BuildGraphViewport(report, state, seriesList);
        HandleGraphInput(plotRect, state, viewport, report.WaveRows.Count);
        ClampGraphViewState(state);
        viewport = BuildGraphViewport(report, state, seriesList);

        DrawGrid(plotRect, report, viewport);
        bool showAnyRatioBaseline = state.ShowClearTimeRatio || state.ShowObstacleDestructionRatio || state.ShowZombieArrivalRatio;
        if (showAnyRatioBaseline)
        {
            DrawRatioBaselines(
                plotRect,
                viewport.RatioMin,
                viewport.RatioMax,
                state.ShowClearTimeRatio,
                state.ShowObstacleDestructionRatio,
                obstacleTargetTimeMultiplier,
                state.ShowZombieArrivalRatio,
                zombieArrivalTimeMultiplier);
        }

        DrawSeriesList(plotRect, seriesList, report.WaveRows.Count, viewport, !state.IsDraggingGraph, out GraphHoverInfo hoverInfo);
        DrawGraphScrollbars(plotRect, state);
        DrawControlHint(graphRect);
        DrawHoverTooltip(graphRect, report, hoverInfo);
    }

    // 현재 표시 상태에 맞는 그래프 선 목록을 만든다
    private static List<GraphSeries> BuildSeriesList(TurretBalanceReportResult report, TurretBalanceReportGraphState state, List<RewardCurrencyType> currencyTypes, float targetClearSeconds, float targetClearSecondsIncrement, float obstacleTargetTimeMultiplier, float zombieArrivalSeconds, float zombieArrivalTimeMultiplier, List<ObstacleWaveRow> obstacleRows)
    {
        List<GraphSeries> seriesList = new List<GraphSeries>(8);
        float hpAxisMax = CalculateHpAxisMax(report, targetClearSeconds, targetClearSecondsIncrement);
        if (state.ShowTotalWaveHp)
        {
            AddZombieHpStackSeries(report, state, seriesList, hpAxisMax);
        }

        if (state.ShowTotalZombieHp)
        {
            seriesList.Add(CreateTotalZombieHpSeries(report, GetTopConditionColor(1), hpAxisMax));
        }

        if (state.ShowAverageZombieHp)
        {
            seriesList.Add(CreateAverageZombieHpSeries(report, GetTopConditionColor(2), hpAxisMax));
        }

        if (state.ShowCumulativeCurrency)
        {
            AddCurrencySeries(report, state, currencyTypes, seriesList);
        }

        if (state.ShowTopRankDps)
        {
            seriesList.Add(CreateTopRankDpsSeries(report, GetTopConditionColor(3), hpAxisMax, targetClearSeconds, targetClearSecondsIncrement));
        }

        if (state.ShowAllTurretDps)
        {
            AddAllTurretDpsSeries(report, state, seriesList, hpAxisMax, targetClearSeconds, targetClearSecondsIncrement);
        }

        bool showAnyRatio = state.ShowClearTimeRatio || state.ShowObstacleDestructionRatio || state.ShowZombieArrivalRatio;
        List<ObstacleWaveRow> obstacleRowsForAxis = state.ShowObstacleDestructionRatio ? obstacleRows : null;
        List<WaveSummaryRow> arrivalRowsForAxis = state.ShowZombieArrivalRatio ? report.WaveRows : null;
        float ratioAxisMax = showAnyRatio ? CalculateRatioAxisMax(report, obstacleRowsForAxis, arrivalRowsForAxis, targetClearSeconds, targetClearSecondsIncrement, obstacleTargetTimeMultiplier, zombieArrivalSeconds, zombieArrivalTimeMultiplier) : 1.0f;
        if (state.ShowClearTimeRatio)
        {
            GraphSeries series = CreateSeries("클리어 시간 / 기준 시간", "배", GetTopConditionColor(5), report.WaveRows.Count);
            series.AxisGroup = TurretBalanceReportGraphAxisGroup.Ratio;
            SetFixedScale(series, 0.0f, ratioAxisMax);
            for (int i = 0; i < report.WaveRows.Count; i++)
            {
                float waveTarget = targetClearSeconds + i * targetClearSecondsIncrement;
                float clearSeconds = i < report.WaveClearRows.Count ? Mathf.Max(0.0f, report.WaveClearRows[i].BestClearSeconds) : 0.0f;
                float value = waveTarget <= 0.0f ? 0.0f : clearSeconds / waveTarget;
                series.Values.Add(value);
                series.PointNotes.Add($"{FormatFloat(clearSeconds)}초 / 기준 {FormatFloat(waveTarget)}초");
            }

            seriesList.Add(series);
        }

        if (state.ShowObstacleDestructionRatio)
        {
            GraphSeries series = CreateSeries("장애물 파괴시간 / 기준 시간", "배", GetTopConditionColor(6), report.WaveRows.Count);
            series.AxisGroup = TurretBalanceReportGraphAxisGroup.Ratio;
            SetFixedScale(series, 0.0f, ratioAxisMax);
            for (int i = 0; i < report.WaveRows.Count; i++)
            {
                float waveTarget = targetClearSeconds + i * targetClearSecondsIncrement;
                bool hasData = obstacleRows != null && i < obstacleRows.Count
                    && obstacleRows[i].Optimal.HasValue && obstacleRows[i].DestructionTime > 0f && waveTarget > 0f;
                if (hasData)
                {
                    float obstacleTarget = waveTarget * obstacleTargetTimeMultiplier;
                    float ratio = obstacleRows[i].DestructionTime / waveTarget;
                    series.Values.Add(ratio);
                    series.PointNotes.Add($"파괴시간 {FormatFloat(obstacleRows[i].DestructionTime)}초 / 기준 {FormatFloat(waveTarget)}초 / 목표 {FormatFloat(obstacleTarget)}초");
                }
                else
                {
                    series.Values.Add(float.NaN);
                    series.PointNotes.Add("데이터 없음");
                }
            }

            seriesList.Add(series);
        }

        if (state.ShowZombieArrivalRatio)
        {
            GraphSeries series = CreateSeries("좀비 전체 도달 시간 / 기준 시간", "배", GetTopConditionColor(7), report.WaveRows.Count);
            series.AxisGroup = TurretBalanceReportGraphAxisGroup.Ratio;
            SetFixedScale(series, 0.0f, ratioAxisMax);
            for (int i = 0; i < report.WaveRows.Count; i++)
            {
                float waveTarget = targetClearSeconds + i * targetClearSecondsIncrement;
                WaveSummaryRow waveRow = report.WaveRows[i];
                int spawnCount = waveRow.SpawnCount;
                float totalArrivalTime = spawnCount > 1
                    ? waveRow.SpawnInterval * (spawnCount - 1) + zombieArrivalSeconds
                    : zombieArrivalSeconds;
                float ratio = waveTarget <= 0.0f ? 0.0f : totalArrivalTime / waveTarget;
                series.Values.Add(ratio);
                series.PointNotes.Add($"스폰간격 {FormatFloat(waveRow.SpawnInterval)}초 × {spawnCount - 1}회 + 도달 {FormatFloat(zombieArrivalSeconds)}초 = {FormatFloat(totalArrivalTime)}초 / 기준 {FormatFloat(waveTarget)}초");
            }

            seriesList.Add(series);
        }

        return seriesList;
    }

    // 웨이브별 총 좀비 HP 그래프 선을 만든다
    private static GraphSeries CreateTotalZombieHpSeries(TurretBalanceReportResult report, Color color, float hpAxisMax)
    {
        GraphSeries series = CreateSeries("총 좀비 HP", "HP", color, report.WaveRows.Count);
        SetFixedScale(series, 0.0f, hpAxisMax);
        for (int i = 0; i < report.WaveRows.Count; i++)
        {
            WaveSummaryRow row = report.WaveRows[i];
            float totalHp = Mathf.Max(0.0f, row.TotalWaveHp);
            series.Values.Add(totalHp);
            series.PointNotes.Add($"총 {FormatFloat(totalHp)} HP / 일반 {row.NormalSpawnCount}마리 / 보스 {row.BossSpawnCount}마리");
        }

        return series;
    }

    // 웨이브별 일반 좀비 1마리 평균 HP 그래프 선을 만든다
    private static GraphSeries CreateAverageZombieHpSeries(TurretBalanceReportResult report, Color color, float hpAxisMax)
    {
        GraphSeries series = CreateSeries("일반 좀비 1마리 평균 HP", "HP", color, report.WaveRows.Count);
        SetFixedScale(series, 0.0f, hpAxisMax);
        for (int i = 0; i < report.WaveRows.Count; i++)
        {
            WaveSummaryRow row = report.WaveRows[i];
            int normalSpawnCount = Mathf.Max(0, row.NormalSpawnCount);
            float averageNormalHp = normalSpawnCount <= 0 ? 0.0f : Mathf.Max(0.0f, row.AverageNormalZombieHp);
            float totalNormalHp = averageNormalHp * normalSpawnCount;
            series.Values.Add(averageNormalHp);
            series.PointNotes.Add($"일반 평균 {FormatFloat(averageNormalHp)} HP / 일반 총 {FormatFloat(totalNormalHp)} HP / 일반 {normalSpawnCount}마리");
        }

        return series;
    }

    // 좀비 타입별 누적 HP 스택 그래프 선을 추가한다
    private static void AddZombieHpStackSeries(TurretBalanceReportResult report, TurretBalanceReportGraphState state, List<GraphSeries> seriesList, float hpAxisMax)
    {
        Array normalTypes = Enum.GetValues(typeof(NormalZombieType));
        int colorIndex = 0;
        for (int i = 0; i < normalTypes.Length; i++)
        {
            NormalZombieType normalType = (NormalZombieType)normalTypes.GetValue(i);
            if (!HasHpStackSegment(report, false, normalType, default))
            {
                continue;
            }

            if (!state.GetNormalZombieVisible(normalType))
            {
                colorIndex++;
                continue;
            }

            AddZombieHpStackSeriesForType(report, seriesList, hpAxisMax, normalType.ToString(), false, normalType, default, GetSeriesColor(colorIndex));
            colorIndex++;
        }

        Array bossTypes = Enum.GetValues(typeof(BossZombieType));
        for (int i = 0; i < bossTypes.Length; i++)
        {
            BossZombieType bossType = (BossZombieType)bossTypes.GetValue(i);
            if (!HasHpStackSegment(report, true, default, bossType))
            {
                continue;
            }

            if (!state.GetBossZombieVisible(bossType))
            {
                colorIndex++;
                continue;
            }

            AddZombieHpStackSeriesForType(report, seriesList, hpAxisMax, bossType.ToString(), true, default, bossType, GetSeriesColor(colorIndex));
            colorIndex++;
        }
    }

    // 특정 좀비 타입의 누적 HP 스택 그래프 선을 추가한다
    private static void AddZombieHpStackSeriesForType(TurretBalanceReportResult report, List<GraphSeries> seriesList, float hpAxisMax, string label, bool isBoss, NormalZombieType normalType, BossZombieType bossType, Color color)
    {
        GraphSeries series = CreateSeries("HP 스택 - " + label, "HP", color, report.WaveRows.Count);
        SetFixedScale(series, 0.0f, hpAxisMax);
        bool hasValue = false;
        for (int waveIndex = 0; waveIndex < report.WaveRows.Count; waveIndex++)
        {
            WaveSummaryRow row = report.WaveRows[waveIndex];
            if (TryGetHpStackSegment(row, isBoss, normalType, bossType, out ZombieHpStackSegment segment))
            {
                series.Values.Add(Mathf.Max(0.0f, segment.CumulativeHp));
                string countNote = segment.IsBoss ? string.Empty : $" / 기대 {FormatFloat(segment.ExpectedCount)}마리";
                series.PointNotes.Add($"{segment.Label}: +{FormatFloat(segment.Hp)} HP{countNote} / 누적 {FormatFloat(segment.CumulativeHp)} HP");
                hasValue = true;
                continue;
            }

            series.Values.Add(float.NaN);
            series.PointNotes.Add(string.Empty);
        }

        if (hasValue)
        {
            seriesList.Add(series);
        }
    }

    // 웨이브 행에서 특정 좀비 타입의 HP 스택 세그먼트를 찾는다
    private static bool TryGetHpStackSegment(WaveSummaryRow row, bool isBoss, NormalZombieType normalType, BossZombieType bossType, out ZombieHpStackSegment segment)
    {
        if (row.HpStackSegments != null)
        {
            for (int i = 0; i < row.HpStackSegments.Count; i++)
            {
                ZombieHpStackSegment candidate = row.HpStackSegments[i];
                if (candidate.IsBoss != isBoss)
                {
                    continue;
                }

                if ((!isBoss && candidate.NormalType == normalType) || (isBoss && candidate.BossType == bossType))
                {
                    segment = candidate;
                    return true;
                }
            }
        }

        segment = default;
        return false;
    }

    // 리포트에 특정 좀비 타입의 HP 스택 데이터가 있는지 확인한다
    private static bool HasHpStackSegment(TurretBalanceReportResult report, bool isBoss, NormalZombieType normalType, BossZombieType bossType)
    {
        if (report == null)
        {
            return false;
        }

        for (int i = 0; i < report.WaveRows.Count; i++)
        {
            if (TryGetHpStackSegment(report.WaveRows[i], isBoss, normalType, bossType, out _))
            {
                return true;
            }
        }

        return false;
    }

    // 클리어 시간 배율과 장애물 파괴시간 배율을 합산한 축 최대값을 계산한다
    private static float CalculateRatioAxisMax(TurretBalanceReportResult report, List<ObstacleWaveRow> obstacleRows, List<WaveSummaryRow> arrivalRows, float targetClearSeconds, float targetClearSecondsIncrement, float obstacleTargetTimeMultiplier, float zombieArrivalSeconds, float zombieArrivalTimeMultiplier)
    {
        float maxValue = 1.0f;
        for (int i = 0; i < report.WaveClearRows.Count; i++)
        {
            float waveTarget = targetClearSeconds + i * targetClearSecondsIncrement;
            if (waveTarget <= 0.0f)
            {
                continue;
            }

            maxValue = Mathf.Max(maxValue, report.WaveClearRows[i].BestClearSeconds / waveTarget);
        }

        if (obstacleRows != null)
        {
            maxValue = Mathf.Max(maxValue, obstacleTargetTimeMultiplier);
            for (int i = 0; i < obstacleRows.Count; i++)
            {
                float waveTarget = targetClearSeconds + i * targetClearSecondsIncrement;
                if (waveTarget <= 0.0f || !obstacleRows[i].Optimal.HasValue || obstacleRows[i].DestructionTime <= 0f)
                {
                    continue;
                }

                maxValue = Mathf.Max(maxValue, obstacleRows[i].DestructionTime / waveTarget);
            }
        }

        if (arrivalRows != null)
        {
            maxValue = Mathf.Max(maxValue, zombieArrivalTimeMultiplier);
            for (int i = 0; i < arrivalRows.Count; i++)
            {
                float waveTarget = targetClearSeconds + i * targetClearSecondsIncrement;
                if (waveTarget <= 0.0f)
                {
                    continue;
                }

                int spawnCount = arrivalRows[i].SpawnCount;
                float totalArrivalTime = spawnCount > 1
                    ? arrivalRows[i].SpawnInterval * (spawnCount - 1) + zombieArrivalSeconds
                    : zombieArrivalSeconds;
                maxValue = Mathf.Max(maxValue, totalArrivalTime / waveTarget);
            }
        }

        return Mathf.Max(1.0f, maxValue);
    }

    // 그래프 위 마우스 입력으로 확대/축소와 드래그 이동을 처리한다
    private static void HandleGraphInput(Rect plotRect, TurretBalanceReportGraphState state, GraphViewport viewport, int waveCount)
    {
        Event currentEvent = Event.current;
        if (currentEvent == null)
        {
            return;
        }

        if (currentEvent.type == EventType.ScrollWheel && plotRect.Contains(currentEvent.mousePosition))
        {
            HandleGraphWheelZoom(plotRect, state, viewport, waveCount, currentEvent);
            return;
        }

        HandleGraphDrag(plotRect, state, viewport, waveCount, currentEvent);
    }

    // 마우스 휠로 활성 세로축 또는 웨이브 축을 확대/축소한다
    private static void HandleGraphWheelZoom(Rect plotRect, TurretBalanceReportGraphState state, GraphViewport viewport, int waveCount, Event currentEvent)
    {
        float multiplier = currentEvent.delta.y < 0.0f ? AXIS_ZOOM_STEP : 1.0f / AXIS_ZOOM_STEP;
        if (currentEvent.shift)
        {
            float anchorRatio = Mathf.InverseLerp(plotRect.xMin, plotRect.xMax, currentEvent.mousePosition.x);
            float anchorValue = Mathf.Lerp(viewport.WaveMin, viewport.WaveMax, anchorRatio);
            state.WaveAxisZoom = ClampZoom(state.WaveAxisZoom * multiplier);
            state.WaveAxisOffset01 = CalculateOffsetForAnchor(0.0f, Mathf.Max(0.0f, waveCount - 1.0f), state.WaveAxisZoom, anchorValue, anchorRatio);
        }
        else
        {
            float anchorRatio = Mathf.InverseLerp(plotRect.yMax, plotRect.yMin, currentEvent.mousePosition.y);
            GetActiveVerticalRange(viewport, state.ActiveVerticalAxisGroup, out float visibleMin, out float visibleMax);
            float anchorValue = Mathf.Lerp(visibleMin, visibleMax, anchorRatio);
            state.SetActiveVerticalZoom(ClampZoom(state.GetActiveVerticalZoom() * multiplier));
            GetBaseVerticalRange(viewport, state.ActiveVerticalAxisGroup, out float baseMin, out float baseMax);
            state.SetActiveVerticalOffset01(CalculateOffsetForAnchor(baseMin, baseMax, state.GetActiveVerticalZoom(), anchorValue, anchorRatio));
        }

        GUI.changed = true;
        currentEvent.Use();
    }

    // 마우스 드래그로 웨이브 축과 활성 세로축을 이동한다
    private static void HandleGraphDrag(Rect plotRect, TurretBalanceReportGraphState state, GraphViewport viewport, int waveCount, Event currentEvent)
    {
        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && plotRect.Contains(currentEvent.mousePosition))
        {
            state.IsDraggingGraph = true;
            state.LastDragMousePosition = currentEvent.mousePosition;
            GUI.changed = true;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
        {
            state.IsDraggingGraph = false;
            GUI.changed = true;
            return;
        }

        if (!state.IsDraggingGraph || currentEvent.type != EventType.MouseDrag || currentEvent.button != 0)
        {
            return;
        }

        Vector2 delta = currentEvent.mousePosition - state.LastDragMousePosition;
        state.LastDragMousePosition = currentEvent.mousePosition;

        float waveSpan = Mathf.Max(0.0f, viewport.WaveMax - viewport.WaveMin);
        float waveValueDelta = plotRect.width <= 0.0f ? 0.0f : -delta.x / plotRect.width * waveSpan;
        state.WaveAxisOffset01 = PanOffset(0.0f, Mathf.Max(0.0f, waveCount - 1.0f), state.WaveAxisZoom, state.WaveAxisOffset01, waveValueDelta);

        GetActiveVerticalRange(viewport, state.ActiveVerticalAxisGroup, out float visibleMin, out float visibleMax);
        GetBaseVerticalRange(viewport, state.ActiveVerticalAxisGroup, out float baseMin, out float baseMax);
        float verticalSpan = Mathf.Max(0.0f, visibleMax - visibleMin);
        float verticalValueDelta = plotRect.height <= 0.0f ? 0.0f : delta.y / plotRect.height * verticalSpan;
        state.SetActiveVerticalOffset01(PanOffset(baseMin, baseMax, state.GetActiveVerticalZoom(), state.GetActiveVerticalOffset01(), verticalValueDelta));

        GUI.changed = true;
        currentEvent.Use();
    }

    // 그래프 축 상태를 허용 범위 안으로 보정한다
    private static void ClampGraphViewState(TurretBalanceReportGraphState state)
    {
        state.WaveAxisZoom = ClampZoom(state.WaveAxisZoom);
        state.WaveAxisOffset01 = Mathf.Clamp01(state.WaveAxisOffset01);
        state.HpAxisZoom = ClampZoom(state.HpAxisZoom);
        state.HpAxisOffset01 = Mathf.Clamp01(state.HpAxisOffset01);
        state.RatioAxisZoom = ClampZoom(state.RatioAxisZoom);
        state.RatioAxisOffset01 = Mathf.Clamp01(state.RatioAxisOffset01);
        state.CurrencyAxisZoom = ClampZoom(state.CurrencyAxisZoom);
        state.CurrencyAxisOffset01 = Mathf.Clamp01(state.CurrencyAxisOffset01);
    }

    // 축 확대 배율을 허용 범위로 제한한다
    private static float ClampZoom(float zoom)
    {
        return Mathf.Clamp(float.IsNaN(zoom) || float.IsInfinity(zoom) ? 1.0f : zoom, AXIS_MIN_ZOOM, AXIS_MAX_ZOOM);
    }

    // 기준 범위와 확대 배율에서 앵커 값을 유지하는 스크롤 위치를 계산한다
    private static float CalculateOffsetForAnchor(float baseMin, float baseMax, float zoom, float anchorValue, float anchorRatio)
    {
        float baseSpan = Mathf.Max(0.0f, baseMax - baseMin);
        if (baseSpan <= 0.0001f || zoom <= 1.0001f)
        {
            return 0.0f;
        }

        float visibleSpan = baseSpan / ClampZoom(zoom);
        float maxStart = Mathf.Max(0.0f, baseSpan - visibleSpan);
        float start = Mathf.Clamp(anchorValue - baseMin - Mathf.Clamp01(anchorRatio) * visibleSpan, 0.0f, maxStart);
        return maxStart <= 0.0001f ? 0.0f : Mathf.Clamp01(start / maxStart);
    }

    // 현재 스크롤 위치에 데이터 단위 이동량을 더한다
    private static float PanOffset(float baseMin, float baseMax, float zoom, float offset01, float valueDelta)
    {
        float baseSpan = Mathf.Max(0.0f, baseMax - baseMin);
        if (baseSpan <= 0.0001f || zoom <= 1.0001f)
        {
            return 0.0f;
        }

        float visibleSpan = baseSpan / ClampZoom(zoom);
        float maxStart = Mathf.Max(0.0f, baseSpan - visibleSpan);
        float start = Mathf.Clamp(Mathf.Clamp01(offset01) * maxStart + valueDelta, 0.0f, maxStart);
        return maxStart <= 0.0001f ? 0.0f : Mathf.Clamp01(start / maxStart);
    }

    // 현재 축 확대/스크롤 상태에서 보이는 그래프 범위를 계산한다
    private static GraphViewport BuildGraphViewport(TurretBalanceReportResult report, TurretBalanceReportGraphState state, List<GraphSeries> seriesList)
    {
        GraphViewport viewport = new GraphViewport();
        float waveMax = Mathf.Max(0.0f, report.WaveRows.Count - 1.0f);
        CalculateVisibleRange(0.0f, waveMax, state.WaveAxisZoom, state.WaveAxisOffset01, out viewport.WaveMin, out viewport.WaveMax);

        float hpMax = CalculateGroupBaseMax(seriesList, TurretBalanceReportGraphAxisGroup.Hp);
        float ratioMax = CalculateGroupBaseMax(seriesList, TurretBalanceReportGraphAxisGroup.Ratio);
        float currencyMax = CalculateGroupBaseMax(seriesList, TurretBalanceReportGraphAxisGroup.Currency);
        viewport.HpBaseMin = 0.0f;
        viewport.HpBaseMax = hpMax;
        viewport.RatioBaseMin = 0.0f;
        viewport.RatioBaseMax = ratioMax;
        viewport.CurrencyBaseMin = 0.0f;
        viewport.CurrencyBaseMax = currencyMax;
        CalculateVisibleRange(viewport.HpBaseMin, viewport.HpBaseMax, state.HpAxisZoom, state.HpAxisOffset01, out viewport.HpMin, out viewport.HpMax);
        CalculateVisibleRange(viewport.RatioBaseMin, viewport.RatioBaseMax, state.RatioAxisZoom, state.RatioAxisOffset01, out viewport.RatioMin, out viewport.RatioMax);
        CalculateVisibleRange(viewport.CurrencyBaseMin, viewport.CurrencyBaseMax, state.CurrencyAxisZoom, state.CurrencyAxisOffset01, out viewport.CurrencyMin, out viewport.CurrencyMax);
        return viewport;
    }

    // 축 기본 범위와 확대/스크롤 상태에서 실제 표시 범위를 계산한다
    private static void CalculateVisibleRange(float baseMin, float baseMax, float zoom, float offset01, out float visibleMin, out float visibleMax)
    {
        float safeBaseMax = Mathf.Max(baseMin + 1.0f, baseMax);
        float baseSpan = safeBaseMax - baseMin;
        float safeZoom = ClampZoom(zoom);
        float visibleSpan = baseSpan / safeZoom;
        float maxStart = Mathf.Max(0.0f, baseSpan - visibleSpan);
        visibleMin = baseMin + Mathf.Clamp01(offset01) * maxStart;
        visibleMax = visibleMin + visibleSpan;
    }

    // 특정 축 그룹의 기본 최대값을 시리즈 목록에서 찾는다
    private static float CalculateGroupBaseMax(List<GraphSeries> seriesList, TurretBalanceReportGraphAxisGroup axisGroup)
    {
        float maxValue = 1.0f;
        for (int i = 0; i < seriesList.Count; i++)
        {
            GraphSeries series = seriesList[i];
            if (series.AxisGroup != axisGroup)
            {
                continue;
            }

            GetSeriesScale(series, out _, out float seriesMax);
            maxValue = Mathf.Max(maxValue, seriesMax);
        }

        return maxValue;
    }

    // 활성 세로축 그룹의 현재 표시 범위를 반환한다
    private static void GetActiveVerticalRange(GraphViewport viewport, TurretBalanceReportGraphAxisGroup axisGroup, out float minValue, out float maxValue)
    {
        switch (axisGroup)
        {
            case TurretBalanceReportGraphAxisGroup.Ratio:
                minValue = viewport.RatioMin;
                maxValue = viewport.RatioMax;
                break;
            case TurretBalanceReportGraphAxisGroup.Currency:
                minValue = viewport.CurrencyMin;
                maxValue = viewport.CurrencyMax;
                break;
            default:
                minValue = viewport.HpMin;
                maxValue = viewport.HpMax;
                break;
        }
    }

    // 활성 세로축 그룹의 기본 전체 범위를 반환한다
    private static void GetBaseVerticalRange(GraphViewport viewport, TurretBalanceReportGraphAxisGroup axisGroup, out float minValue, out float maxValue)
    {
        switch (axisGroup)
        {
            case TurretBalanceReportGraphAxisGroup.Ratio:
                minValue = viewport.RatioBaseMin;
                maxValue = viewport.RatioBaseMax;
                break;
            case TurretBalanceReportGraphAxisGroup.Currency:
                minValue = viewport.CurrencyBaseMin;
                maxValue = viewport.CurrencyBaseMax;
                break;
            default:
                minValue = viewport.HpBaseMin;
                maxValue = viewport.HpBaseMax;
                break;
        }
    }

    // 좀비 총 HP와 기준 시간 처리 가능 HP를 포함한 공통 HP 축 최대값을 계산한다
    private static float CalculateHpAxisMax(TurretBalanceReportResult report, float targetClearSeconds, float targetClearSecondsIncrement)
    {
        float maxValue = 0.0f;
        for (int i = 0; i < report.WaveRows.Count; i++)
        {
            WaveSummaryRow row = report.WaveRows[i];
            if (row.HpStackSegments != null && row.HpStackSegments.Count > 0)
            {
                for (int j = 0; j < row.HpStackSegments.Count; j++)
                {
                    maxValue = Mathf.Max(maxValue, row.HpStackSegments[j].CumulativeHp);
                }
            }
            else
            {
                maxValue = Mathf.Max(maxValue, row.TotalWaveHp);
            }
        }

        for (int i = 0; i < report.WaveClearRows.Count; i++)
        {
            List<WaveClearRankEntry> topRanks = report.WaveClearRows[i].TopRanks;
            if (topRanks != null && topRanks.Count > 0)
            {
                float waveTarget = targetClearSeconds + i * targetClearSecondsIncrement;
                maxValue = Mathf.Max(maxValue, topRanks[0].CriticalExpectedTotalDps * waveTarget);
            }

            List<WaveClearRankEntry> speciesEntries = report.WaveClearRows[i].SpeciesEntries;
            if (speciesEntries == null)
            {
                continue;
            }

            for (int j = 0; j < speciesEntries.Count; j++)
            {
                float waveTarget = targetClearSeconds + i * targetClearSecondsIncrement;
                maxValue = Mathf.Max(maxValue, speciesEntries[j].CriticalExpectedTotalDps * waveTarget);
            }
        }

        return Mathf.Max(1.0f, maxValue);
    }

    // 모든 터렛 종류의 웨이브별 최적 총 DPS 그래프 선을 추가한다
    private static void AddAllTurretDpsSeries(TurretBalanceReportResult report, TurretBalanceReportGraphState state, List<GraphSeries> seriesList, float hpAxisMax, float targetClearSeconds, float targetClearSecondsIncrement)
    {
        List<TurretGraphEntry> turretEntries = BuildTurretGraphEntries(report);
        int maxTier = GetMaxTurretTier(turretEntries);
        for (int turretIndex = 0; turretIndex < turretEntries.Count; turretIndex++)
        {
            TurretGraphEntry turretEntry = turretEntries[turretIndex];
            if (!state.GetTurretVisible(turretEntry.TurretName))
            {
                continue;
            }

            Color criticalColor = GetTurretEvolutionColor(turretEntry.Tier, maxTier, turretIndex);
            criticalColor.a = 1.0f;
            GraphSeries criticalSeries = CreateSeries("특수타 기대 HP - " + turretEntry.TurretName, "HP", criticalColor, report.WaveRows.Count);
            SetFixedScale(criticalSeries, 0.0f, hpAxisMax);
            for (int waveIndex = 0; waveIndex < report.WaveRows.Count; waveIndex++)
            {
                if (TryFindSpeciesEntry(report, waveIndex, turretEntry.TurretName, out WaveClearRankEntry entry))
                {
                    float waveTarget = targetClearSeconds + waveIndex * targetClearSecondsIncrement;
                    float criticalProcessableHp = Mathf.Max(0.0f, entry.CriticalExpectedTotalDps * waveTarget);
                    criticalSeries.Values.Add(criticalProcessableHp);
                    criticalSeries.PointNotes.Add($"치명타/강타 기대 {FormatFloat(entry.CriticalExpectedTotalDps)} DPS / 기존 {FormatFloat(entry.TotalDps)} DPS / {entry.InstallCount}대 / Lv{entry.Level}");
                    continue;
                }

                criticalSeries.Values.Add(float.NaN);
                criticalSeries.PointNotes.Add("설치 불가");
            }

            seriesList.Add(criticalSeries);
        }
    }

    // 리포트 상세 행에서 터렛 이름과 세대 목록을 만든다
    private static List<TurretGraphEntry> BuildTurretGraphEntries(TurretBalanceReportResult report)
    {
        List<TurretGraphEntry> turretEntries = new List<TurretGraphEntry>(report.SpeciesDetailRows.Count);
        for (int i = 0; i < report.SpeciesDetailRows.Count; i++)
        {
            string turretName = report.SpeciesDetailRows[i].TurretName;
            if (!string.IsNullOrEmpty(turretName) && !ContainsTurretName(turretEntries, turretName))
            {
                turretEntries.Add(new TurretGraphEntry
                {
                    TurretName = turretName,
                    Tier = report.SpeciesDetailRows[i].Tier
                });
            }
        }

        return turretEntries;
    }

    // 터렛 이름이 이미 목록에 있는지 확인한다
    private static bool ContainsTurretName(List<TurretGraphEntry> turretEntries, string turretName)
    {
        for (int i = 0; i < turretEntries.Count; i++)
        {
            if (turretEntries[i].TurretName == turretName)
            {
                return true;
            }
        }

        return false;
    }

    // 터렛 목록에서 가장 깊은 진화 단계를 찾는다
    private static int GetMaxTurretTier(List<TurretGraphEntry> turretEntries)
    {
        int maxTier = 0;
        for (int i = 0; i < turretEntries.Count; i++)
        {
            maxTier = Mathf.Max(maxTier, turretEntries[i].Tier);
        }

        return maxTier;
    }

    // 터렛 진화 단계에 따라 초록에서 파랑을 거쳐 보라로 변하는 색상을 반환한다
    private static Color GetTurretEvolutionColor(int tier, int maxTier, int colorIndex)
    {
        float ratio = maxTier <= 0 ? 0.0f : Mathf.Clamp01(tier / (float)maxTier);
        Color baseColor;
        if (ratio <= 0.5f)
        {
            baseColor = Color.Lerp(EvolutionStartColor, EvolutionMiddleColor, ratio * 2.0f);
        }
        else
        {
            baseColor = Color.Lerp(EvolutionMiddleColor, EvolutionEndColor, (ratio - 0.5f) * 2.0f);
        }

        return ApplyTurretColorVariant(baseColor, colorIndex);
    }

    // 같은 진화 단계의 터렛 선을 구분하기 위해 색상 밝기를 조금 조정한다
    private static Color ApplyTurretColorVariant(Color color, int colorIndex)
    {
        int variantIndex = colorIndex % 5;
        float strength = (variantIndex - 2) * 0.06f;
        Color targetColor = strength >= 0.0f ? Color.white : Color.black;
        Color adjustedColor = Color.Lerp(color, targetColor, Mathf.Abs(strength));
        adjustedColor.a = color.a;

        return adjustedColor;
    }

    // 지정 웨이브의 터렛 종류별 후보 목록에서 이름이 일치하는 항목을 찾는다
    private static bool TryFindSpeciesEntry(TurretBalanceReportResult report, int waveIndex, string turretName, out WaveClearRankEntry entry)
    {
        entry = default;
        if (waveIndex < 0 || waveIndex >= report.WaveClearRows.Count)
        {
            return false;
        }

        List<WaveClearRankEntry> entries = report.WaveClearRows[waveIndex].SpeciesEntries;
        if (entries == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].TurretName == turretName)
            {
                entry = entries[i];
                return true;
            }
        }

        return false;
    }

    // 누적 재화 그래프 선을 목록에 추가한다
    private static void AddCurrencySeries(TurretBalanceReportResult report, TurretBalanceReportGraphState state, List<RewardCurrencyType> currencyTypes, List<GraphSeries> seriesList)
    {
        HashSet<RewardCurrencyType> currencyScope = TurretBalanceReportCurrencyProjector.BuildTurretCurrencyScope(report);
        float currencyAxisMax = CalculateCurrencyAxisMax(report, state, currencyTypes, currencyScope);
        for (int currencyIndex = 0; currencyIndex < currencyTypes.Count; currencyIndex++)
        {
            RewardCurrencyType currencyType = currencyTypes[currencyIndex];
            if (!state.GetCurrencyVisible(currencyType))
            {
                continue;
            }

            GraphSeries series = CreateSeries("누적 " + GetCurrencyLabel(currencyType), GetCurrencyLabel(currencyType), GetCurrencyGraphColor(currencyIndex), report.WaveRows.Count);
            series.AxisGroup = TurretBalanceReportGraphAxisGroup.Currency;
            SetFixedScale(series, 0.0f, currencyAxisMax);
            for (int i = 0; i < report.WaveRows.Count; i++)
            {
                Dictionary<RewardCurrencyType, float> rewards = i < report.ItemBalanceRows.Count
                    ? TurretBalanceReportCurrencyProjector.FilterItemAmounts(report.ItemBalanceRows[i], currencyScope)
                    : new Dictionary<RewardCurrencyType, float>();
                float value = rewards.TryGetValue(currencyType, out float amount) ? Mathf.Max(0.0f, amount) : 0.0f;
                series.Values.Add(value);
                series.PointNotes.Add(string.Empty);
            }

            seriesList.Add(series);
        }
    }

    // 표시 중인 누적 재화 그래프가 공유할 세로축 최대값을 계산한다
    private static float CalculateCurrencyAxisMax(TurretBalanceReportResult report, TurretBalanceReportGraphState state, List<RewardCurrencyType> currencyTypes, HashSet<RewardCurrencyType> currencyScope)
    {
        float maxValue = 1.0f;
        for (int i = 0; i < report.WaveRows.Count; i++)
        {
            Dictionary<RewardCurrencyType, float> rewards = i < report.ItemBalanceRows.Count
                ? TurretBalanceReportCurrencyProjector.FilterItemAmounts(report.ItemBalanceRows[i], currencyScope)
                : null;
            if (rewards == null)
            {
                continue;
            }

            for (int currencyIndex = 0; currencyIndex < currencyTypes.Count; currencyIndex++)
            {
                RewardCurrencyType currencyType = currencyTypes[currencyIndex];
                if (!state.GetCurrencyVisible(currencyType))
                {
                    continue;
                }

                if (rewards.TryGetValue(currencyType, out float amount))
                {
                    maxValue = Mathf.Max(maxValue, Mathf.Max(0.0f, amount));
                }
            }
        }

        return maxValue;
    }
    // 1순위 터렛의 치명타/강타 기대 처리 가능 HP 그래프 선을 만든다
    private static GraphSeries CreateTopRankDpsSeries(TurretBalanceReportResult report, Color color, float hpAxisMax, float targetClearSeconds, float targetClearSecondsIncrement)
    {
        GraphSeries series = CreateSeries("1순위 특수타 기대 HP", "HP", color, report.WaveRows.Count);
        SetFixedScale(series, 0.0f, hpAxisMax);
        for (int i = 0; i < report.WaveRows.Count; i++)
        {
            if (i < report.WaveClearRows.Count && report.WaveClearRows[i].TopRanks != null && report.WaveClearRows[i].TopRanks.Count > 0)
            {
                WaveClearRankEntry entry = report.WaveClearRows[i].TopRanks[0];
                float waveTarget = targetClearSeconds + i * targetClearSecondsIncrement;
                float processableHp = Mathf.Max(0.0f, entry.CriticalExpectedTotalDps * waveTarget);
                series.Values.Add(processableHp);
                series.PointNotes.Add($"{entry.TurretName} / 치명타/강타 기대 {FormatFloat(entry.CriticalExpectedTotalDps)} DPS / 기존 {FormatFloat(entry.TotalDps)} DPS / {entry.InstallCount}대 / Lv{entry.Level}");
                continue;
            }

            series.Values.Add(0.0f);
            series.PointNotes.Add("설치 불가");
        }

        return series;
    }

    // 그래프 선 기본 데이터를 만든다
    private static GraphSeries CreateSeries(string name, string unit, Color color, int capacity)
    {
        return new GraphSeries
        {
            Name = name,
            Unit = unit,
            Color = color,
            Values = new List<float>(capacity),
            PointNotes = new List<string>(capacity)
        };
    }

    // 그래프 선에 고정 정규화 범위를 설정한다
    private static void SetFixedScale(GraphSeries series, float minValue, float maxValue)
    {
        series.UseFixedScale = true;
        series.FixedMinValue = minValue;
        series.FixedMaxValue = Mathf.Max(minValue, maxValue);
    }

    // 그래프 배경 격자와 축 라벨을 그린다
    private static void DrawGrid(Rect plotRect, TurretBalanceReportResult report, GraphViewport viewport)
    {
        EditorGUI.DrawRect(plotRect, new Color(0.11f, 0.11f, 0.11f, 0.18f));
        Handles.BeginGUI();
        Handles.color = new Color(0.55f, 0.55f, 0.55f, 0.35f);
        for (int i = 0; i <= 4; i++)
        {
            float t = i / 4.0f;
            float y = Mathf.Lerp(plotRect.yMax, plotRect.yMin, t);
            Handles.DrawLine(new Vector3(plotRect.xMin, y), new Vector3(plotRect.xMax, y));
        }

        for (int i = 0; i <= 6; i++)
        {
            float t = i / 6.0f;
            float x = Mathf.Lerp(plotRect.xMin, plotRect.xMax, t);
            Handles.DrawLine(new Vector3(x, plotRect.yMin), new Vector3(x, plotRect.yMax));
        }

        Handles.color = Color.white;
        Handles.DrawLine(new Vector3(plotRect.xMin, plotRect.yMin), new Vector3(plotRect.xMin, plotRect.yMax));
        Handles.DrawLine(new Vector3(plotRect.xMin, plotRect.yMax), new Vector3(plotRect.xMax, plotRect.yMax));
        Handles.EndGUI();

        GUI.Label(new Rect(plotRect.xMin - 52.0f, plotRect.yMin - 2.0f, 48.0f, 20.0f), "최대", EditorStyles.miniLabel);
        GUI.Label(new Rect(plotRect.xMin - 52.0f, plotRect.yMax - 18.0f, 48.0f, 20.0f), "최소", EditorStyles.miniLabel);
        int minWaveIndex = Mathf.Clamp(Mathf.RoundToInt(viewport.WaveMin), 0, report.WaveRows.Count - 1);
        int maxWaveIndex = Mathf.Clamp(Mathf.RoundToInt(viewport.WaveMax), 0, report.WaveRows.Count - 1);
        GUI.Label(new Rect(plotRect.xMin, plotRect.yMax + 6.0f, 160.0f, 20.0f), GetWaveLabel(report, minWaveIndex), EditorStyles.miniLabel);
        GUI.Label(new Rect(plotRect.xMax - 160.0f, plotRect.yMax + 6.0f, 160.0f, 20.0f), GetWaveLabel(report, maxWaveIndex), EditorStyles.miniLabel);
    }

    // 표시 중인 배율 그래프의 기준선을 그린다
    private static void DrawRatioBaselines(Rect plotRect, float visibleMin, float visibleMax, bool showClearBaseline, bool showObstacleBaseline, float obstacleTargetTimeMultiplier, bool showArrivalBaseline, float zombieArrivalTimeMultiplier)
    {
        if (showClearBaseline)
        {
            DrawRatioBaseline(plotRect, visibleMin, visibleMax, 1.0f, "기준 1.0x");
        }

        if (showObstacleBaseline)
        {
            float obstacleBaseline = Mathf.Max(0.1f, obstacleTargetTimeMultiplier);
            DrawRatioBaseline(plotRect, visibleMin, visibleMax, obstacleBaseline, $"장애물 기준 {FormatFloat(obstacleBaseline)}x");
        }

        if (showArrivalBaseline)
        {
            float arrivalBaseline = Mathf.Max(0.01f, zombieArrivalTimeMultiplier);
            DrawRatioBaseline(plotRect, visibleMin, visibleMax, arrivalBaseline, $"도달 기준 {FormatFloat(arrivalBaseline)}x");
        }
    }

    // 지정 배율 위치에 기준선을 그린다
    private static void DrawRatioBaseline(Rect plotRect, float visibleMin, float visibleMax, float baselineRatio, string label)
    {
        if (baselineRatio <= 0.0f || baselineRatio < visibleMin || baselineRatio > visibleMax)
        {
            return;
        }

        float yRatio = Mathf.Approximately(visibleMax, visibleMin) ? 1.0f : Mathf.InverseLerp(visibleMin, visibleMax, baselineRatio);
        float y = Mathf.Lerp(plotRect.yMax, plotRect.yMin, yRatio);
        Handles.BeginGUI();
        Handles.color = new Color(1.0f, 1.0f, 1.0f, 0.45f);
        Handles.DrawLine(new Vector3(plotRect.xMin, y), new Vector3(plotRect.xMax, y));
        Handles.EndGUI();
        GUI.Label(new Rect(plotRect.xMin + 4.0f, y - 18.0f, 140.0f, 18.0f), label, EditorStyles.miniLabel);
    }

    // 모든 그래프 선을 그리고 마우스 hover 정보를 찾는다
    private static void DrawSeriesList(Rect plotRect, List<GraphSeries> seriesList, int waveCount, GraphViewport viewport, bool allowHover, out GraphHoverInfo hoverInfo)
    {
        hoverInfo = new GraphHoverInfo { HasValue = false, Distance = float.MaxValue };
        if (waveCount <= 0 || seriesList.Count == 0)
        {
            GUI.Label(plotRect, "표시할 선이 없습니다.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Vector2 mousePosition = Event.current.mousePosition;
        bool canHover = allowHover && plotRect.Contains(mousePosition);
        Handles.BeginGUI();
        for (int i = 0; i < seriesList.Count; i++)
        {
            DrawSeriesLine(plotRect, seriesList[i], waveCount, viewport, mousePosition, canHover, ref hoverInfo);
        }

        Handles.EndGUI();
    }

    // 그래프 선 하나를 그리고 hover 후보를 갱신한다
    private static void DrawSeriesLine(Rect plotRect, GraphSeries series, int waveCount, GraphViewport viewport, Vector2 mousePosition, bool allowHover, ref GraphHoverInfo hoverInfo)
    {
        if (series.Values == null || series.Values.Count == 0)
        {
            return;
        }

        GetSeriesVisibleScale(series, viewport, out float minValue, out float maxValue);
        int pointCount = series.Values.Count;
        EnsurePointBuffers(pointCount);
        for (int i = 0; i < pointCount; i++)
        {
            reusableValidPointBuffer[i] = IsValidGraphValue(series.Values[i]) && i >= viewport.WaveMin && i <= viewport.WaveMax && series.Values[i] >= minValue && series.Values[i] <= maxValue;
            if (reusableValidPointBuffer[i])
            {
                reusablePointBuffer[i] = CalculatePoint(plotRect, i, series.Values[i], viewport.WaveMin, viewport.WaveMax, minValue, maxValue);
            }
        }

        Handles.color = series.Color;
        DrawValidLineSegments(reusablePointBuffer, reusableValidPointBuffer, pointCount);
        for (int i = 0; i < pointCount; i++)
        {
            if (!reusableValidPointBuffer[i])
            {
                continue;
            }

            Rect pointRect = new Rect(reusablePointBuffer[i].x - 2.0f, reusablePointBuffer[i].y - 2.0f, 4.0f, 4.0f);
            EditorGUI.DrawRect(pointRect, series.Color);
        }

        if (allowHover)
        {
            UpdateHoverInfo(series, reusablePointBuffer, reusableValidPointBuffer, pointCount, mousePosition, ref hoverInfo);
        }
    }

    // 그래프 선 렌더링에 사용할 재사용 버퍼 크기를 보장한다
    private static void EnsurePointBuffers(int pointCount)
    {
        if (reusablePointBuffer.Length >= pointCount && reusableValidPointBuffer.Length >= pointCount)
        {
            return;
        }

        reusablePointBuffer = new Vector3[pointCount];
        reusableValidPointBuffer = new bool[pointCount];
    }

    // 유효한 값끼리 이어진 선분만 그린다
    private static void DrawValidLineSegments(Vector3[] points, bool[] validPoints, int pointCount)
    {
        for (int i = 0; i < pointCount - 1; i++)
        {
            if (validPoints[i] && validPoints[i + 1])
            {
                Handles.DrawAAPolyLine(2.4f, points[i], points[i + 1]);
            }
        }
    }

    // 그래프 선의 정규화 범위를 반환한다
    private static void GetSeriesScale(GraphSeries series, out float minValue, out float maxValue)
    {
        if (series.UseFixedScale)
        {
            minValue = series.FixedMinValue;
            maxValue = series.FixedMaxValue;
            return;
        }

        CalculateMinMax(series.Values, out minValue, out maxValue);
    }

    // 그래프 선의 축 그룹에 맞는 현재 표시 범위를 반환한다
    private static void GetSeriesVisibleScale(GraphSeries series, GraphViewport viewport, out float minValue, out float maxValue)
    {
        switch (series.AxisGroup)
        {
            case TurretBalanceReportGraphAxisGroup.Ratio:
                minValue = viewport.RatioMin;
                maxValue = viewport.RatioMax;
                break;
            case TurretBalanceReportGraphAxisGroup.Currency:
                minValue = viewport.CurrencyMin;
                maxValue = viewport.CurrencyMax;
                break;
            default:
                minValue = viewport.HpMin;
                maxValue = viewport.HpMax;
                break;
        }
    }

    // 값 목록의 최소/최대값을 계산한다
    private static void CalculateMinMax(List<float> values, out float minValue, out float maxValue)
    {
        minValue = float.MaxValue;
        maxValue = float.MinValue;
        for (int i = 0; i < values.Count; i++)
        {
            float value = values[i];
            if (!IsValidGraphValue(value))
            {
                continue;
            }

            if (value < minValue)
            {
                minValue = value;
            }

            if (value > maxValue)
            {
                maxValue = value;
            }
        }

        if (minValue == float.MaxValue)
        {
            minValue = 0.0f;
            maxValue = 0.0f;
        }
    }

    // 그래프에 표시할 수 있는 유효한 실수 값인지 확인한다
    private static bool IsValidGraphValue(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // 그래프 좌표계에서 값 하나의 위치를 계산한다
    private static Vector3 CalculatePoint(Rect plotRect, int index, float value, float waveMin, float waveMax, float minValue, float maxValue)
    {
        float xRatio = Mathf.Approximately(waveMax, waveMin) ? 0.0f : Mathf.InverseLerp(waveMin, waveMax, index);
        float yRatio = Mathf.Approximately(maxValue, minValue) ? 0.5f : Mathf.InverseLerp(minValue, maxValue, value);
        float x = Mathf.Lerp(plotRect.xMin, plotRect.xMax, xRatio);
        float y = Mathf.Lerp(plotRect.yMax, plotRect.yMin, yRatio);
        return new Vector3(x, y, 0.0f);
    }

    // 마우스와 가까운 선분을 찾아 hover 정보를 갱신한다
    private static void UpdateHoverInfo(GraphSeries series, Vector3[] points, bool[] validPoints, int pointCount, Vector2 mousePosition, ref GraphHoverInfo hoverInfo)
    {
        if (pointCount <= 0)
        {
            return;
        }

        if (pointCount == 1)
        {
            if (validPoints[0])
            {
                UpdatePointHoverInfo(series, 0, Vector2.Distance(mousePosition, points[0]), mousePosition, ref hoverInfo);
            }

            return;
        }

        for (int i = 0; i < pointCount - 1; i++)
        {
            if (!validPoints[i] || !validPoints[i + 1])
            {
                continue;
            }

            float segmentRatio;
            float distance = DistanceToSegment(mousePosition, points[i], points[i + 1], out segmentRatio);
            int pointIndex = segmentRatio < 0.5f ? i : i + 1;
            UpdatePointHoverInfo(series, pointIndex, distance, mousePosition, ref hoverInfo);
        }
    }

    // 단일 점 hover 후보를 갱신한다
    private static void UpdatePointHoverInfo(GraphSeries series, int pointIndex, float distance, Vector2 mousePosition, ref GraphHoverInfo hoverInfo)
    {
        if (distance > HOVER_DISTANCE || distance >= hoverInfo.Distance || pointIndex < 0 || pointIndex >= series.Values.Count)
        {
            return;
        }

        hoverInfo.HasValue = true;
        hoverInfo.Distance = distance;
        hoverInfo.MousePosition = mousePosition;
        hoverInfo.SeriesName = series.Name;
        hoverInfo.Value = series.Values[pointIndex];
        hoverInfo.Unit = series.Unit;
        hoverInfo.PointIndex = pointIndex;
        hoverInfo.Note = pointIndex < series.PointNotes.Count ? series.PointNotes[pointIndex] : string.Empty;
    }

    // 마우스와 선분 사이의 최단 거리를 계산한다
    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end, out float segmentRatio)
    {
        Vector2 segment = end - start;
        float segmentLengthSqr = segment.sqrMagnitude;
        if (segmentLengthSqr <= 0.0001f)
        {
            segmentRatio = 0.0f;
            return Vector2.Distance(point, start);
        }

        segmentRatio = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentLengthSqr);
        Vector2 projected = start + segment * segmentRatio;
        return Vector2.Distance(point, projected);
    }

    // hover 툴팁을 그린다
    private static void DrawHoverTooltip(Rect graphRect, TurretBalanceReportResult report, GraphHoverInfo hoverInfo)
    {
        if (!hoverInfo.HasValue || !graphRect.Contains(hoverInfo.MousePosition))
        {
            return;
        }

        string valueText = FormatFloat(hoverInfo.Value);
        string tooltip = $"조건: {hoverInfo.SeriesName}\n웨이브: {GetWaveLabel(report, hoverInfo.PointIndex)}\n값: {valueText} {hoverInfo.Unit}";
        if (!string.IsNullOrEmpty(hoverInfo.Note))
        {
            tooltip += "\n" + hoverInfo.Note;
        }

        Vector2 size = EditorStyles.helpBox.CalcSize(new GUIContent(tooltip));
        Rect tooltipRect = new Rect(hoverInfo.MousePosition.x + 14.0f, hoverInfo.MousePosition.y + 14.0f, Mathf.Max(180.0f, size.x + 18.0f), size.y + 12.0f);
        if (tooltipRect.xMax > graphRect.xMax)
        {
            tooltipRect.x = hoverInfo.MousePosition.x - tooltipRect.width - 14.0f;
        }

        if (tooltipRect.yMax > graphRect.yMax)
        {
            tooltipRect.y = hoverInfo.MousePosition.y - tooltipRect.height - 14.0f;
        }

        GUI.Box(tooltipRect, tooltip, EditorStyles.helpBox);
    }

    // 확대된 그래프를 이동할 수 있는 스크롤바를 그린다
    private static void DrawGraphScrollbars(Rect plotRect, TurretBalanceReportGraphState state)
    {
        if (state.WaveAxisZoom > 1.0001f)
        {
            Rect horizontalRect = new Rect(plotRect.xMin, plotRect.yMax + 4.0f, plotRect.width, SCROLLBAR_SIZE);
            float visibleSize = 1.0f / ClampZoom(state.WaveAxisZoom);
            state.WaveAxisOffset01 = GUI.HorizontalScrollbar(horizontalRect, state.WaveAxisOffset01, visibleSize, 0.0f, 1.0f);
        }

        if (state.GetActiveVerticalZoom() > 1.0001f)
        {
            Rect verticalRect = new Rect(plotRect.xMax + 4.0f, plotRect.yMin, SCROLLBAR_SIZE, plotRect.height);
            float visibleSize = 1.0f / ClampZoom(state.GetActiveVerticalZoom());
            state.SetActiveVerticalOffset01(GUI.VerticalScrollbar(verticalRect, state.GetActiveVerticalOffset01(), visibleSize, 0.0f, 1.0f));
        }
    }

    // 그래프 조작법을 최하단 우측에 표시한다
    private static void DrawControlHint(Rect graphRect)
    {
        Rect hintRect = new Rect(graphRect.xMax - CONTROL_HINT_WIDTH - 10.0f, graphRect.yMax - 20.0f, CONTROL_HINT_WIDTH, 18.0f);
        GUI.Label(hintRect, "휠: 세로 확대 | Shift+휠: 웨이브 확대 | 드래그: 이동", EditorStyles.miniLabel);
    }

    // 웨이브 행의 표시 라벨을 반환한다
    private static string GetWaveLabel(TurretBalanceReportResult report, int index)
    {
        if (report == null || index < 0 || index >= report.WaveRows.Count)
        {
            return "W-";
        }

        return report.WaveRows[index].WaveLabel;
    }

    // 재화 종류의 표기용 이름을 반환한다
    private static string GetCurrencyLabel(RewardCurrencyType currencyType)
    {
        switch (currencyType)
        {
            case RewardCurrencyType.Coin:
                return "코인";
            default:
                return currencyType.ToString();
        }
    }

    // 실수 값을 그래프 표시용 문자열로 변환한다
    private static string FormatFloat(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    // 그래프 선 하나의 원본 값과 표시 정보를 보관한다.
    private sealed class GraphSeries
    {
        public string Name;
        public string Unit;
        public Color Color;
        public TurretBalanceReportGraphAxisGroup AxisGroup;
        public List<float> Values;
        public List<string> PointNotes;
        public bool UseFixedScale;
        public float FixedMinValue;
        public float FixedMaxValue;
    }

    // 현재 그래프에서 보이는 가로축과 세로축 범위를 보관한다.
    private struct GraphViewport
    {
        public float WaveMin;
        public float WaveMax;
        public float HpBaseMin;
        public float HpBaseMax;
        public float HpMin;
        public float HpMax;
        public float RatioBaseMin;
        public float RatioBaseMax;
        public float RatioMin;
        public float RatioMax;
        public float CurrencyBaseMin;
        public float CurrencyBaseMax;
        public float CurrencyMin;
        public float CurrencyMax;
    }

    // 터렛 그래프 선의 이름과 세대 정보를 보관한다.
    private struct TurretGraphEntry
    {
        public string TurretName;
        public int Tier;
    }

    // 그래프 hover 상태를 보관한다.
    private struct GraphHoverInfo
    {
        public bool HasValue;
        public float Distance;
        public Vector2 MousePosition;
        public string SeriesName;
        public float Value;
        public string Unit;
        public int PointIndex;
        public string Note;
    }
}
