using System;
using UnityEditor;
using UnityEngine;

// 터렛 웨이브 밸런스 리포트 표 모델의 IMGUI 렌더링을 담당한다.
internal static class TurretBalanceReportTableRenderer
{
    public const float RowHeight = 20.0f;

    private const float COLUMN_MIN_WIDTH = 72.0f;
    private const float COLUMN_MAX_WIDTH = 320.0f;
    private const float COLUMN_PADDING = 24.0f;

    // 헤더와 행 문자열을 기준으로 열 너비를 다시 계산한다
    public static void RecalculateColumnWidths(ReportTableModel table)
    {
        if (table.ColumnWidths.Length != table.Headers.Length)
        {
            table.ColumnWidths = new float[table.Headers.Length];
        }

        for (int i = 0; i < table.ColumnWidths.Length; i++)
        {
            table.ColumnWidths[i] = CalculateColumnWidth(i < table.Headers.Length ? table.Headers[i] : string.Empty, EditorStyles.boldLabel);
        }

        for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            string[] row = table.Rows[rowIndex];
            int columnCount = Mathf.Min(table.ColumnWidths.Length, row.Length);
            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                float width = CalculateColumnWidth(row[columnIndex], EditorStyles.label);
                if (width > table.ColumnWidths[columnIndex])
                {
                    table.ColumnWidths[columnIndex] = width;
                }
            }
        }
    }

    // 리포트 표 모델을 IMGUI로 그린다
    public static Vector2 Draw(ReportTableModel table, Vector2 scrollPosition, Rect windowPosition)
    {
        if (table == null)
        {
            return scrollPosition;
        }

        DrawInfoBox(table.InfoText);
        Rect headerRect = GUILayoutUtility.GetRect(0.0f, RowHeight + 4.0f, GUILayout.ExpandWidth(true));
        DrawHeader(table.Headers, table.ColumnWidths, headerRect, scrollPosition.x);

        float bodyHeight = Mathf.Max(RowHeight, windowPosition.height - headerRect.yMax - 8.0f);
        Rect bodyRect = GUILayoutUtility.GetRect(0.0f, bodyHeight, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        Rect contentRect = new Rect(0.0f, 0.0f, GetTableWidth(table.ColumnWidths), Mathf.Max(bodyRect.height, table.Rows.Count * RowHeight));
        Vector2 nextScrollPosition = GUI.BeginScrollView(bodyRect, scrollPosition, contentRect);
        DrawVisibleRows(table, bodyRect.height, nextScrollPosition.y);
        GUI.EndScrollView();
        return nextScrollPosition;
    }

    // 문자열 길이를 기준으로 표 열 너비를 계산한다
    private static float CalculateColumnWidth(string text, GUIStyle style)
    {
        string safeText = string.IsNullOrEmpty(text) ? "-" : text;
        float contentWidth = style.CalcSize(new GUIContent(safeText)).x + COLUMN_PADDING;
        return Mathf.Clamp(contentWidth, COLUMN_MIN_WIDTH, COLUMN_MAX_WIDTH);
    }

    // 안내 문구 박스를 그린다
    private static void DrawInfoBox(string message)
    {
        EditorGUILayout.HelpBox(message, MessageType.Info);
    }

    // 표 헤더 행을 캐시된 열 너비로 그린다
    private static void DrawHeader(string[] columns, float[] columnWidths, Rect viewRect, float horizontalScroll)
    {
        GUI.Box(viewRect, GUIContent.none, EditorStyles.helpBox);
        Rect cellRect = new Rect(viewRect.x - horizontalScroll, viewRect.y + 2.0f, 0.0f, RowHeight);
        for (int i = 0; i < columns.Length; i++)
        {
            cellRect.width = GetColumnWidth(columnWidths, i);
            GUI.Label(cellRect, columns[i], EditorStyles.boldLabel);
            cellRect.x += cellRect.width;
        }
    }

    // 스크롤 위치에 보이는 표 행만 그린다
    private static void DrawVisibleRows(ReportTableModel table, float viewportHeight, float verticalScroll)
    {
        if (table.Rows.Count <= 0)
        {
            return;
        }

        int firstRow = Mathf.Clamp(Mathf.FloorToInt(verticalScroll / RowHeight), 0, table.Rows.Count);
        int visibleRowCount = Mathf.CeilToInt(Mathf.Max(RowHeight, viewportHeight) / RowHeight) + 2;
        int lastRowExclusive = Mathf.Min(table.Rows.Count, firstRow + visibleRowCount);

        for (int i = firstRow; i < lastRowExclusive; i++)
        {
            DrawRow(table.Rows[i], table.ColumnWidths, i * RowHeight);
        }
    }

    // 표 데이터 행을 캐시된 열 너비로 그린다
    private static void DrawRow(string[] columns, float[] columnWidths, float y)
    {
        Rect cellRect = new Rect(0.0f, y, 0.0f, RowHeight);
        for (int i = 0; i < columns.Length; i++)
        {
            cellRect.width = GetColumnWidth(columnWidths, i);
            GUI.Label(cellRect, columns[i], EditorStyles.label);
            cellRect.x += cellRect.width;
        }
    }

    // 표 전체 너비를 계산한다
    private static float GetTableWidth(float[] columnWidths)
    {
        if (columnWidths == null || columnWidths.Length == 0)
        {
            return COLUMN_MIN_WIDTH;
        }

        float totalWidth = 0.0f;
        for (int i = 0; i < columnWidths.Length; i++)
        {
            totalWidth += GetColumnWidth(columnWidths, i);
        }

        return Mathf.Max(COLUMN_MIN_WIDTH, totalWidth);
    }

    // 캐시된 표 열 너비를 반환한다
    private static float GetColumnWidth(float[] columnWidths, int index)
    {
        return columnWidths != null && index >= 0 && index < columnWidths.Length ? columnWidths[index] : COLUMN_MIN_WIDTH;
    }
}
