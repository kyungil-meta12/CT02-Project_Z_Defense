#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 터렛 정의, 엔지니어 제한, 기본 스탯, 성장, 업그레이드 비용을 단일 CSV로 관리한다.
/// </summary>
public class TurretDataCsvEditorTool : EditorWindow
{
    private const string CSV_PATH = "Assets/__PROJECT__/Scenes/KKW/Turret_Scene/SO/TurretData.csv";
    private const string BASE_COSTS_PROPERTY = "baseCostsPerLevel";
    private const string ADDITIONAL_COST_PERCENT_PROPERTY = "additionalCostPercentPerTierLevel";

    private Vector2 scrollPosition;
    private readonly List<string> lastMessages = new List<string>(32);

    [MenuItem("Tools/터렛 데이터 CSV 관리 도구")]
    // 터렛 데이터 CSV 관리 창을 연다
    public static void ShowWindow()
    {
        GetWindow<TurretDataCsvEditorTool>("터렛 데이터 CSV");
    }

    // 에디터 창 UI를 그린다
    private void OnGUI()
    {
        EditorGUILayout.LabelField("터렛 데이터 CSV 관리 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("CSV 파일 경로", CSV_PATH);
        EditorGUILayout.HelpBox("Definition 1행이 엔지니어 제한, 기본 스탯, 성장 파라미터, 업그레이드 비용을 함께 관리합니다. 레벨별 결과표는 터렛 밸런스 리포트에서 자동 계산합니다.", MessageType.Info);

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

        if (GUILayout.Button("리포트 새로고침", GUILayout.Height(34)))
        {
            ExecuteSafely(RefreshReport);
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

    // 터렛 SO 데이터를 CSV로 내보낸다
    private void ExportToCsv()
    {
        ClearRunState();
        List<TurretDefinitionSO> definitions = LoadTurretDefinitions();
        StringBuilder builder = new StringBuilder(8192);
        AppendHeader(builder);
        for (int i = 0; i < definitions.Count; i++)
        {
            AppendDefinitionRow(builder, definitions[i]);
        }

        if (!WriteUtf8Csv(CSV_PATH, builder.ToString()))
        {
            FlushMessagesToConsole(false);
            return;
        }

        AddMessage($"터렛 CSV 익스포트 완료: {definitions.Count}개 Definition");
        FlushMessagesToConsole(true);
    }

    // 터렛 CSV를 읽어 SO 데이터에 반영한다
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
        TurretBalanceReportWindow.RefreshOpenWindows();
        AddMessage($"터렛 CSV 임포트 완료: {updatedCount}개 Definition 갱신");
        AddMessage("열려 있는 터렛 밸런스 리포트를 새로고침했습니다.");
        FlushMessagesToConsole(true);
    }

    // 터렛 CSV 파일을 기본 앱으로 연다
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

    // 열려 있는 터렛 밸런스 리포트를 새로고침한다
    private void RefreshReport()
    {
        ClearRunState();
        TurretBalanceReportWindow.RefreshOpenWindows();
        AddMessage("터렛 밸런스 리포트를 새로고침했습니다.");
        FlushMessagesToConsole(true);
    }

    // CSV 헤더를 추가한다
    private static void AppendHeader(StringBuilder builder)
    {
        AppendCsvLine(
            builder,
            "DefinitionPath",
            "TurretId",
            "DisplayName",
            "MaxLevel",
            "MaxEngineerSeatCount",
            "BaseStatProfilePath",
            "Damage",
            "Range",
            "FireInterval",
            "ProjectileSpeed",
            "ProjectileCount",
            "PierceCount",
            "GrowthProfilePath",
            "GrowthType",
            "DamagePercentPerLevel",
            "RangePerLevel",
            "FireIntervalReductionPerLevel",
            "ProjectileSpeedIntervalLevel",
            "ProjectileSpeedPerInterval",
            "ProjectileCountIntervalLevel",
            "PierceCountIntervalLevel",
            "MaxRange",
            "MinFireInterval",
            "MaxProjectileSpeed",
            "MaxProjectileCount",
            "MaxPierceCount",
            "UpgradeCostProfilePath",
            "UpgradeCostCurrencyType",
            "UpgradeBaseAmountPerLevel",
            "UpgradeAdditionalCostPercentPerTierLevel");
    }

    // 터렛 Definition 한 개를 CSV 행으로 추가한다
    private static void AppendDefinitionRow(StringBuilder builder, TurretDefinitionSO definition)
    {
        TurretStatProfileSO stat = definition.baseStatProfile;
        TurretStatGrowthProfileSO growth = definition.statGrowthProfile;
        TurretUpgradeCostProfileSO cost = definition.upgradeCostProfile;
        ReadCostProfile(cost, out RewardCurrencyType currencyType, out int costAmount, out float costPercent);

        AppendCsvLine(
            builder,
            AssetDatabase.GetAssetPath(definition),
            definition.turretId,
            definition.displayName,
            definition.maxLevel,
            definition.maxEngineerSeatCount,
            AssetDatabase.GetAssetPath(stat),
            stat == null ? 0.0f : stat.damage,
            stat == null ? 0.0f : stat.range,
            stat == null ? 0.0f : stat.fireInterval,
            stat == null ? 0.0f : stat.projectileSpeed,
            stat == null ? 1 : stat.projectileCount,
            stat == null ? 0 : stat.pierceCount,
            AssetDatabase.GetAssetPath(growth),
            GetGrowthType(growth),
            growth == null ? 0.0f : growth.damagePercentPerLevel,
            growth == null ? 0.0f : growth.rangePerLevel,
            growth == null ? 0.0f : growth.fireIntervalReductionPerLevel,
            growth == null ? 0 : growth.projectileSpeedIntervalLevel,
            growth == null ? 0.0f : growth.projectileSpeedPerInterval,
            growth == null ? 0 : growth.projectileCountIntervalLevel,
            growth == null ? 0 : growth.pierceCountIntervalLevel,
            growth == null ? 0.0f : growth.maxRange,
            growth == null ? 0.01f : growth.minFireInterval,
            growth == null ? 0.0f : growth.maxProjectileSpeed,
            growth == null ? 1 : growth.maxProjectileCount,
            growth == null ? 0 : growth.maxPierceCount,
            AssetDatabase.GetAssetPath(cost),
            currencyType.ToString(),
            costAmount,
            costPercent);
    }

    // CSV 행을 터렛 Definition과 연결 프로필에 반영한다
    private bool ApplyRow(List<string> row, Dictionary<string, int> headerMap, int lineNumber)
    {
        string definitionPath = ReadString(row, headerMap, "DefinitionPath");
        TurretDefinitionSO definition = AssetDatabase.LoadAssetAtPath<TurretDefinitionSO>(definitionPath);
        if (definition == null)
        {
            AddMessage($"{lineNumber}행: Definition을 찾을 수 없어 건너뜁니다. 경로: {definitionPath}");
            return false;
        }

        definition.turretId = ReadString(row, headerMap, "TurretId");
        definition.displayName = ReadString(row, headerMap, "DisplayName");
        definition.maxLevel = ReadInt(row, headerMap, "MaxLevel", lineNumber, 0);
        definition.maxEngineerSeatCount = ReadOptionalInt(row, headerMap, "MaxEngineerSeatCount", lineNumber, 0);
        ApplyStatProfile(row, headerMap, lineNumber, definition);
        ApplyGrowthProfile(row, headerMap, lineNumber, definition);
        ApplyCostProfile(row, headerMap, lineNumber, definition);
        EditorUtility.SetDirty(definition);
        return true;
    }

    // CSV 행의 기본 스탯 값을 StatProfile에 반영한다
    private void ApplyStatProfile(List<string> row, Dictionary<string, int> headerMap, int lineNumber, TurretDefinitionSO definition)
    {
        string statPath = ReadString(row, headerMap, "BaseStatProfilePath");
        TurretStatProfileSO stat = AssetDatabase.LoadAssetAtPath<TurretStatProfileSO>(statPath);
        if (stat == null)
        {
            AddMessage($"{lineNumber}행: StatProfile을 찾을 수 없습니다. 경로: {statPath}");
            return;
        }

        definition.baseStatProfile = stat;
        stat.damage = ReadFloat(row, headerMap, "Damage", lineNumber, stat.damage);
        stat.range = ReadFloat(row, headerMap, "Range", lineNumber, stat.range);
        stat.fireInterval = ReadFloat(row, headerMap, "FireInterval", lineNumber, stat.fireInterval);
        stat.projectileSpeed = ReadFloat(row, headerMap, "ProjectileSpeed", lineNumber, stat.projectileSpeed);
        stat.projectileCount = ReadInt(row, headerMap, "ProjectileCount", lineNumber, stat.projectileCount);
        stat.pierceCount = ReadInt(row, headerMap, "PierceCount", lineNumber, stat.pierceCount);
        EditorUtility.SetDirty(stat);
    }

    // CSV 행의 성장 값을 GrowthProfile에 반영한다
    private void ApplyGrowthProfile(List<string> row, Dictionary<string, int> headerMap, int lineNumber, TurretDefinitionSO definition)
    {
        string growthPath = ReadString(row, headerMap, "GrowthProfilePath");
        TurretStatGrowthProfileSO growth = AssetDatabase.LoadAssetAtPath<TurretStatGrowthProfileSO>(growthPath);
        if (growth == null)
        {
            AddMessage($"{lineNumber}행: GrowthProfile을 찾을 수 없습니다. 경로: {growthPath}");
            return;
        }

        definition.statGrowthProfile = growth;
        growth.damagePercentPerLevel = ReadFloat(row, headerMap, "DamagePercentPerLevel", lineNumber, growth.damagePercentPerLevel);
        growth.rangePerLevel = ReadFloat(row, headerMap, "RangePerLevel", lineNumber, growth.rangePerLevel);
        growth.fireIntervalReductionPerLevel = ReadFloat(row, headerMap, "FireIntervalReductionPerLevel", lineNumber, growth.fireIntervalReductionPerLevel);
        growth.projectileSpeedIntervalLevel = ReadInt(row, headerMap, "ProjectileSpeedIntervalLevel", lineNumber, growth.projectileSpeedIntervalLevel);
        growth.projectileSpeedPerInterval = ReadFloat(row, headerMap, "ProjectileSpeedPerInterval", lineNumber, growth.projectileSpeedPerInterval);
        growth.projectileCountIntervalLevel = ReadInt(row, headerMap, "ProjectileCountIntervalLevel", lineNumber, growth.projectileCountIntervalLevel);
        growth.pierceCountIntervalLevel = ReadInt(row, headerMap, "PierceCountIntervalLevel", lineNumber, growth.pierceCountIntervalLevel);
        growth.maxRange = ReadFloat(row, headerMap, "MaxRange", lineNumber, growth.maxRange);
        growth.minFireInterval = ReadFloat(row, headerMap, "MinFireInterval", lineNumber, growth.minFireInterval);
        growth.maxProjectileSpeed = ReadFloat(row, headerMap, "MaxProjectileSpeed", lineNumber, growth.maxProjectileSpeed);
        growth.maxProjectileCount = ReadInt(row, headerMap, "MaxProjectileCount", lineNumber, growth.maxProjectileCount);
        growth.maxPierceCount = ReadInt(row, headerMap, "MaxPierceCount", lineNumber, growth.maxPierceCount);
        EditorUtility.SetDirty(growth);
    }

    // CSV 행의 업그레이드 비용 값을 CostProfile에 반영한다
    private void ApplyCostProfile(List<string> row, Dictionary<string, int> headerMap, int lineNumber, TurretDefinitionSO definition)
    {
        string costPath = ReadString(row, headerMap, "UpgradeCostProfilePath");
        TurretUpgradeCostProfileSO costProfile = AssetDatabase.LoadAssetAtPath<TurretUpgradeCostProfileSO>(costPath);
        if (costProfile == null)
        {
            AddMessage($"{lineNumber}행: UpgradeCostProfile을 찾을 수 없습니다. 경로: {costPath}");
            return;
        }

        definition.upgradeCostProfile = costProfile;
        SerializedObject serializedObject = new SerializedObject(costProfile);
        SerializedProperty baseCosts = serializedObject.FindProperty(BASE_COSTS_PROPERTY);
        SerializedProperty percent = serializedObject.FindProperty(ADDITIONAL_COST_PERCENT_PROPERTY);
        baseCosts.arraySize = 1;
        SerializedProperty cost = baseCosts.GetArrayElementAtIndex(0);
        cost.FindPropertyRelative("currencyType").enumValueIndex = GetRewardCurrencyIndex(ReadRewardCurrencyType(row, headerMap, "UpgradeCostCurrencyType", lineNumber));
        cost.FindPropertyRelative("amount").intValue = ReadInt(row, headerMap, "UpgradeBaseAmountPerLevel", lineNumber, 0);
        percent.floatValue = ReadFloat(row, headerMap, "UpgradeAdditionalCostPercentPerTierLevel", lineNumber, 0.0f);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(costProfile);
    }

    // 모든 터렛 Definition을 로드한다
    private static List<TurretDefinitionSO> LoadTurretDefinitions()
    {
        string[] guids = AssetDatabase.FindAssets("t:TurretDefinitionSO");
        List<TurretDefinitionSO> definitions = new List<TurretDefinitionSO>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TurretDefinitionSO definition = AssetDatabase.LoadAssetAtPath<TurretDefinitionSO>(path);
            if (definition != null && IsManagedGenerationPath(path))
            {
                definitions.Add(definition);
            }
        }

        definitions.Sort(CompareTurretDefinitions);
        return definitions;
    }

    // 터렛 Definition을 파일 디렉토리 세대와 경로 기준으로 정렬한다
    private static int CompareTurretDefinitions(TurretDefinitionSO left, TurretDefinitionSO right)
    {
        string leftPath = AssetDatabase.GetAssetPath(left);
        string rightPath = AssetDatabase.GetAssetPath(right);
        int generationCompare = GetGenerationOrder(leftPath).CompareTo(GetGenerationOrder(rightPath));
        if (generationCompare != 0)
        {
            return generationCompare;
        }

        return string.CompareOrdinal(leftPath, rightPath);
    }

    // CSV 관리 대상 세대 폴더에 포함된 경로인지 확인한다
    private static bool IsManagedGenerationPath(string assetPath)
    {
        return GetGenerationOrder(assetPath) <= 3;
    }

    // 에셋 경로에서 1stGen, 2ndGen, 3rdGen 정렬 순서를 반환한다
    private static int GetGenerationOrder(string assetPath)
    {
        if (assetPath.IndexOf("/1stGen/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 1;
        }

        if (assetPath.IndexOf("/2ndGen/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 2;
        }

        if (assetPath.IndexOf("/3rdGen/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 3;
        }

        return 99;
    }

    // 비용 프로필의 첫 번째 기본 비용과 성장률을 읽는다
    private static void ReadCostProfile(TurretUpgradeCostProfileSO profile, out RewardCurrencyType currencyType, out int amount, out float percent)
    {
        currencyType = RewardCurrencyType.Coin;
        amount = 0;
        percent = 0.0f;
        if (profile == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(profile);
        SerializedProperty baseCosts = serializedObject.FindProperty(BASE_COSTS_PROPERTY);
        SerializedProperty percentProperty = serializedObject.FindProperty(ADDITIONAL_COST_PERCENT_PROPERTY);
        percent = percentProperty == null ? 0.0f : percentProperty.floatValue;
        if (baseCosts == null || !baseCosts.isArray || baseCosts.arraySize <= 0)
        {
            return;
        }

        SerializedProperty firstCost = baseCosts.GetArrayElementAtIndex(0);
        currencyType = (RewardCurrencyType)firstCost.FindPropertyRelative("currencyType").enumValueIndex;
        amount = firstCost.FindPropertyRelative("amount").intValue;
    }

    // 성장 프로필의 타입 이름을 반환한다
    private static string GetGrowthType(TurretStatGrowthProfileSO growth)
    {
        return growth == null ? string.Empty : growth.GetType().Name;
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

        AddMessage("필수 CSV 컬럼이 없습니다: DefinitionPath");
        return false;
    }

    // CSV 헤더를 컬럼 인덱스 맵으로 변환한다
    private bool TryBuildHeaderMap(List<string> headers, out Dictionary<string, int> headerMap, bool reportMissing)
    {
        headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            string header = (headers[i] ?? string.Empty).Trim('\uFEFF').Trim();
            if (!string.IsNullOrEmpty(header) && !headerMap.ContainsKey(header))
            {
                headerMap.Add(header, i);
            }
        }

        string[] requiredColumns = { "DefinitionPath", "TurretId", "DisplayName", "BaseStatProfilePath", "GrowthProfilePath", "UpgradeCostProfilePath" };
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
    private bool WriteUtf8Csv(string path, string text)
    {
        try
        {
            string directoryPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(path, text, new UTF8Encoding(true));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            return true;
        }
        catch (IOException exception)
        {
            AddMessage("CSV 파일을 저장할 수 없습니다. 파일이 Excel 등 다른 프로그램에서 열려 있으면 닫고 다시 시도하세요: " + path);
            AddMessage(exception.Message);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            AddMessage("CSV 파일 저장 권한이 없습니다. 파일 권한 또는 읽기 전용 상태를 확인하세요: " + path);
            AddMessage(exception.Message);
            return false;
        }
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

    // CSV 행에서 정수 값을 읽는다
    private int ReadInt(List<string> row, Dictionary<string, int> headerMap, string columnName, int lineNumber, int fallback)
    {
        string value = ReadString(row, headerMap, columnName);
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            return result;
        }

        AddMessage($"{lineNumber}행: {columnName} 정수 값이 유효하지 않아 {fallback}을 사용합니다. 값: {value}");
        return fallback;
    }

    // 선택 CSV 정수 컬럼이 없거나 비어 있으면 기본값을 반환한다
    private int ReadOptionalInt(List<string> row, Dictionary<string, int> headerMap, string columnName, int lineNumber, int fallback)
    {
        if (!headerMap.ContainsKey(columnName))
        {
            return fallback;
        }

        string value = ReadString(row, headerMap, columnName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return ReadInt(row, headerMap, columnName, lineNumber, fallback);
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

    // CSV 행에서 재화 enum 값을 읽는다
    private RewardCurrencyType ReadRewardCurrencyType(List<string> row, Dictionary<string, int> headerMap, string columnName, int lineNumber)
    {
        string value = ReadString(row, headerMap, columnName);
        if (Enum.TryParse(value, out RewardCurrencyType result))
        {
            return result;
        }

        AddMessage($"{lineNumber}행: {columnName} 값이 유효하지 않아 Coin을 사용합니다. 값: {value}");
        return RewardCurrencyType.Coin;
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
            Debug.Log("[TurretDataCsvEditorTool]\n" + joinedMessage);
        }
        else
        {
            Debug.LogWarning("[TurretDataCsvEditorTool]\n" + joinedMessage);
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
