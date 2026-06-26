using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 터렛 웨이브 밸런스 리포트의 CSV 내보내기를 담당한다.
internal static class TurretBalanceReportCsvExporter
{
    // 현재 화면 표 모델을 CSV 파일로 내보낸다
    public static void Export(ReportTableModel[] tables)
    {
        string folderPath = EditorUtility.SaveFolderPanel("터렛 웨이브 밸런스 CSV 내보내기", Application.dataPath, "TurretWaveBalanceReports");
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        for (int i = 0; i < tables.Length; i++)
        {
            ReportTableModel table = tables[i];
            if (table == null || string.IsNullOrWhiteSpace(table.FileName))
            {
                continue;
            }

            WriteCsv(Path.Combine(folderPath, table.FileName), BuildTableCsv(table));
        }

        Debug.Log($"[터렛 웨이브 밸런스 시뮬레이션] CSV 내보내기 완료: {folderPath}");
    }

    // 표 모델 하나를 CSV 문자열로 변환한다
    private static string BuildTableCsv(ReportTableModel table)
    {
        StringBuilder builder = new StringBuilder(4096);
        AppendCsvLine(builder, table.Headers);
        for (int i = 0; i < table.Rows.Count; i++)
        {
            AppendCsvLine(builder, table.Rows[i]);
        }

        return builder.ToString();
    }

    // CSV 파일을 UTF-8 BOM으로 저장한다
    private static void WriteCsv(string path, string contents)
    {
        File.WriteAllText(path, contents, new UTF8Encoding(true));
    }

    // CSV 한 줄을 추가한다
    private static void AppendCsvLine(StringBuilder builder, string[] columns)
    {
        for (int i = 0; i < columns.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(EscapeCsv(columns[i]));
        }

        builder.AppendLine();
    }

    // CSV 셀 값을 이스케이프한다
    private static string EscapeCsv(string value)
    {
        string text = value ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        bool mustQuote = text.Contains(",") || text.Contains("\"") || text.Contains("\n") || text.Contains("\r");
        return mustQuote ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }
}
