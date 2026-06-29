using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

// 터렛 웨이브 밸런스 리포트 그래프 탭에서 사용할 표시 상태를 보관한다.
internal sealed class TurretBalanceReportGraphState
{
    public bool ShowTotalWaveHp;
    public bool ShowCumulativeCurrency;
    public bool ShowTopRankDps;
    public bool ShowAllTurretDps;
    public bool ShowClearTimeRatio;
    public float HpAxisScaleMultiplier = 1.0f;
    public readonly Dictionary<RewardCurrencyType, bool> CurrencyVisibility = new Dictionary<RewardCurrencyType, bool>();

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
    private const float HP_AXIS_MIN_SCALE = 0.001f;
    private const float HP_AXIS_MAX_SCALE = 20.0f;
    private const float HP_AXIS_ZOOM_STEP = 1.12f;

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

    // 그래프 탭 전체 UI를 그린다
    public static void Draw(TurretBalanceReportResult report, TurretBalanceReportGraphState state, float targetClearSeconds)
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
        DrawSeriesToggles(state, currencyTypes);

        Rect graphRect = GUILayoutUtility.GetRect(0.0f, GRAPH_MIN_HEIGHT, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        DrawGraph(report, state, currencyTypes, graphRect, Mathf.Max(1.0f, targetClearSeconds));
    }

