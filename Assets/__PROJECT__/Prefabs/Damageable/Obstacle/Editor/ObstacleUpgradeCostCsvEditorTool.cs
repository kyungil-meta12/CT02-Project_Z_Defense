#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 장애물과 게이트 업그레이드 비용 프로필을 CSV로 관리한다.
/// </summary>
public class ObstacleUpgradeCostCsvEditorTool : EditorWindow
{
    private const string CSV_PATH = "Assets/__PROJECT__/Prefabs/Damageable/Obstacle/SO/ObstacleUpgradeCosts.csv";
    private const string BASE_COSTS_PROPERTY = "baseCostsPerLevel";
    private const string ADDITIONAL_COST_PERCENT_PROPERTY = "additionalCostPercentPerLevel";

    private Vector2 scrollPosition;
    private readonly List<string> lastMessages = new List<string>(32);

    [MenuItem("Tools/장애물 업그레이드 비용 CSV 관리 도구")]
    // 장애물 업그레이드 비용 CSV 관리 창을 연다
    public static void ShowWindow()
    {
        GetWindow<ObstacleUpgradeCostCsvEditorTool>("장애물 비용 CSV");
    }

    // 에디터 창 UI를 그린다
    private void OnGUI()
    {
        EditorGUILayout.LabelField("장애물 업그레이드 비용 CSV 관리 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("CSV 파일 경로", CSV_PATH);
        EditorGUILayout.HelpBox("CSV 컬럼은 컬럼명(한글 설명) 형태로 출력됩니다. BaseCosts는 Coin:100|FirePart:2 형식으로 입력합니다. 임포트는 UpgradeCostProfilePath의 비용 프로필만 수정하고 Definition 표시 정보는 참고용으로 둡니다.", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("CSV로 익스포트", GUILayout.Height(34)))
        {
            ExecuteSafely(ExportToCsv);
        }

        if (GUILayout.Button("CSV에서 임포트", GUILayout.Height(34)))
        {
            ExecuteSafely(ImportFromCsv);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("CSV 열기", GUILayout.Height(34)))
        {
            ExecuteSafely(OpenCsv);
        }
        EditorGUILayout.EndHorizontal();

        DrawMessages();
    }

    // 최근 실행 결과 메시지를 UI에 표시한다
    private void DrawMessages()
    {
        if (lastMessages.Count == 0)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("실행 결과", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(120));
        for (int i = 0; i < lastMessages.Count; i++)
        {
            EditorGUILayout.LabelField(lastMessages[i], EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    // 장애물 업그레이드 비용 데이터를 CSV로 내보낸다
    private void ExportToCsv()
    {
        ClearRunState();
        List<ObstacleDefinitionSO> definitions = LoadObstacleDefinitions();
        StringBuilder builder = new StringBuilder(2048);
        AppendHeader(builder);
        for (int i = 0; i < definitions.Count; i++)
        {
            AppendDefinitionRow(builder, definitions[i]);
        }

        WriteUtf8Csv(CSV_PATH, builder.ToString());
        AddMessage($"장애물 업그레이드 비용 CSV 익스포트 완료: {definitions.Count}개 Definition");
        FlushMessagesToConsole(true);
    }

    // CSV를 읽어 장애물 업그레이드 비용 프로필에 반영한다
    private void ImportFromCsv()
    {
        ClearRunState();
        if (!TryReadCsv(CSV_PATH, out List<List<string>> table))
        {
            FlushMessagesToConsole(false);
            return;
        }

        if (!TryFindHeaderMap(table, out int headerRowIndex, out Dictionary<string, int> headerMap))
        {
            FlushMessagesToConsole(false);
            return;
        }

        int updatedCount = 0;
        for (int i = headerRowIndex + 1; i < table.Count; i++)
        {
            List<string> row = table[i];
            if (IsEmptyCsvRow(row))
            {
                continue;
            }

            if (ApplyRow(row, headerMap, i + 1))
            {
                updatedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AddMessage($"장애물 업그레이드 비용 CSV 임포트 완료: {updatedCount}개 프로필 갱신");
        FlushMessagesToConsole(true);
    }

    // 장애물 업그레이드 비용 CSV 파일을 기본 앱으로 연다
    private void OpenCsv()
    {
        ClearRunState();
        if (!File.Exists(CSV_PATH))
        {
            AddMessage("CSV 파일을 찾을 수 없습니다: " + CSV_PATH);
            FlushMessagesToConsole(false);
            return;
        }

        EditorUtility.OpenWithDefaultApp(CSV_PATH);
        AddMessage("CSV 파일 열기 요청: " + CSV_PATH);
        FlushMessagesToConsole(true);
    }

    // CSV 헤더를 추가한다
    private static void AppendHeader(StringBuilder builder)
    {
        AppendCsvLine(
            builder,
            "DefinitionPath(장애물 정의 SO 경로)",
            "ObstacleId(장애물 ID)",
            "DisplayName(표시 이름)",
            "SlotType(슬롯 타입)",
            "MaxLevel(최대 레벨)",
            "UpgradeCostProfilePath(업그레이드 비용 SO 경로)",
            "BaseCosts(기본 비용 목록)",
            "AdditionalCostPercentPerLevel(레벨당 추가 비용 비율)");
    }

    // 장애물 Definition 한 개를 CSV 행으로 추가한다
    private static void AppendDefinitionRow(StringBuilder builder, ObstacleDefinitionSO definition)
    {
        ObstacleUpgradeCostProfileSO profile = definition.UpgradeCostProfile;
        ReadCostProfile(profile, out ResourceCost[] baseCosts, out float additionalPercent);
        AppendCsvLine(
            builder,
            AssetDatabase.GetAssetPath(definition),
            definition.ObstacleId,
            definition.DisplayName,
            definition.SlotType,
            definition.MaxLevel,
            AssetDatabase.GetAssetPath(profile),
            FormatCosts(baseCosts),
            additionalPercent);
    }

    // CSV 행을 장애물 업그레이드 비용 프로필에 반영한다
    private bool ApplyRow(List<string> row, Dictionary<string, int> headerMap, int lineNumber)
    {
        string profilePath = ReadString(row, headerMap, "UpgradeCostProfilePath");
        ObstacleUpgradeCostProfileSO profile = AssetDatabase.LoadAssetAtPath<ObstacleUpgradeCostProfileSO>(profilePath);
        if (profile == null)
        {
            AddMessage($"{lineNumber}행: UpgradeCostProfile을 찾을 수 없어 건너뜁니다. 경로: {profilePath}");
            return false;
        }

        if (!TryParseCosts(ReadString(row, headerMap, "BaseCosts"), lineNumber, out List<ResourceCost> baseCosts))
        {
            return false;
        }

        float additionalPercent = ReadFloat(row, headerMap, "AdditionalCostPercentPerLevel", lineNumber, 0.0f);
        ApplyCostProfile(profile, baseCosts, additionalPercent);
        EditorUtility.SetDirty(profile);
        return true;
    }

    // 비용 프로필에 CSV 비용 값을 직렬화 필드로 반영한다
    private static void ApplyCostProfile(ObstacleUpgradeCostProfileSO profile, List<ResourceCost> baseCosts, float additionalPercent)
    {
        SerializedObject serializedObject = new SerializedObject(profile);
        SerializedProperty baseCostsProperty = serializedObject.FindProperty(BASE_COSTS_PROPERTY);
        SerializedProperty additionalPercentProperty = serializedObject.FindProperty(ADDITIONAL_COST_PERCENT_PROPERTY);
        baseCostsProperty.arraySize = baseCosts.Count;
        for (int i = 0; i < baseCosts.Count; i++)
        {
            SerializedProperty costProperty = baseCostsProperty.GetArrayElementAtIndex(i);
            costProperty.FindPropertyRelative("currencyType").enumValueIndex = GetRewardCurrencyIndex(baseCosts[i].currencyType);
            costProperty.FindPropertyRelative("amount").intValue = Mathf.Max(0, baseCosts[i].amount);
        }

        additionalPercentProperty.floatValue = Mathf.Max(0.0f, additionalPercent);
        serializedObject.ApplyModifiedProperties();
    }

    // 모든 장애물 Definition을 로드한다
    private static List<ObstacleDefinitionSO> LoadObstacleDefinitions()
    {
        string[] guids = AssetDatabase.FindAssets("t:ObstacleDefinitionSO");
        List<ObstacleDefinitionSO> definitions = new List<ObstacleDefinitionSO>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ObstacleDefinitionSO definition = AssetDatabase.LoadAssetAtPath<ObstacleDefinitionSO>(path);
            if (definition != null && IsManagedObstaclePath(path))
            {
                definitions.Add(definition);
            }
        }

        definitions.Sort(CompareObstacleDefinitions);
        return definitions;
    }

    // 프로젝트 장애물 SO 폴더 안의 Definition인지 확인한다
    private static bool IsManagedObstaclePath(string assetPath)
    {
        return assetPath.StartsWith("Assets/__PROJECT__/Prefabs/Damageable/Obstacle/SO/", StringComparison.OrdinalIgnoreCase);
    }

    // 장애물 Definition을 슬롯 타입과 경로 기준으로 정렬한다
    private static int CompareObstacleDefinitions(ObstacleDefinitionSO left, ObstacleDefinitionSO right)
    {
        int slotCompare = left.SlotType.CompareTo(right.SlotType);
        if (slotCompare != 0)
        {
            return slotCompare;
        }

        return string.CompareOrdinal(AssetDatabase.GetAssetPath(left), AssetDatabase.GetAssetPath(right));
    }

    // 비용 프로필의 기본 비용과 증가율을 읽는다
    private static void ReadCostProfile(ObstacleUpgradeCostProfileSO profile, out ResourceCost[] baseCosts, out float additionalPercent)
    {
        baseCosts = Array.Empty<ResourceCost>();
        additionalPercent = 0.0f;
        if (profile == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(profile);
        SerializedProperty baseCostsProperty = serializedObject.FindProperty(BASE_COSTS_PROPERTY);
        SerializedProperty additionalPercentProperty = serializedObject.FindProperty(ADDITIONAL_COST_PERCENT_PROPERTY);
        additionalPercent = additionalPercentProperty == null ? 0.0f : additionalPercentProperty.floatValue;
        if (baseCostsProperty == null || !baseCostsProperty.isArray || baseCostsProperty.arraySize <= 0)
        {
            return;
        }

        baseCosts = new ResourceCost[baseCostsProperty.arraySize];
        for (int i = 0; i < baseCostsProperty.arraySize; i++)
        {
            SerializedProperty costProperty = baseCostsProperty.GetArrayElementAtIndex(i);
            RewardCurrencyType currencyType = (RewardCurrencyType)costProperty.FindPropertyRelative("currencyType").enumValueIndex;
            int amount = costProperty.FindPropertyRelative("amount").intValue;
            baseCosts[i] = new ResourceCost(currencyType, amount);
        }
    }

    // 비용 목록을 CSV 셀 문자열로 변환한다
    private static string FormatCosts(ResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(64);
        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('|');
            }

            builder.Append(cost.currencyType);
            builder.Append(':');
            builder.Append(cost.amount.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    // CSV 비용 셀 문자열을 비용 목록으로 변환한다
    private bool TryParseCosts(string text, int lineNumber, out List<ResourceCost> costs)
    {
        costs = new List<ResourceCost>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        string[] entries = text.Split('|');
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i].Trim();
            if (string.IsNullOrEmpty(entry))
            {
                continue;
            }

            string[] parts = entry.Split(':');
            if (parts.Length != 2)
            {
                AddMessage($"{lineNumber}행: BaseCosts 형식이 유효하지 않습니다. 값: {entry}");
                return false;
            }

            if (!Enum.TryParse(parts[0].Trim(), out RewardCurrencyType currencyType))
            {
                AddMessage($"{lineNumber}행: BaseCosts 재화 타입이 유효하지 않습니다. 값: {parts[0]}");
                return false;
            }

            if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
            {
                AddMessage($"{lineNumber}행: BaseCosts 비용 수량이 유효하지 않습니다. 값: {parts[1]}");
                return false;
            }

            costs.Add(new ResourceCost(currencyType, Mathf.Max(0, amount)));
        }

        return true;
    }

    // CSV 파일을 읽고 테이블로 파싱한다
    private bool TryReadCsv(string path, out List<List<string>> table)
    {
        table = null;
        if (!File.Exists(path))
        {
            AddMessage("CSV 파일을 찾을 수 없습니다: " + path);
            return false;
        }

        string text = File.ReadAllText(path, Encoding.UTF8);
        char delimiter = DetectDelimiter(text);
        table = ParseCsv(text, delimiter);
        if (table.Count <= 0)
        {
            AddMessage("CSV가 비어 있습니다: " + path);
            return false;
        }

        return true;
    }

    // CSV 테이블에서 실제 헤더 행을 찾아 컬럼 인덱스 맵으로 변환한다
    private bool TryFindHeaderMap(List<List<string>> table, out int headerRowIndex, out Dictionary<string, int> headerMap)
    {
        headerRowIndex = -1;
        headerMap = null;
        for (int i = 0; i < table.Count; i++)
        {
            List<string> row = table[i];
            if (IsEmptyCsvRow(row) || IsExcelSeparatorRow(row))
            {
                continue;
            }

            if (TryBuildHeaderMap(row, out headerMap, false))
            {
                headerRowIndex = i;
                return true;
            }
        }

        AddMessage("필수 CSV 컬럼이 없습니다: UpgradeCostProfilePath");
        return false;
    }

    // CSV 헤더를 컬럼 인덱스 맵으로 변환한다
    private bool TryBuildHeaderMap(List<string> headers, out Dictionary<string, int> headerMap, bool reportMissing)
    {
        headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            string header = NormalizeHeaderName(headers[i]);
            if (!string.IsNullOrEmpty(header) && !headerMap.ContainsKey(header))
            {
                headerMap.Add(header, i);
            }
        }

        string[] requiredColumns = { "UpgradeCostProfilePath", "BaseCosts", "AdditionalCostPercentPerLevel" };
        for (int i = 0; i < requiredColumns.Length; i++)
        {
            if (!headerMap.ContainsKey(requiredColumns[i]))
            {
                if (reportMissing)
                {
                    AddMessage("필수 CSV 컬럼이 없습니다: " + requiredColumns[i]);
                }

                return false;
            }
        }

        return true;
    }

    // 설명이 붙은 CSV 헤더에서 실제 컬럼 키만 추출한다
    private static string NormalizeHeaderName(string header)
    {
        string normalizedHeader = (header ?? string.Empty).Trim('\uFEFF').Trim();
        int descriptionIndex = normalizedHeader.IndexOf('(');
        if (descriptionIndex < 0)
        {
            descriptionIndex = normalizedHeader.IndexOf('（');
        }

        return descriptionIndex >= 0 ? normalizedHeader.Substring(0, descriptionIndex).Trim() : normalizedHeader;
    }

    // 엑셀 구분자 안내 행인지 확인한다
    private static bool IsExcelSeparatorRow(List<string> row)
    {
        return row.Count == 1 && row[0] != null && row[0].Trim().StartsWith("sep=", StringComparison.OrdinalIgnoreCase);
    }

    // CSV 텍스트에서 가장 가능성이 높은 구분자를 감지한다
    private static char DetectDelimiter(string text)
    {
        string firstDataLine = GetFirstDataLine(text);
        int commaCount = CountDelimiter(firstDataLine, ',');
        int tabCount = CountDelimiter(firstDataLine, '\t');
        int semicolonCount = CountDelimiter(firstDataLine, ';');
        if (tabCount > commaCount && tabCount >= semicolonCount)
        {
            return '\t';
        }

        if (semicolonCount > commaCount)
        {
            return ';';
        }

        return ',';
    }

    // CSV 텍스트에서 비어 있지 않은 첫 데이터 줄을 반환한다
    private static string GetFirstDataLine(string text)
    {
        using (StringReader reader = new StringReader(text))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("sep=", StringComparison.OrdinalIgnoreCase))
                {
                    return line;
                }
            }
        }

        return string.Empty;
    }

    // 따옴표 밖의 구분자 개수를 센다
    private static int CountDelimiter(string line, char delimiter)
    {
        int count = 0;
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
            }
            else if (c == delimiter && !inQuotes)
            {
                count++;
            }
        }

