using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 터렛 웨이브 밸런스 시뮬레이션 리포트 창의 생명주기와 사용자 입력을 관리한다.
internal sealed class TurretBalanceReportWindow : EditorWindow
{
    private const string MENU_PATH = "Tools/밸런스 리포트";
    private const double AUTO_REFRESH_INTERVAL_SECONDS = 2.0d;
    private const int GRAPH_TAB_INDEX = 0;
    private const string EDITOR_PREFS_PREFIX = "ProjectZDefense.TurretBalanceReport.";
    private static readonly string[] TabLabels = { "그래프", "장애물 밸런스", "터렛 밸런스", "터렛 밸런스 상세", "데이터 경고" };

    private readonly TurretBalanceReportInputCollector inputCollector = new TurretBalanceReportInputCollector();
    private readonly TurretBalanceReportCalculator calculator = new TurretBalanceReportCalculator();
    private readonly TurretBalanceReportTableBuilder tableBuilder = new TurretBalanceReportTableBuilder();
    private readonly TurretBalanceReportGraphState graphState = new TurretBalanceReportGraphState();

    private ReportTableModel[] lastTables =
    {
        new ReportTableModel(),
        new ReportTableModel(),
        new ReportTableModel(),
        new ReportTableModel()
    };
    private TurretBalanceReportResult lastReport;
    private string lastDataSignature;
    private Vector2 scrollPosition;
    private int selectedTab;
    private double nextAutoRefreshTime;
    private string lastRefreshLabel;
    private float targetClearSeconds = 30.0f;
    private float targetClearSecondsIncrement = 0.0f;
    private List<ObstacleWaveRow> lastObstacleRows = new List<ObstacleWaveRow>();
    private TurretBalanceDpsSettings dpsSettings = CreateDefaultDpsSettings();

    // 터렛 웨이브 밸런스 시뮬레이션 창을 연다
    [MenuItem(MENU_PATH)]
    private static void OpenWindow()
    {
        TurretBalanceReportWindow window = GetWindow<TurretBalanceReportWindow>("밸런스 리포트");
        window.minSize = new Vector2(1240.0f, 560.0f);
        window.RefreshReport();
        window.Show();
    }

    // 열려 있는 터렛 웨이브 밸런스 시뮬레이션 창을 모두 새로고침한다
    internal static void RefreshOpenWindows()
    {
        TurretBalanceReportWindow[] windows = Resources.FindObjectsOfTypeAll<TurretBalanceReportWindow>();
        for (int i = 0; i < windows.Length; i++)
        {
            if (windows[i] != null)
            {
                windows[i].RefreshReport();
            }
        }
    }

    // 창이 활성화될 때 자동 갱신 루프를 연결하고 데이터를 계산한다
    private void OnEnable()
    {
        LoadEditorPrefs();
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        RefreshReport(true);
    }

    // 창이 비활성화될 때 자동 갱신 루프를 해제한다
    private void OnDisable()
    {
        SaveEditorPrefs();
        EditorApplication.update -= OnEditorUpdate;
    }

    // 창이 포커스를 얻을 때 최신 에셋 값으로 다시 계산한다
    private void OnFocus()
    {
        RefreshReportIfDataChanged();
    }

    // 프로젝트 에셋 변경 시 최신 SO 값을 다시 계산한다
    private void OnProjectChange()
    {
        RefreshReport(true);
    }

    // 지정 간격으로 리포트 원천 데이터 변경을 감지한다
    private void OnEditorUpdate()
    {
        if (EditorApplication.timeSinceStartup < nextAutoRefreshTime)
        {
            return;
        }

        nextAutoRefreshTime = EditorApplication.timeSinceStartup + AUTO_REFRESH_INTERVAL_SECONDS;
        RefreshReportIfDataChanged();
    }

