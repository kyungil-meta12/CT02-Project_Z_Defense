using System;
using UnityEditor;
using UnityEngine;

// 터렛 웨이브 밸런스 시뮬레이션 리포트 창의 생명주기와 사용자 입력을 관리한다.
internal sealed class TurretBalanceReportWindow : EditorWindow
{
    private const string MENU_PATH = "Tools/터렛 웨이브 밸런스 시뮬레이션";
    private const double AUTO_REFRESH_INTERVAL_SECONDS = 2.0d;
    private static readonly string[] TabLabels = { "웨이브 클리어", "터렛 시나리오 상세", "원천 데이터 점검" };

    private readonly TurretBalanceReportInputCollector inputCollector = new TurretBalanceReportInputCollector();
    private readonly TurretBalanceReportCalculator calculator = new TurretBalanceReportCalculator();
    private readonly TurretBalanceReportTableBuilder tableBuilder = new TurretBalanceReportTableBuilder();

    private ReportTableModel[] lastTables =
    {
        new ReportTableModel(),
        new ReportTableModel(),
        new ReportTableModel()
    };
    private string lastDataSignature;
    private Vector2 scrollPosition;
    private int selectedTab;
    private double nextAutoRefreshTime;
    private string lastRefreshLabel;

    // 터렛 웨이브 밸런스 시뮬레이션 창을 연다
    [MenuItem(MENU_PATH)]
    private static void OpenWindow()
    {
        TurretBalanceReportWindow window = GetWindow<TurretBalanceReportWindow>("터렛 웨이브 밸런스");
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
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        RefreshReport(true);
    }

    // 창이 비활성화될 때 자동 갱신 루프를 해제한다
    private void OnDisable()
    {
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
        EditorGUILayout.Space(6.0f);

        int nextSelectedTab = GUILayout.Toolbar(selectedTab, TabLabels);
        if (nextSelectedTab != selectedTab)
        {
            selectedTab = nextSelectedTab;
            scrollPosition = Vector2.zero;
        }

        EditorGUILayout.Space(6.0f);
        scrollPosition = TurretBalanceReportTableRenderer.Draw(GetReportTable(selectedTab), scrollPosition);
    }

    // 선택한 탭의 캐시된 표 데이터를 반환한다
    private ReportTableModel GetReportTable(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= lastTables.Length)
        {
            return lastTables[0];
        }

        return lastTables[tabIndex];
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

        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField(lastRefreshLabel ?? "새로고침 전", EditorStyles.miniLabel, GUILayout.Width(280.0f));
        EditorGUILayout.EndHorizontal();
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
        TurretBalanceReportResult report = calculator.Build(snapshot);
        lastTables = tableBuilder.Build(report);

        lastRefreshLabel = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        Repaint();
    }

    // 리포트 원천 데이터가 바뀐 경우에만 다시 계산한다
    private void RefreshReportIfDataChanged()
    {
        RefreshReport(false);
    }
}
