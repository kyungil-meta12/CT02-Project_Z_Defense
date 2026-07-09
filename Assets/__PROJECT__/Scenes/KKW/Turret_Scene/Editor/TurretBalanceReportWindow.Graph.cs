using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

// 터렛 웨이브 밸런스 리포트 그래프 탭에서 사용할 표시 상태를 보관한다.
internal sealed class TurretBalanceReportGraphState
{
    public bool ShowTotalWaveHp;
    public bool ShowTotalZombieHp;
    public bool ShowAverageZombieHp;
    public bool ShowCumulativeCurrency;
    public bool ShowTopRankDps;
    public bool ShowAllTurretDps;
    public bool ShowClearTimeRatio;
    public bool ShowObstacleDestructionRatio;
    public bool ShowZombieArrivalRatio;
    public float HpAxisScaleMultiplier = 1.0f;
    public readonly Dictionary<RewardCurrencyType, bool> CurrencyVisibility = new Dictionary<RewardCurrencyType, bool>();
    public readonly Dictionary<NormalZombieType, bool> NormalZombieVisibility = new Dictionary<NormalZombieType, bool>();
    public readonly Dictionary<BossZombieType, bool> BossZombieVisibility = new Dictionary<BossZombieType, bool>();
    public readonly Dictionary<string, bool> TurretVisibility = new Dictionary<string, bool>();

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
    private const float HP_AXIS_MIN_SCALE = 0.0001f;
    private const float HP_AXIS_MAX_SCALE = 20.0f;
    private const float HP_AXIS_ZOOM_STEP = 1.12f;
    private const int ZOMBIE_TOGGLE_COLUMNS = 4;
    private const int TURRET_TOGGLE_COLUMNS = 4;

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

