using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 터렛 웨이브 밸런스 리포트 표 모델의 IMGUI 렌더링을 담당한다.
internal static class TurretBalanceReportTableRenderer
{
    public const float RowHeight = 20.0f;

    private const float COLUMN_MIN_WIDTH = 72.0f;
    private const float COLUMN_MAX_WIDTH = 320.0f;
    private const float COLUMN_PADDING = 24.0f;

    private static GUIStyle wrappingHeaderStyle;

    // 헤더와 행 문자열을 기준으로 열 너비, 줄바꿈을 포함한 행 높이, 헤더 줄바꿈 높이를 다시 계산한다
    public static void RecalculateColumnWidths(ReportTableModel table)
    {
        if (table.ColumnWidths.Length != table.Headers.Length)
        {
            table.ColumnWidths = new float[table.Headers.Length];
        }

        for (int i = 0; i < table.ColumnWidths.Length; i++)
        {
            table.ColumnWidths[i] = COLUMN_MIN_WIDTH;
        }

        table.RowHeights.Clear();
        for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            string[] row = table.Rows[rowIndex];
            int columnCount = Mathf.Min(table.ColumnWidths.Length, row.Length);
            int maxLineCount = 1;
            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                float width = CalculateColumnWidth(row[columnIndex], EditorStyles.label);
                if (width > table.ColumnWidths[columnIndex])
                {
                    table.ColumnWidths[columnIndex] = width;
                }

                maxLineCount = Mathf.Max(maxLineCount, CountLines(row[columnIndex]));
            }

            table.RowHeights.Add(maxLineCount * RowHeight);
        }

        table.HeaderHeight = CalculateHeaderHeight(table.Headers, table.ColumnWidths);
    }

    // 열 너비 안에서 줄바꿈했을 때 헤더가 필요로 하는 최대 높이를 계산한다
    private static float CalculateHeaderHeight(string[] headers, float[] columnWidths)
    {
        GUIStyle style = GetWrappingHeaderStyle();
        float maxHeight = RowHeight;
        for (int i = 0; i < headers.Length; i++)
        {
            float width = Mathf.Max(1.0f, GetColumnWidth(columnWidths, i) - 8.0f);
            float height = style.CalcHeight(new GUIContent(headers[i]), width);
            maxHeight = Mathf.Max(maxHeight, height);
        }

        return maxHeight;
    }

    // 줄바꿈이 켜진 헤더 라벨 스타일을 가져온다
    private static GUIStyle GetWrappingHeaderStyle()
    {
        if (wrappingHeaderStyle == null)
        {
            wrappingHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
        }

        return wrappingHeaderStyle;
    }

    // 문자열의 줄바꿈(\n) 개수를 기준으로 줄 수를 센다
    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 1;
        }

        int lineCount = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lineCount++;
            }
        }

        return lineCount;
    }

    // 리포트 표 모델을 IMGUI로 그린다. 가로/세로 스크롤 모두 하나의 스크롤뷰가 자동으로 처리한다
    public static Vector2 Draw(ReportTableModel table, Vector2 scrollPosition)
    {
        if (table == null)
        {
            return scrollPosition;
        }

        DrawInfoBox(table.InfoText);
        Rect headerRect = GUILayoutUtility.GetRect(0.0f, table.HeaderHeight + 4.0f, GUILayout.ExpandWidth(true));
        DrawHeader(table.Headers, table.ColumnWidths, headerRect, scrollPosition.x);

        // ExpandHeight로 남은 공간을 모두 차지해, 창 크기(EditorWindow.position)에 의존하지 않고 항상 정확히 그려지도록 한다.
        Rect bodyRect = GUILayoutUtility.GetRect(0.0f, RowHeight, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        float tableWidth = GetTableWidth(table.ColumnWidths);
        float totalRowsHeight = GetTotalRowsHeight(table.RowHeights);
        Rect contentRect = new Rect(0.0f, 0.0f, tableWidth, Mathf.Max(bodyRect.height, totalRowsHeight));
        Vector2 nextScrollPosition = GUI.BeginScrollView(bodyRect, scrollPosition, contentRect);
        DrawVisibleRows(table, bodyRect.height, nextScrollPosition.y);
        GUI.EndScrollView();

        return nextScrollPosition;
    }

    // 모든 행 높이의 합을 계산한다
    private static float GetTotalRowsHeight(List<float> rowHeights)
    {
        float total = 0.0f;
        for (int i = 0; i < rowHeights.Count; i++)
        {
            total += rowHeights[i];
        }

        return total;
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

    // 표 헤더 행을 캐시된 열 너비로 줄바꿈해 그리고, 좌우 스크롤 위치를 따라가도록 잘라낸 영역 안에서 이동시킨다
    private static void DrawHeader(string[] columns, float[] columnWidths, Rect viewRect, float horizontalScroll)
    {
        GUI.Box(viewRect, GUIContent.none, EditorStyles.helpBox);
        GUI.BeginGroup(viewRect);
        GUIStyle style = GetWrappingHeaderStyle();
        Rect cellRect = new Rect(-horizontalScroll, 2.0f, 0.0f, viewRect.height - 4.0f);
        for (int i = 0; i < columns.Length; i++)
        {
            cellRect.width = GetColumnWidth(columnWidths, i);
            GUI.Label(cellRect, columns[i], style);
            cellRect.x += cellRect.width;
        }

        GUI.EndGroup();
    }

    // 스크롤 위치에 보이는 표 행만, 행마다 다른 높이를 누적해 그린다
    private static void DrawVisibleRows(ReportTableModel table, float viewportHeight, float verticalScroll)
    {
        if (table.Rows.Count <= 0)
        {
            return;
        }

        float viewportBottom = verticalScroll + Mathf.Max(RowHeight, viewportHeight);
        float y = 0.0f;
        for (int i = 0; i < table.Rows.Count; i++)
        {
            float rowHeight = i < table.RowHeights.Count ? table.RowHeights[i] : RowHeight;
            if (y + rowHeight >= verticalScroll)
            {
                DrawRow(table.Rows[i], table.ColumnWidths, y, rowHeight);
            }

            y += rowHeight;
            if (y > viewportBottom)
            {
                break;
            }
        }
    }

    // 표 데이터 행을 캐시된 열 너비와 행 높이로 그린다
    private static void DrawRow(string[] columns, float[] columnWidths, float y, float rowHeight)
    {
        Rect cellRect = new Rect(0.0f, y, 0.0f, rowHeight);
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