    // 리포트에 등장한 누적 재화 종류 목록을 정렬해서 만든다
    private static List<RewardCurrencyType> BuildCurrencyTypeList(TurretBalanceReportResult report)
    {
        List<RewardCurrencyType> currencyTypes = new List<RewardCurrencyType>(4);
        for (int i = 0; i < report.WaveRows.Count; i++)
        {
            Dictionary<RewardCurrencyType, float> rewards = report.WaveRows[i].CumulativeReward;
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
    private static void DrawSeriesToggles(TurretBalanceReportGraphState state, List<RewardCurrencyType> currencyTypes)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("표시 조건", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        state.ShowTotalWaveHp = EditorGUILayout.ToggleLeft("좀비 총 HP", state.ShowTotalWaveHp, GUILayout.Width(120.0f));
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

        EditorGUILayout.EndVertical();
    }

    // 그래프 영역 안에 배경, 축, 선, 툴팁을 그린다
    private static void DrawGraph(TurretBalanceReportResult report, TurretBalanceReportGraphState state, List<RewardCurrencyType> currencyTypes, Rect graphRect, float targetClearSeconds)
    {
        if (graphRect.width <= GRAPH_LEFT_PADDING + GRAPH_RIGHT_PADDING || graphRect.height <= GRAPH_TOP_PADDING + GRAPH_BOTTOM_PADDING)
        {
            return;
        }

        List<GraphSeries> seriesList = BuildSeriesList(report, state, currencyTypes, targetClearSeconds);
        GUI.Box(graphRect, GUIContent.none, EditorStyles.helpBox);

        Rect plotRect = new Rect(
            graphRect.x + GRAPH_LEFT_PADDING,
            graphRect.y + GRAPH_TOP_PADDING,
            graphRect.width - GRAPH_LEFT_PADDING - GRAPH_RIGHT_PADDING,
            graphRect.height - GRAPH_TOP_PADDING - GRAPH_BOTTOM_PADDING);

        HandleHpAxisWheelZoom(plotRect, state);
        DrawGrid(plotRect, report);
        if (state.ShowClearTimeRatio)
        {
            DrawRatioBaseline(plotRect, CalculateClearTimeRatioAxisMax(report, targetClearSeconds));
        }

        DrawSeriesList(plotRect, seriesList, report.WaveRows.Count, out GraphHoverInfo hoverInfo);
        DrawLegend(graphRect, seriesList);
        DrawHoverTooltip(graphRect, report, hoverInfo);
    }

    // 현재 표시 상태에 맞는 그래프 선 목록을 만든다
    private static List<GraphSeries> BuildSeriesList(TurretBalanceReportResult report, TurretBalanceReportGraphState state, List<RewardCurrencyType> currencyTypes, float targetClearSeconds)
    {
        List<GraphSeries> seriesList = new List<GraphSeries>(8);
        int colorIndex = 0;
        float hpAxisMax = CalculateHpAxisMax(report, targetClearSeconds) * Mathf.Clamp(state.HpAxisScaleMultiplier, HP_AXIS_MIN_SCALE, HP_AXIS_MAX_SCALE);
        if (state.ShowTotalWaveHp)
        {
            GraphSeries series = CreateSeries("좀비 총 HP", "HP", SeriesColors[colorIndex++ % SeriesColors.Length], report.WaveRows.Count);
            SetFixedScale(series, 0.0f, hpAxisMax);
            for (int i = 0; i < report.WaveRows.Count; i++)
            {
                series.Values.Add(Mathf.Max(0.0f, report.WaveRows[i].TotalWaveHp));
                series.PointNotes.Add(string.Empty);
            }

            seriesList.Add(series);
        }

        if (state.ShowCumulativeCurrency)
        {
            AddCurrencySeries(report, state, currencyTypes, seriesList, ref colorIndex);
        }

        if (state.ShowTopRankDps)
        {
            seriesList.Add(CreateTopRankDpsSeries(report, SeriesColors[colorIndex++ % SeriesColors.Length], hpAxisMax, targetClearSeconds));
        }

        if (state.ShowAllTurretDps)
        {
            AddAllTurretDpsSeries(report, seriesList, ref colorIndex, hpAxisMax, targetClearSeconds);
        }

        if (state.ShowClearTimeRatio)
        {
            GraphSeries series = CreateSeries("클리어 시간 / 기준 시간", "배", SeriesColors[colorIndex++ % SeriesColors.Length], report.WaveRows.Count);
            SetFixedScale(series, 0.0f, CalculateClearTimeRatioAxisMax(report, targetClearSeconds));
            for (int i = 0; i < report.WaveRows.Count; i++)
            {
                float clearSeconds = i < report.WaveClearRows.Count ? Mathf.Max(0.0f, report.WaveClearRows[i].BestClearSeconds) : 0.0f;
                float value = targetClearSeconds <= 0.0f ? 0.0f : clearSeconds / targetClearSeconds;
                series.Values.Add(value);
                series.PointNotes.Add($"{FormatFloat(clearSeconds)}초 / 기준 {FormatFloat(targetClearSeconds)}초");
            }

            seriesList.Add(series);
        }

        return seriesList;
    }

    // 클리어 시간 배율 축 최대값을 계산한다
    private static float CalculateClearTimeRatioAxisMax(TurretBalanceReportResult report, float targetClearSeconds)
    {
        float maxValue = 1.0f;
        if (targetClearSeconds <= 0.0f)
        {
            return maxValue;
        }

        for (int i = 0; i < report.WaveClearRows.Count; i++)
        {
            maxValue = Mathf.Max(maxValue, report.WaveClearRows[i].BestClearSeconds / targetClearSeconds);
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
    private static float CalculateHpAxisMax(TurretBalanceReportResult report, float targetClearSeconds)
    {
        float maxValue = 0.0f;
        for (int i = 0; i < report.WaveRows.Count; i++)
        {
            maxValue = Mathf.Max(maxValue, report.WaveRows[i].TotalWaveHp);
        }

        for (int i = 0; i < report.WaveClearRows.Count; i++)
        {
            List<WaveClearRankEntry> topRanks = report.WaveClearRows[i].TopRanks;
            if (topRanks != null && topRanks.Count > 0)
            {
                maxValue = Mathf.Max(maxValue, topRanks[0].TotalDps * targetClearSeconds);
            }
        }

        return Mathf.Max(1.0f, maxValue);
    }

    // 모든 터렛 종류의 웨이브별 최적 총 DPS 그래프 선을 추가한다
    private static void AddAllTurretDpsSeries(TurretBalanceReportResult report, List<GraphSeries> seriesList, ref int colorIndex, float hpAxisMax, float targetClearSeconds)
    {
        List<string> turretNames = BuildTurretNameList(report);
        for (int turretIndex = 0; turretIndex < turretNames.Count; turretIndex++)
        {
            string turretName = turretNames[turretIndex];
            GraphSeries series = CreateSeries("처리 가능 HP - " + turretName, "HP", SeriesColors[colorIndex++ % SeriesColors.Length], report.WaveRows.Count);
            SetFixedScale(series, 0.0f, hpAxisMax);
            for (int waveIndex = 0; waveIndex < report.WaveRows.Count; waveIndex++)
            {
                if (TryFindSpeciesEntry(report, waveIndex, turretName, out WaveClearRankEntry entry))
                {
                    float processableHp = Mathf.Max(0.0f, entry.TotalDps * targetClearSeconds);
                    series.Values.Add(processableHp);
                    series.PointNotes.Add($"{FormatFloat(entry.TotalDps)} DPS / {entry.InstallCount}대 / Lv{entry.Level}");
                    continue;
                }

                series.Values.Add(0.0f);
                series.PointNotes.Add("설치 불가");
            }

            seriesList.Add(series);
        }
    }

    // 리포트 상세 행에서 터렛 이름 목록을 만든다
    private static List<string> BuildTurretNameList(TurretBalanceReportResult report)
    {
        List<string> turretNames = new List<string>(report.SpeciesDetailRows.Count);
        for (int i = 0; i < report.SpeciesDetailRows.Count; i++)
        {
            string turretName = report.SpeciesDetailRows[i].TurretName;
            if (!string.IsNullOrEmpty(turretName) && !turretNames.Contains(turretName))
            {
                turretNames.Add(turretName);
            }
        }

        return turretNames;
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
                Dictionary<RewardCurrencyType, float> rewards = report.WaveRows[i].CumulativeReward;
                float value = rewards != null && rewards.TryGetValue(currencyType, out float amount) ? Mathf.Max(0.0f, amount) : 0.0f;
                series.Values.Add(value);
                series.PointNotes.Add(string.Empty);
            }

            seriesList.Add(series);
        }
    }

    // 1순위 터렛 처리 가능 HP 그래프 선을 만든다
    private static GraphSeries CreateTopRankDpsSeries(TurretBalanceReportResult report, Color color, float hpAxisMax, float targetClearSeconds)
    {
        GraphSeries series = CreateSeries("1순위 처리 가능 HP", "HP", color, report.WaveRows.Count);
        SetFixedScale(series, 0.0f, hpAxisMax);
        for (int i = 0; i < report.WaveRows.Count; i++)
        {
            if (i < report.WaveClearRows.Count && report.WaveClearRows[i].TopRanks != null && report.WaveClearRows[i].TopRanks.Count > 0)
            {
                WaveClearRankEntry entry = report.WaveClearRows[i].TopRanks[0];
                float processableHp = Mathf.Max(0.0f, entry.TotalDps * targetClearSeconds);
                series.Values.Add(processableHp);
                series.PointNotes.Add($"{entry.TurretName} / {FormatFloat(entry.TotalDps)} DPS / {entry.InstallCount}대 / Lv{entry.Level}");
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

    // 클리어 시간 배율의 기준선인 1.0x 라인을 그린다
    private static void DrawRatioBaseline(Rect plotRect, float ratioAxisMax)
    {
        float yRatio = Mathf.Approximately(ratioAxisMax, 0.0f) ? 1.0f : Mathf.Clamp01(1.0f / ratioAxisMax);
        float y = Mathf.Lerp(plotRect.yMax, plotRect.yMin, yRatio);
        Handles.BeginGUI();
        Handles.color = new Color(1.0f, 1.0f, 1.0f, 0.45f);
        Handles.DrawLine(new Vector3(plotRect.xMin, y), new Vector3(plotRect.xMax, y));
        Handles.EndGUI();
        GUI.Label(new Rect(plotRect.xMin + 4.0f, y - 18.0f, 110.0f, 18.0f), "기준 1.0x", EditorStyles.miniLabel);
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
        for (int i = 0; i < series.Values.Count; i++)
        {
            points[i] = CalculatePoint(plotRect, i, waveCount, series.Values[i], minValue, maxValue);
        }

        Handles.color = series.Color;
        Handles.DrawAAPolyLine(2.4f, points);
        for (int i = 0; i < points.Length; i++)
        {
            Rect pointRect = new Rect(points[i].x - 2.0f, points[i].y - 2.0f, 4.0f, 4.0f);
            EditorGUI.DrawRect(pointRect, series.Color);
        }

        UpdateHoverInfo(series, points, mousePosition, ref hoverInfo);
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
    private static void UpdateHoverInfo(GraphSeries series, Vector3[] points, Vector2 mousePosition, ref GraphHoverInfo hoverInfo)
    {
        if (points.Length <= 0)
        {
            return;
        }

        if (points.Length == 1)
        {
            UpdatePointHoverInfo(series, 0, Vector2.Distance(mousePosition, points[0]), mousePosition, ref hoverInfo);
            return;
        }

        for (int i = 0; i < points.Length - 1; i++)
        {
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
            case RewardCurrencyType.FirePart:
                return "파이어 파츠";
            case RewardCurrencyType.SpecialPart:
                return "스페셜 파츠";
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