    // 그래프 탭 전체 UI를 그린다
    public static void Draw(TurretBalanceReportResult report, TurretBalanceReportGraphState state, float targetClearSeconds, float targetClearSecondsIncrement, float obstacleTargetTimeMultiplier, float zombieArrivalSeconds, float zombieArrivalTimeMultiplier, List<ObstacleWaveRow> obstacleRows)
    {
        if (state == null)
        {
            return;
        }

        EditorGUILayout.HelpBox("가로축은 웨이브입니다. 좀비 총 HP와 터렛 DPS 계열은 같은 HP 축을 사용합니다. 터렛 선은 총 DPS × 기준 클리어 시간으로 변환한 처리 가능 HP입니다. 그래프 위 마우스 휠로 HP 축 최대값을 조절합니다.", MessageType.Info);
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

        EditorGUILayout.BeginHorizontal();
        state.ShowTotalWaveHp = EditorGUILayout.ToggleLeft("좀비 HP 스택", state.ShowTotalWaveHp, GUILayout.Width(120.0f));
        state.ShowTotalZombieHp = EditorGUILayout.ToggleLeft("총 좀비 HP", state.ShowTotalZombieHp, GUILayout.Width(110.0f));
        state.ShowAverageZombieHp = EditorGUILayout.ToggleLeft("일반 좀비 1마리 평균 HP", state.ShowAverageZombieHp, GUILayout.Width(170.0f));
        bool nextShowTopRankDps = EditorGUILayout.ToggleLeft("1순위 터렛 DPS", state.ShowTopRankDps, GUILayout.Width(140.0f));
        if (nextShowTopRankDps)
        {
            state.ShowAllTurretDps = false;
        }

        state.ShowTopRankDps = nextShowTopRankDps;

        bool nextShowAllTurretDps = EditorGUILayout.ToggleLeft("모든 터렛 DPS", state.ShowAllTurretDps, GUILayout.Width(140.0f));
        if (nextShowAllTurretDps)
        {
            state.ShowTopRankDps = false;
        }

        state.ShowAllTurretDps = nextShowAllTurretDps;
        state.ShowClearTimeRatio = EditorGUILayout.ToggleLeft("클리어 시간 배율", state.ShowClearTimeRatio, GUILayout.Width(140.0f));
        state.ShowObstacleDestructionRatio = EditorGUILayout.ToggleLeft("장애물 파괴시간 배율", state.ShowObstacleDestructionRatio, GUILayout.Width(160.0f));
        state.ShowZombieArrivalRatio = EditorGUILayout.ToggleLeft("좀비 도달시간 배율", state.ShowZombieArrivalRatio, GUILayout.Width(150.0f));
        state.ShowCumulativeCurrency = EditorGUILayout.ToggleLeft("누적 재화", state.ShowCumulativeCurrency, GUILayout.Width(100.0f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"HP 축 {state.HpAxisScaleMultiplier:0.##}x", EditorStyles.miniLabel, GUILayout.Width(76.0f));
        if (GUILayout.Button("HP 축 초기화", EditorStyles.miniButton, GUILayout.Width(84.0f)))
        {
            state.HpAxisScaleMultiplier = 1.0f;
        }

        EditorGUILayout.EndHorizontal();

        if (state.ShowCumulativeCurrency && currencyTypes.Count > 0)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("재화", GUILayout.Width(36.0f));
            for (int i = 0; i < currencyTypes.Count; i++)
            {
                RewardCurrencyType currencyType = currencyTypes[i];
                bool visible = EditorGUILayout.ToggleLeft(GetCurrencyLabel(currencyType), state.GetCurrencyVisible(currencyType), GUILayout.Width(120.0f));
                state.SetCurrencyVisible(currencyType, visible);
            }

            EditorGUILayout.EndHorizontal();
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

    // 좀비 타입별 HP 스택 표시 토글을 그린다
    private static void DrawZombieVisibilityToggles(TurretBalanceReportResult report, TurretBalanceReportGraphState state)
    {
        Array normalTypes = Enum.GetValues(typeof(NormalZombieType));
        int normalColumn = 0;
        for (int i = 0; i < normalTypes.Length; i++)
        {
            NormalZombieType normalType = (NormalZombieType)normalTypes.GetValue(i);
            if (!HasHpStackSegment(report, false, normalType, default))
            {
                continue;
            }

            if (normalColumn % ZOMBIE_TOGGLE_COLUMNS == 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(normalColumn == 0 ? "일반" : string.Empty, GUILayout.Width(36.0f));
            }

            bool visible = EditorGUILayout.ToggleLeft(normalType.ToString(), state.GetNormalZombieVisible(normalType), GUILayout.Width(136.0f));
            state.SetNormalZombieVisible(normalType, visible);
            normalColumn++;
            if (normalColumn % ZOMBIE_TOGGLE_COLUMNS == 0)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        if (normalColumn > 0 && normalColumn % ZOMBIE_TOGGLE_COLUMNS != 0)
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("보스", GUILayout.Width(36.0f));
        Array bossTypes = Enum.GetValues(typeof(BossZombieType));
        for (int i = 0; i < bossTypes.Length; i++)
        {
            BossZombieType bossType = (BossZombieType)bossTypes.GetValue(i);
            if (!HasHpStackSegment(report, true, default, bossType))
            {
                continue;
            }

            bool visible = EditorGUILayout.ToggleLeft(bossType.ToString(), state.GetBossZombieVisible(bossType), GUILayout.Width(120.0f));
            state.SetBossZombieVisible(bossType, visible);
        }

        EditorGUILayout.EndHorizontal();
    }

    // 터렛별 DPS 표시 토글을 그린다
    private static void DrawTurretVisibilityToggles(TurretBalanceReportResult report, TurretBalanceReportGraphState state)
    {
        List<TurretGraphEntry> turretEntries = BuildTurretGraphEntries(report);
        int turretColumn = 0;
        for (int i = 0; i < turretEntries.Count; i++)
        {
            string turretName = turretEntries[i].TurretName;
            if (string.IsNullOrEmpty(turretName))
            {
                continue;
            }

            if (turretColumn % TURRET_TOGGLE_COLUMNS == 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(turretColumn == 0 ? "터렛" : string.Empty, GUILayout.Width(36.0f));
            }

            bool visible = EditorGUILayout.ToggleLeft(turretName, state.GetTurretVisible(turretName), GUILayout.Width(136.0f));
            state.SetTurretVisible(turretName, visible);
            turretColumn++;
            if (turretColumn % TURRET_TOGGLE_COLUMNS == 0)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        if (turretColumn > 0 && turretColumn % TURRET_TOGGLE_COLUMNS != 0)
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
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

        HandleHpAxisWheelZoom(plotRect, state);
        DrawGrid(plotRect, report);
        bool showAnyRatioBaseline = state.ShowClearTimeRatio || state.ShowObstacleDestructionRatio || state.ShowZombieArrivalRatio;
        if (showAnyRatioBaseline)
        {
            List<ObstacleWaveRow> obstacleRowsForAxis = state.ShowObstacleDestructionRatio ? obstacleRows : null;
            List<WaveSummaryRow> arrivalRowsForAxis = state.ShowZombieArrivalRatio ? report.WaveRows : null;
            DrawRatioBaselines(
                plotRect,
                CalculateRatioAxisMax(report, obstacleRowsForAxis, arrivalRowsForAxis, targetClearSeconds, targetClearSecondsIncrement, obstacleTargetTimeMultiplier, zombieArrivalSeconds, zombieArrivalTimeMultiplier),
                state.ShowClearTimeRatio,
                state.ShowObstacleDestructionRatio,
                obstacleTargetTimeMultiplier,
                state.ShowZombieArrivalRatio,
                zombieArrivalTimeMultiplier);
        }

        DrawSeriesList(plotRect, seriesList, report.WaveRows.Count, out GraphHoverInfo hoverInfo);
        DrawLegend(graphRect, seriesList);
        DrawHoverTooltip(graphRect, report, hoverInfo);
    }

    // 현재 표시 상태에 맞는 그래프 선 목록을 만든다
    private static List<GraphSeries> BuildSeriesList(TurretBalanceReportResult report, TurretBalanceReportGraphState state, List<RewardCurrencyType> currencyTypes, float targetClearSeconds, float targetClearSecondsIncrement, float obstacleTargetTimeMultiplier, float zombieArrivalSeconds, float zombieArrivalTimeMultiplier, List<ObstacleWaveRow> obstacleRows)
    {
        List<GraphSeries> seriesList = new List<GraphSeries>(8);
        int colorIndex = 0;
        float hpAxisMax = CalculateHpAxisMax(report, targetClearSeconds, targetClearSecondsIncrement) * Mathf.Clamp(state.HpAxisScaleMultiplier, HP_AXIS_MIN_SCALE, HP_AXIS_MAX_SCALE);
        if (state.ShowTotalWaveHp)
        {
            AddZombieHpStackSeries(report, state, seriesList, ref colorIndex, hpAxisMax);
        }

        if (state.ShowTotalZombieHp)
        {
            seriesList.Add(CreateTotalZombieHpSeries(report, SeriesColors[colorIndex++ % SeriesColors.Length], hpAxisMax));
        }

        if (state.ShowAverageZombieHp)
        {
            seriesList.Add(CreateAverageZombieHpSeries(report, SeriesColors[colorIndex++ % SeriesColors.Length], hpAxisMax));
        }

        if (state.ShowCumulativeCurrency)
        {
            AddCurrencySeries(report, state, currencyTypes, seriesList, ref colorIndex);
        }

        if (state.ShowTopRankDps)
        {
            seriesList.Add(CreateTopRankDpsSeries(report, SeriesColors[colorIndex++ % SeriesColors.Length], hpAxisMax, targetClearSeconds, targetClearSecondsIncrement));
        }

        if (state.ShowAllTurretDps)
        {
            AddAllTurretDpsSeries(report, state, seriesList, ref colorIndex, hpAxisMax, targetClearSeconds, targetClearSecondsIncrement);
        }

        bool showAnyRatio = state.ShowClearTimeRatio || state.ShowObstacleDestructionRatio || state.ShowZombieArrivalRatio;
        List<ObstacleWaveRow> obstacleRowsForAxis = state.ShowObstacleDestructionRatio ? obstacleRows : null;
        List<WaveSummaryRow> arrivalRowsForAxis = state.ShowZombieArrivalRatio ? report.WaveRows : null;
        float ratioAxisMax = showAnyRatio ? CalculateRatioAxisMax(report, obstacleRowsForAxis, arrivalRowsForAxis, targetClearSeconds, targetClearSecondsIncrement, obstacleTargetTimeMultiplier, zombieArrivalSeconds, zombieArrivalTimeMultiplier) : 1.0f;
        if (state.ShowClearTimeRatio)
        {
            GraphSeries series = CreateSeries("클리어 시간 / 기준 시간", "배", SeriesColors[colorIndex++ % SeriesColors.Length], report.WaveRows.Count);
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
            GraphSeries series = CreateSeries("장애물 파괴시간 / 기준 시간", "배", SeriesColors[colorIndex++ % SeriesColors.Length], report.WaveRows.Count);
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
            GraphSeries series = CreateSeries("좀비 전체 도달 시간 / 기준 시간", "배", SeriesColors[colorIndex++ % SeriesColors.Length], report.WaveRows.Count);
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
    private static void AddZombieHpStackSeries(TurretBalanceReportResult report, TurretBalanceReportGraphState state, List<GraphSeries> seriesList, ref int colorIndex, float hpAxisMax)
    {
        Array normalTypes = Enum.GetValues(typeof(NormalZombieType));
        for (int i = 0; i < normalTypes.Length; i++)
        {
            NormalZombieType normalType = (NormalZombieType)normalTypes.GetValue(i);
            if (!state.GetNormalZombieVisible(normalType))
            {
                continue;
            }

            AddZombieHpStackSeriesForType(report, seriesList, ref colorIndex, hpAxisMax, normalType.ToString(), false, normalType, default);
        }

        Array bossTypes = Enum.GetValues(typeof(BossZombieType));
        for (int i = 0; i < bossTypes.Length; i++)
        {
            BossZombieType bossType = (BossZombieType)bossTypes.GetValue(i);
            if (!state.GetBossZombieVisible(bossType))
            {
                continue;
            }

            AddZombieHpStackSeriesForType(report, seriesList, ref colorIndex, hpAxisMax, bossType.ToString(), true, default, bossType);
        }
    }

    // 특정 좀비 타입의 누적 HP 스택 그래프 선을 추가한다
    private static void AddZombieHpStackSeriesForType(TurretBalanceReportResult report, List<GraphSeries> seriesList, ref int colorIndex, float hpAxisMax, string label, bool isBoss, NormalZombieType normalType, BossZombieType bossType)
    {
        GraphSeries series = CreateSeries("HP 스택 - " + label, "HP", SeriesColors[colorIndex % SeriesColors.Length], report.WaveRows.Count);
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
            colorIndex++;
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

    // 그래프 위 마우스 휠 입력으로 HP 축 최대값 배율을 조절한다
    private static void HandleHpAxisWheelZoom(Rect plotRect, TurretBalanceReportGraphState state)
    {
        Event currentEvent = Event.current;
        if (currentEvent == null || currentEvent.type != EventType.ScrollWheel || !plotRect.Contains(currentEvent.mousePosition))
        {
            return;
        }

        float multiplier = currentEvent.delta.y < 0.0f ? 1.0f / HP_AXIS_ZOOM_STEP : HP_AXIS_ZOOM_STEP;
        state.HpAxisScaleMultiplier = Mathf.Clamp(state.HpAxisScaleMultiplier * multiplier, HP_AXIS_MIN_SCALE, HP_AXIS_MAX_SCALE);
        currentEvent.Use();
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
    private static void AddAllTurretDpsSeries(TurretBalanceReportResult report, TurretBalanceReportGraphState state, List<GraphSeries> seriesList, ref int colorIndex, float hpAxisMax, float targetClearSeconds, float targetClearSecondsIncrement)
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
            colorIndex++;
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
    private static void AddCurrencySeries(TurretBalanceReportResult report, TurretBalanceReportGraphState state, List<RewardCurrencyType> currencyTypes, List<GraphSeries> seriesList, ref int colorIndex)
    {
        HashSet<RewardCurrencyType> currencyScope = TurretBalanceReportCurrencyProjector.BuildTurretCurrencyScope(report);
        for (int currencyIndex = 0; currencyIndex < currencyTypes.Count; currencyIndex++)
        {
            RewardCurrencyType currencyType = currencyTypes[currencyIndex];
            if (!state.GetCurrencyVisible(currencyType))
            {
                continue;
            }

            GraphSeries series = CreateSeries("누적 " + GetCurrencyLabel(currencyType), GetCurrencyLabel(currencyType), SeriesColors[colorIndex++ % SeriesColors.Length], report.WaveRows.Count);
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
    private static void DrawGrid(Rect plotRect, TurretBalanceReportResult report)
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
        GUI.Label(new Rect(plotRect.xMin, plotRect.yMax + 6.0f, 160.0f, 20.0f), GetWaveLabel(report, 0), EditorStyles.miniLabel);
        GUI.Label(new Rect(plotRect.xMax - 160.0f, plotRect.yMax + 6.0f, 160.0f, 20.0f), GetWaveLabel(report, report.WaveRows.Count - 1), EditorStyles.miniLabel);
    }

    // 표시 중인 배율 그래프의 기준선을 그린다
    private static void DrawRatioBaselines(Rect plotRect, float ratioAxisMax, bool showClearBaseline, bool showObstacleBaseline, float obstacleTargetTimeMultiplier, bool showArrivalBaseline, float zombieArrivalTimeMultiplier)
    {
        if (showClearBaseline)
        {
            DrawRatioBaseline(plotRect, ratioAxisMax, 1.0f, "기준 1.0x");
        }

        if (showObstacleBaseline)
        {
            float obstacleBaseline = Mathf.Max(0.1f, obstacleTargetTimeMultiplier);
            DrawRatioBaseline(plotRect, ratioAxisMax, obstacleBaseline, $"장애물 기준 {FormatFloat(obstacleBaseline)}x");
        }

        if (showArrivalBaseline)
        {
            float arrivalBaseline = Mathf.Max(0.01f, zombieArrivalTimeMultiplier);
            DrawRatioBaseline(plotRect, ratioAxisMax, arrivalBaseline, $"도달 기준 {FormatFloat(arrivalBaseline)}x");
        }
    }

    // 지정 배율 위치에 기준선을 그린다
    private static void DrawRatioBaseline(Rect plotRect, float ratioAxisMax, float baselineRatio, string label)
    {
        if (baselineRatio <= 0.0f)
        {
            return;
        }

        float yRatio = Mathf.Approximately(ratioAxisMax, 0.0f) ? 1.0f : Mathf.Clamp01(baselineRatio / ratioAxisMax);
        float y = Mathf.Lerp(plotRect.yMax, plotRect.yMin, yRatio);
        Handles.BeginGUI();
        Handles.color = new Color(1.0f, 1.0f, 1.0f, 0.45f);
        Handles.DrawLine(new Vector3(plotRect.xMin, y), new Vector3(plotRect.xMax, y));
        Handles.EndGUI();
        GUI.Label(new Rect(plotRect.xMin + 4.0f, y - 18.0f, 140.0f, 18.0f), label, EditorStyles.miniLabel);
    }

    // 모든 그래프 선을 그리고 마우스 hover 정보를 찾는다
    private static void DrawSeriesList(Rect plotRect, List<GraphSeries> seriesList, int waveCount, out GraphHoverInfo hoverInfo)
    {
        hoverInfo = new GraphHoverInfo { HasValue = false, Distance = float.MaxValue };
        if (waveCount <= 0 || seriesList.Count == 0)
        {
            GUI.Label(plotRect, "표시할 선이 없습니다.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Vector2 mousePosition = Event.current.mousePosition;
        Handles.BeginGUI();
        for (int i = 0; i < seriesList.Count; i++)
        {
            DrawSeriesLine(plotRect, seriesList[i], waveCount, mousePosition, ref hoverInfo);
        }

        Handles.EndGUI();
    }

    // 그래프 선 하나를 그리고 hover 후보를 갱신한다
    private static void DrawSeriesLine(Rect plotRect, GraphSeries series, int waveCount, Vector2 mousePosition, ref GraphHoverInfo hoverInfo)
    {
        if (series.Values == null || series.Values.Count == 0)
        {
            return;
        }

        GetSeriesScale(series, out float minValue, out float maxValue);
        Vector3[] points = new Vector3[series.Values.Count];
        bool[] validPoints = new bool[series.Values.Count];
        for (int i = 0; i < series.Values.Count; i++)
        {
            validPoints[i] = IsValidGraphValue(series.Values[i]);
            if (validPoints[i])
            {
                points[i] = CalculatePoint(plotRect, i, waveCount, series.Values[i], minValue, maxValue);
            }
        }

        Handles.color = series.Color;
        DrawValidLineSegments(points, validPoints);
        for (int i = 0; i < points.Length; i++)
        {
            if (!validPoints[i])
            {
                continue;
            }

            Rect pointRect = new Rect(points[i].x - 2.0f, points[i].y - 2.0f, 4.0f, 4.0f);
            EditorGUI.DrawRect(pointRect, series.Color);
        }

        UpdateHoverInfo(series, points, validPoints, mousePosition, ref hoverInfo);
    }

    // 유효한 값끼리 이어진 선분만 그린다
    private static void DrawValidLineSegments(Vector3[] points, bool[] validPoints)
    {
        for (int i = 0; i < points.Length - 1; i++)
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
    private static Vector3 CalculatePoint(Rect plotRect, int index, int waveCount, float value, float minValue, float maxValue)
    {
        float xRatio = waveCount <= 1 ? 0.0f : index / (float)(waveCount - 1);
        float yRatio = Mathf.Approximately(maxValue, minValue) ? 0.5f : Mathf.InverseLerp(minValue, maxValue, value);
        float x = Mathf.Lerp(plotRect.xMin, plotRect.xMax, xRatio);
        float y = Mathf.Lerp(plotRect.yMax, plotRect.yMin, yRatio);
        return new Vector3(x, y, 0.0f);
    }

    // 마우스와 가까운 선분을 찾아 hover 정보를 갱신한다
    private static void UpdateHoverInfo(GraphSeries series, Vector3[] points, bool[] validPoints, Vector2 mousePosition, ref GraphHoverInfo hoverInfo)
    {
        if (points.Length <= 0)
        {
            return;
        }

        if (points.Length == 1)
        {
            if (validPoints[0])
            {
                UpdatePointHoverInfo(series, 0, Vector2.Distance(mousePosition, points[0]), mousePosition, ref hoverInfo);
            }

            return;
        }

        for (int i = 0; i < points.Length - 1; i++)
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

    // 그래프 범례를 그린다
    private static void DrawLegend(Rect graphRect, List<GraphSeries> seriesList)
    {
        float x = graphRect.x + 10.0f;
        float y = graphRect.y + 8.0f;
        for (int i = 0; i < seriesList.Count; i++)
        {
            GraphSeries series = seriesList[i];
            Rect colorRect = new Rect(x, y + 4.0f, 10.0f, 10.0f);
            EditorGUI.DrawRect(colorRect, series.Color);
            GUI.Label(new Rect(x + 14.0f, y, 240.0f, 18.0f), series.Name, EditorStyles.miniLabel);
            y += 18.0f;
        }
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
        public List<float> Values;
        public List<string> PointNotes;
        public bool UseFixedScale;
        public float FixedMinValue;
        public float FixedMaxValue;
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