    // 에디터 창의 IMGUI를 그린다
    private void OnGUI()
    {
        DrawToolbar();
        DrawDpsSettingsToolbar();
        EditorGUILayout.Space(6.0f);

        int nextSelectedTab = GUILayout.Toolbar(selectedTab, TabLabels);
        if (nextSelectedTab != selectedTab)
        {
            selectedTab = nextSelectedTab;
            scrollPosition = Vector2.zero;
        }

        EditorGUILayout.Space(6.0f);
        if (selectedTab == GRAPH_TAB_INDEX)
        {
            TurretBalanceReportGraphRenderer.Draw(lastReport, graphState, targetClearSeconds, targetClearSecondsIncrement, lastObstacleRows);
            return;
        }

        scrollPosition = TurretBalanceReportTableRenderer.Draw(GetReportTable(selectedTab), scrollPosition);
    }

    // 선택한 탭의 캐시된 표 데이터를 반환한다. 탭 0은 그래프(배열 없음), 탭 1~4 → lastTables[0~3].
    private ReportTableModel GetReportTable(int tabIndex)
    {
        int tableIndex = tabIndex - 1;
        if (tableIndex < 0 || tableIndex >= lastTables.Length)
        {
            return lastTables[0];
        }

        return lastTables[tableIndex];
    }