        return count;
    }

    // CSV 텍스트를 행과 필드 목록으로 파싱한다
    private static List<List<string>> ParseCsv(string text, char delimiter)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> row = new List<string>();
        StringBuilder field = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                row.Add(field.ToString());
                field.Length = 0;
            }
            else if ((c == '\n' || c == '\r') && !inQuotes)
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(field.ToString());
                rows.Add(row);
                row = new List<string>();
                field.Length = 0;
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    // CSV 한 줄을 추가한다
    private static void AppendCsvLine(StringBuilder builder, params object[] columns)
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
    private static string EscapeCsv(object value)
    {
        string text = value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        bool mustQuote = text.Contains(",") || text.Contains("\"") || text.Contains("\n") || text.Contains("\r");
        return mustQuote ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }

    // CSV 파일을 UTF-8 BOM으로 저장한다
    private static void WriteUtf8Csv(string path, string text)
    {
        string directoryPath = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.WriteAllText(path, text, new UTF8Encoding(true));
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
    }

    // CSV 행이 비어 있는지 확인한다
    private static bool IsEmptyCsvRow(List<string> row)
    {
        for (int i = 0; i < row.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(row[i]))
            {
                return false;
            }
        }

        return true;
    }

    // CSV 행에서 문자열 값을 읽는다
    private static string ReadString(List<string> row, Dictionary<string, int> headerMap, string columnName)
    {
        if (!headerMap.TryGetValue(columnName, out int index))
        {
            return string.Empty;
        }

        return index >= 0 && index < row.Count ? row[index] ?? string.Empty : string.Empty;
    }

    // CSV 행에서 실수 값을 읽는다
    private float ReadFloat(List<string> row, Dictionary<string, int> headerMap, string columnName, int lineNumber, float fallback)
    {
        string value = ReadString(row, headerMap, columnName);
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
        {
            return result;
        }

        AddMessage($"{lineNumber}행: {columnName} 실수 값이 유효하지 않아 {fallback}을 사용합니다. 값: {value}");
        return fallback;
    }

    // 재화 enum 값을 직렬화 enum 인덱스로 변환한다
    private static int GetRewardCurrencyIndex(RewardCurrencyType currencyType)
    {
        string[] names = Enum.GetNames(typeof(RewardCurrencyType));
        string target = currencyType.ToString();
        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == target)
            {
                return i;
            }
        }

        return 0;
    }

    // 실행 상태 메시지를 초기화한다
    private void ClearRunState()
    {
        lastMessages.Clear();
    }

    // 실행 메시지를 추가한다
    private void AddMessage(string message)
    {
        lastMessages.Add(message);
    }

    // 실행 메시지를 콘솔에 출력한다
    private void FlushMessagesToConsole(bool isSuccess)
    {
        string joinedMessage = string.Join("\n", lastMessages);
        if (isSuccess)
        {
            Debug.Log("[ObstacleUpgradeCostCsvEditorTool]\n" + joinedMessage);
        }
        else
        {
            Debug.LogWarning("[ObstacleUpgradeCostCsvEditorTool]\n" + joinedMessage);
        }
    }

    // OnGUI 레이아웃이 깨지지 않도록 버튼 동작 예외를 처리한다
    private void ExecuteSafely(Action action)
    {
        try
        {
            action?.Invoke();
        }
        catch (ExitGUIException)
        {
            throw;
        }
        catch (Exception exception)
        {
            ClearRunState();
            AddMessage("처리 중 예외가 발생했습니다. 콘솔 로그를 확인하세요.");
            AddMessage(exception.Message);
            Debug.LogException(exception);
        }
    }
}
#endif