    // 상단 도구 버튼과 갱신 상태를 그린다
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(80.0f)))
        {
            RefreshReport(true);
        }

        if (GUILayout.Button("CSV 내보내기", EditorStyles.toolbarButton, GUILayout.Width(100.0f)))
        {
            TurretBalanceReportCsvExporter.Export(lastTables);
        }

        EditorGUILayout.LabelField("자동 갱신: 켜짐", EditorStyles.miniLabel, GUILayout.Width(100.0f));
        EditorGUILayout.LabelField("기준 클리어", EditorStyles.miniLabel, GUILayout.Width(58.0f));
        EditorGUI.BeginChangeCheck();
        float nextTargetClearSeconds = Mathf.Max(1.0f, EditorGUILayout.FloatField(targetClearSeconds, GUILayout.Width(46.0f)));
        EditorGUILayout.LabelField("초 +웨이브당", EditorStyles.miniLabel, GUILayout.Width(70.0f));
        float nextIncrement = Mathf.Max(0.0f, EditorGUILayout.FloatField(targetClearSecondsIncrement, GUILayout.Width(40.0f)));
        EditorGUILayout.LabelField("초", EditorStyles.miniLabel, GUILayout.Width(18.0f));
        if (EditorGUI.EndChangeCheck())
        {
            targetClearSeconds = nextTargetClearSeconds;
            targetClearSecondsIncrement = nextIncrement;
            SaveEditorPrefs();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField(lastRefreshLabel ?? "새로고침 전", EditorStyles.miniLabel, GUILayout.Width(280.0f));
        EditorGUILayout.EndHorizontal();
    }

    // 다수 대상 DPS 계산에 쓰는 기대 대상 수와 관통 효율 입력을 그린다
    private void DrawDpsSettingsToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("DPS 기대 대상", EditorStyles.miniLabel, GUILayout.Width(78.0f));

        EditorGUI.BeginChangeCheck();
        dpsSettings.FrostExpectedTargetCount = DrawPositiveFloatToolbarField("Frost", dpsSettings.FrostExpectedTargetCount, 38.0f);
        dpsSettings.PoisonExpectedTargetCount = DrawPositiveFloatToolbarField("Poison", dpsSettings.PoisonExpectedTargetCount, 42.0f);
        dpsSettings.ElectroExpectedTargetCount = DrawPositiveFloatToolbarField("Electro", dpsSettings.ElectroExpectedTargetCount, 44.0f);
        dpsSettings.IgnitionExpectedTargetCount = DrawPositiveFloatToolbarField("Ignition", dpsSettings.IgnitionExpectedTargetCount, 50.0f);
        EditorGUILayout.LabelField("Overload", EditorStyles.miniLabel, GUILayout.Width(52.0f));
        dpsSettings.ElectroOverloadTriggerExpectation = Mathf.Clamp01(EditorGUILayout.FloatField(dpsSettings.ElectroOverloadTriggerExpectation, GUILayout.Width(42.0f)));
        EditorGUILayout.LabelField("관통 DPS/개", EditorStyles.miniLabel, GUILayout.Width(72.0f));
        dpsSettings.PierceDpsMultiplierPerCount = Mathf.Max(0.0f, EditorGUILayout.FloatField(dpsSettings.PierceDpsMultiplierPerCount, GUILayout.Width(46.0f)));
        if (EditorGUI.EndChangeCheck())
        {
            SaveEditorPrefs();
            RefreshReport(true);
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    // 툴바에 양수 실수 입력 필드를 그린다
    private static float DrawPositiveFloatToolbarField(string label, float value, float labelWidth)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(labelWidth));
        return Mathf.Max(1.0f, EditorGUILayout.FloatField(value, GUILayout.Width(42.0f)));
    }

    // 모든 리포트 데이터를 다시 계산한다
    private void RefreshReport(bool force = false)
    {
        string dataSignature = inputCollector.BuildDataSignature();
        if (!force && dataSignature == lastDataSignature)
        {
            return;
        }

        lastDataSignature = dataSignature;
        TurretBalanceInputSnapshot snapshot = inputCollector.Collect();
        TurretBalanceReportResult report = calculator.Build(snapshot, dpsSettings);
        lastReport = report;
        ReportTableModel[] turretTables = tableBuilder.Build(report, targetClearSeconds, targetClearSecondsIncrement);
        List<ObstacleEntrySpec> obstacleEntries = ObstacleBalanceCalculator.CollectEntries(report.Warnings);
        List<ObstacleWaveRow> obstacleRows = ObstacleBalanceCalculator.BuildRows(report.WaveRows, obstacleEntries, report.WaveClearRows);
        lastObstacleRows = obstacleRows;
        lastTables = new ReportTableModel[]
        {
            ObstacleBalanceTableBuilder.Build(obstacleRows, obstacleEntries),  // tab 1: 장애물 밸런스
            turretTables[0],  // tab 2: 웨이브 클리어
            turretTables[1],  // tab 3: 터렛 상세
            turretTables[2],  // tab 4: 데이터 경고
        };

        lastRefreshLabel = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        Repaint();
    }

    // 리포트 원천 데이터가 바뀐 경우에만 다시 계산한다
    private void RefreshReportIfDataChanged()
    {
        RefreshReport(false);
    }

    // 저장된 에디터 입력값을 불러온다
    private void LoadEditorPrefs()
    {
        targetClearSeconds = EditorPrefs.GetFloat(EDITOR_PREFS_PREFIX + "TargetClearSeconds", 30.0f);
        targetClearSecondsIncrement = EditorPrefs.GetFloat(EDITOR_PREFS_PREFIX + "TargetClearSecondsIncrement", 0.0f);
        dpsSettings = CreateDefaultDpsSettings();
        dpsSettings.FrostExpectedTargetCount = EditorPrefs.GetFloat(EDITOR_PREFS_PREFIX + "FrostExpectedTargetCount", dpsSettings.FrostExpectedTargetCount);
        dpsSettings.PoisonExpectedTargetCount = EditorPrefs.GetFloat(EDITOR_PREFS_PREFIX + "PoisonExpectedTargetCount", dpsSettings.PoisonExpectedTargetCount);
        dpsSettings.ElectroExpectedTargetCount = EditorPrefs.GetFloat(EDITOR_PREFS_PREFIX + "ElectroExpectedTargetCount", dpsSettings.ElectroExpectedTargetCount);
        dpsSettings.IgnitionExpectedTargetCount = EditorPrefs.GetFloat(EDITOR_PREFS_PREFIX + "IgnitionExpectedTargetCount", dpsSettings.IgnitionExpectedTargetCount);
        dpsSettings.ElectroOverloadTriggerExpectation = EditorPrefs.GetFloat(EDITOR_PREFS_PREFIX + "ElectroOverloadTriggerExpectation", dpsSettings.ElectroOverloadTriggerExpectation);
        dpsSettings.PierceDpsMultiplierPerCount = EditorPrefs.GetFloat(EDITOR_PREFS_PREFIX + "PierceDpsMultiplierPerCount", dpsSettings.PierceDpsMultiplierPerCount);
        SanitizeInputValues();
    }

    // 에디터 입력값을 저장한다
    private void SaveEditorPrefs()
    {
        SanitizeInputValues();
        EditorPrefs.SetFloat(EDITOR_PREFS_PREFIX + "TargetClearSeconds", targetClearSeconds);
        EditorPrefs.SetFloat(EDITOR_PREFS_PREFIX + "TargetClearSecondsIncrement", targetClearSecondsIncrement);
        EditorPrefs.SetFloat(EDITOR_PREFS_PREFIX + "FrostExpectedTargetCount", dpsSettings.FrostExpectedTargetCount);
        EditorPrefs.SetFloat(EDITOR_PREFS_PREFIX + "PoisonExpectedTargetCount", dpsSettings.PoisonExpectedTargetCount);
        EditorPrefs.SetFloat(EDITOR_PREFS_PREFIX + "ElectroExpectedTargetCount", dpsSettings.ElectroExpectedTargetCount);
        EditorPrefs.SetFloat(EDITOR_PREFS_PREFIX + "IgnitionExpectedTargetCount", dpsSettings.IgnitionExpectedTargetCount);
        EditorPrefs.SetFloat(EDITOR_PREFS_PREFIX + "ElectroOverloadTriggerExpectation", dpsSettings.ElectroOverloadTriggerExpectation);
        EditorPrefs.SetFloat(EDITOR_PREFS_PREFIX + "PierceDpsMultiplierPerCount", dpsSettings.PierceDpsMultiplierPerCount);
    }

    // 입력값을 계산 가능한 범위로 보정한다
    private void SanitizeInputValues()
    {
        targetClearSeconds = Mathf.Max(1.0f, targetClearSeconds);
        targetClearSecondsIncrement = Mathf.Max(0.0f, targetClearSecondsIncrement);
        dpsSettings.FrostExpectedTargetCount = Mathf.Max(1.0f, dpsSettings.FrostExpectedTargetCount);
        dpsSettings.PoisonExpectedTargetCount = Mathf.Max(1.0f, dpsSettings.PoisonExpectedTargetCount);
        dpsSettings.ElectroExpectedTargetCount = Mathf.Max(1.0f, dpsSettings.ElectroExpectedTargetCount);
        dpsSettings.IgnitionExpectedTargetCount = Mathf.Max(1.0f, dpsSettings.IgnitionExpectedTargetCount);
        dpsSettings.ElectroOverloadTriggerExpectation = Mathf.Clamp01(dpsSettings.ElectroOverloadTriggerExpectation);
        dpsSettings.PierceDpsMultiplierPerCount = Mathf.Max(0.0f, dpsSettings.PierceDpsMultiplierPerCount);
    }

    // 리포트 DPS 계산 설정의 기본값을 만든다
    private static TurretBalanceDpsSettings CreateDefaultDpsSettings()
    {
        return new TurretBalanceDpsSettings
        {
            FrostExpectedTargetCount = 1.0f,
            PoisonExpectedTargetCount = 1.0f,
            ElectroExpectedTargetCount = 5.0f,
            IgnitionExpectedTargetCount = 3.0f,
            ElectroOverloadTriggerExpectation = 0.0f,
            PierceDpsMultiplierPerCount = 1.0f
        };
    }
}
