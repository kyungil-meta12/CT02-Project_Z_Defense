#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 터렛 진화 요구 레벨과 진화 재료 비용을 단일 CSV로 관리한다.
/// </summary>
public class TurretEvolutionCostCsvEditorTool : EditorWindow
{
    private const string CSV_PATH = "Assets/__PROJECT__/Scenes/KKW/Turret_Scene/SO/TurretEvolutionCosts.csv";
    private const string EVOLUTION_ENTRIES_PROPERTY = "evolutionEntries";
    private const string REQUIRED_LEVEL_PROPERTY = "requiredLevel";
    private const string EVOLUTION_COSTS_PROPERTY = "evolutionCosts";
    private const string CURRENCY_TYPE_PROPERTY = "currencyType";
    private const string AMOUNT_PROPERTY = "amount";

    private Vector2 scrollPosition;
    private readonly List<string> lastMessages = new List<string>(32);

    [MenuItem("Tools/터렛 진화 재료 CSV 관리 도구")]
    // 터렛 진화 재료 CSV 관리 창을 연다
    public static void ShowWindow()
    {
        GetWindow<TurretEvolutionCostCsvEditorTool>("터렛 진화 재료 CSV");
    }

    // 에디터 창 UI를 그린다
    private void OnGUI()
    {
        EditorGUILayout.LabelField("터렛 진화 재료 CSV 관리 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("CSV 파일 경로", CSV_PATH);
        EditorGUILayout.HelpBox("현재 터렛 이름과 진화 대상 터렛 이름으로 진화 요구 레벨과 재료 비용을 관리합니다. 비용은 아이템 조합법처럼 Coin:100;Stone:5 형식으로 입력합니다.", MessageType.Info);

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

    // 터렛 진화 재료 데이터를 CSV로 내보낸다
    private void ExportToCsv()
    {
        ClearRunState();
        List<TurretDefinitionSO> definitions = LoadTurretDefinitions();
        StringBuilder builder = new StringBuilder(8192);
        AppendHeader(builder);

        int rowCount = AppendEvolutionRowsInOrder(builder, definitions);

        if (!WriteUtf8Csv(CSV_PATH, builder.ToString()))
        {
            FlushMessagesToConsole(false);
            return;
        }

        AddMessage($"터렛 진화 재료 CSV 익스포트 완료: {rowCount}개 진화 엔트리");
        FlushMessagesToConsole(true);
    }

    // 루트 터렛부터 진화 트리 순서대로 CSV 행을 추가한다
    private static int AppendEvolutionRowsInOrder(StringBuilder builder, List<TurretDefinitionSO> definitions)
    {
        HashSet<TurretDefinitionSO> managedDefinitions = new HashSet<TurretDefinitionSO>(definitions);
        HashSet<TurretDefinitionSO> expandedDefinitions = new HashSet<TurretDefinitionSO>();
        Queue<TurretDefinitionSO> queue = new Queue<TurretDefinitionSO>();
        List<TurretDefinitionSO> roots = LoadRootDefinitions();
        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i] != null && managedDefinitions.Contains(roots[i]) && !expandedDefinitions.Contains(roots[i]))
            {
                queue.Enqueue(roots[i]);
            }
        }

        if (queue.Count == 0)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null)
                {
                    queue.Enqueue(definitions[i]);
                }
            }
        }

        int rowCount = AppendQueuedEvolutionRows(builder, queue, managedDefinitions, expandedDefinitions);
        for (int i = 0; i < definitions.Count; i++)
        {
            TurretDefinitionSO definition = definitions[i];
            if (definition == null || expandedDefinitions.Contains(definition))
            {
                continue;
            }

            queue.Enqueue(definition);
            rowCount += AppendQueuedEvolutionRows(builder, queue, managedDefinitions, expandedDefinitions);
        }

        return rowCount;
    }

    // 큐에 들어 있는 터렛들을 진화 순서대로 펼쳐 CSV 행을 추가한다
    private static int AppendQueuedEvolutionRows(StringBuilder builder, Queue<TurretDefinitionSO> queue, HashSet<TurretDefinitionSO> managedDefinitions, HashSet<TurretDefinitionSO> expandedDefinitions)
    {
        int rowCount = 0;
        while (queue.Count > 0)
        {
            TurretDefinitionSO sourceDefinition = queue.Dequeue();
            if (sourceDefinition == null || expandedDefinitions.Contains(sourceDefinition))
            {
                continue;
            }

            expandedDefinitions.Add(sourceDefinition);
            TurretEvolutionProgressionSO progression = sourceDefinition.evolutionProgressionProfile;
            TurretEvolutionEntry[] entries = progression == null ? null : progression.evolutionEntries;
            if (entries == null || entries.Length == 0)
            {
                continue;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                TurretEvolutionEntry entry = entries[i];
                if (entry == null || entry.targetDefinition == null)
                {
                    continue;
                }

                AppendEvolutionRow(builder, sourceDefinition, entry);
                rowCount++;

                if (managedDefinitions.Contains(entry.targetDefinition) && !expandedDefinitions.Contains(entry.targetDefinition))
                {
                    queue.Enqueue(entry.targetDefinition);
                }
            }
        }

        return rowCount;
    }

    // 터렛 진화 재료 CSV를 읽어 SO 데이터에 반영한다
    private void ImportFromCsv()
    {
        ClearRunState();
        if (!TryBuildTurretDefinitionMap(out Dictionary<string, TurretDefinitionSO> definitionByDisplayName))
        {
            FlushMessagesToConsole(false);
            return;
        }

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

            if (ApplyRow(row, headerMap, i + 1, definitionByDisplayName))
            {
                updatedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        TurretBalanceReportWindow.RefreshOpenWindows();
        AddMessage($"터렛 진화 재료 CSV 임포트 완료: {updatedCount}개 진화 엔트리 갱신");
        AddMessage("열려 있는 터렛 밸런스 리포트를 새로고침했습니다.");
        FlushMessagesToConsole(true);
    }

    // 터렛 진화 재료 CSV 파일을 기본 앱으로 연다
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
        List<object> columns = new List<object>(4)
        {
            "EvolutionProfilePath(임포트 매칭용 진화 프로필 경로)",
            "SourceDisplayName(현재 터렛 이름)",
            "TargetDisplayName(진화 대상 터렛 이름)",
            "RequiredLevel(진화 요구 티어 레벨)",
            "EvolutionCosts(진화 재료: Coin:100;Stone:5)"
        };

        AppendCsvLine(builder, columns);
    }

    // 터렛 Definition의 진화 엔트리를 CSV 행으로 추가한다
    private static int AppendDefinitionRows(StringBuilder builder, TurretDefinitionSO sourceDefinition)
    {
        TurretEvolutionProgressionSO progression = sourceDefinition == null ? null : sourceDefinition.evolutionProgressionProfile;
        TurretEvolutionEntry[] entries = progression == null ? null : progression.evolutionEntries;
        if (entries == null || entries.Length == 0)
        {
            return 0;
        }

        int rowCount = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            TurretEvolutionEntry entry = entries[i];
            if (entry == null || entry.targetDefinition == null)
            {
                continue;
            }

            AppendEvolutionRow(builder, sourceDefinition, entry);
            rowCount++;
        }

        return rowCount;
    }

    // 단일 진화 엔트리를 CSV 행으로 추가한다
    private static void AppendEvolutionRow(StringBuilder builder, TurretDefinitionSO sourceDefinition, TurretEvolutionEntry entry)
    {
        List<object> columns = new List<object>(4)
        {
            AssetDatabase.GetAssetPath(sourceDefinition.evolutionProgressionProfile),
            sourceDefinition.displayName,
            entry.targetDefinition.displayName,
            entry.requiredLevel,
            FormatCosts(entry.evolutionCosts)
        };

        AppendCsvLine(builder, columns);
    }

    // CSV 경로 컬럼으로 진화 프로필 에셋을 로드한다
    private static TurretEvolutionProgressionSO LoadEvolutionProgression(string evolutionProfilePath)
    {
        if (string.IsNullOrWhiteSpace(evolutionProfilePath))
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<TurretEvolutionProgressionSO>(evolutionProfilePath);
    }

    // CSV 행을 진화 엔트리에 반영한다
    private bool ApplyRow(List<string> row, Dictionary<string, int> headerMap, int lineNumber, Dictionary<string, TurretDefinitionSO> definitionByDisplayName)
    {
        string sourceDisplayName = ReadString(row, headerMap, "SourceDisplayName").Trim();
        string evolutionProfilePath = ReadString(row, headerMap, "EvolutionProfilePath").Trim();
        TurretDefinitionSO sourceDefinition = null;
        TurretEvolutionProgressionSO progression = LoadEvolutionProgression(evolutionProfilePath);
        if (progression == null && !definitionByDisplayName.TryGetValue(sourceDisplayName, out sourceDefinition))
        {
            AddMessage($"{lineNumber}행: EvolutionProfilePath와 SourceDisplayName에 해당하는 진화 프로필을 찾을 수 없어 건너뜁니다. 경로: {evolutionProfilePath}, 이름: {sourceDisplayName}");
            return false;
        }

        if (progression == null)
        {
            progression = sourceDefinition.evolutionProgressionProfile;
        }

        if (progression == null || progression.evolutionEntries == null)
        {
            AddMessage($"{lineNumber}행: 진화 프로필이 없거나 비어 있어 건너뜁니다. 경로: {evolutionProfilePath}, 이름: {sourceDisplayName}");
            return false;
        }

        string targetDisplayName = ReadString(row, headerMap, "TargetDisplayName").Trim();
        int resolvedIndex = ResolveEntryIndex(progression, targetDisplayName, lineNumber);
        if (resolvedIndex < 0)
        {
            return false;
        }

        SerializedObject serializedProgression = new SerializedObject(progression);
        SerializedProperty entries = serializedProgression.FindProperty(EVOLUTION_ENTRIES_PROPERTY);
        if (entries == null || !entries.isArray || resolvedIndex >= entries.arraySize)
        {
            AddMessage($"{lineNumber}행: Evolution Entries 직렬화 데이터를 찾을 수 없어 건너뜁니다. 이름: {sourceDisplayName}, 대상: {targetDisplayName}");
            return false;
        }

        SerializedProperty entry = entries.GetArrayElementAtIndex(resolvedIndex);
        SerializedProperty requiredLevel = entry.FindPropertyRelative(REQUIRED_LEVEL_PROPERTY);
        SerializedProperty evolutionCosts = entry.FindPropertyRelative(EVOLUTION_COSTS_PROPERTY);
        if (requiredLevel == null || evolutionCosts == null || !evolutionCosts.isArray)
        {
            AddMessage($"{lineNumber}행: 진화 엔트리 직렬화 필드를 찾을 수 없어 건너뜁니다. 이름: {sourceDisplayName}, 대상: {targetDisplayName}");
            return false;
        }

        requiredLevel.intValue = Mathf.Max(1, ReadInt(row, headerMap, "RequiredLevel", lineNumber, requiredLevel.intValue));
        if (!TryReadCosts(row, headerMap, lineNumber, out List<ResourceCost> costs))
        {
            return false;
        }

        ApplyCosts(evolutionCosts, costs);
        serializedProgression.ApplyModifiedProperties();
        EditorUtility.SetDirty(progression);
        return true;
    }

    // CSV의 대상 터렛 이름으로 실제 진화 엔트리 인덱스를 찾는다
    private int ResolveEntryIndex(TurretEvolutionProgressionSO progression, string targetDisplayName, int lineNumber)
    {
        TurretEvolutionEntry[] entries = progression.evolutionEntries;
        int resolvedIndex = -1;
        for (int i = 0; i < entries.Length; i++)
        {
            if (!IsTargetDisplayName(entries[i], targetDisplayName))
            {
                continue;
            }

            if (resolvedIndex >= 0)
            {
                AddMessage($"{lineNumber}행: 같은 TargetDisplayName 진화 엔트리가 둘 이상 있어 건너뜁니다. 대상 이름: {targetDisplayName}");
                return -1;
            }

            resolvedIndex = i;
        }

        if (resolvedIndex < 0)
        {
            AddMessage($"{lineNumber}행: TargetDisplayName에 해당하는 진화 엔트리를 찾을 수 없어 건너뜁니다. 대상 이름: {targetDisplayName}");
        }

        return resolvedIndex;
    }

    // 진화 엔트리의 대상 터렛 표시 이름이 일치하는지 확인한다
    private static bool IsTargetDisplayName(TurretEvolutionEntry entry, string targetDisplayName)
    {
        return entry != null &&
               entry.targetDefinition != null &&
               string.Equals(entry.targetDefinition.displayName, targetDisplayName, StringComparison.Ordinal);
    }

    // 진화 비용 목록을 아이템 조합법 CSV 형식으로 변환한다
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
            if (cost == null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(';');
            }

            builder.Append(cost.currencyType.ToString());
            builder.Append(':');
            builder.Append(Mathf.Max(0, cost.amount));
        }

        return builder.ToString();
    }

    // CSV 행에서 진화 비용 목록을 읽는다
    private bool TryReadCosts(List<string> row, Dictionary<string, int> headerMap, int lineNumber, out List<ResourceCost> costs)
    {
        costs = null;
        Dictionary<RewardCurrencyType, int> amountByCurrency = new Dictionary<RewardCurrencyType, int>();
        string costsText = ReadString(row, headerMap, "EvolutionCosts").Trim();
        if (!TryParseCostsText(costsText, lineNumber, amountByCurrency))
        {
            return false;
        }

        costs = new List<ResourceCost>(amountByCurrency.Count);
        string[] currencyNames = Enum.GetNames(typeof(RewardCurrencyType));
        for (int i = 0; i < currencyNames.Length; i++)
        {
            if (!Enum.TryParse(currencyNames[i], out RewardCurrencyType currencyType))
            {
                continue;
            }

            if (amountByCurrency.TryGetValue(currencyType, out int amount))
            {
                costs.Add(new ResourceCost(currencyType, amount));
            }
        }

        return true;
    }

    // 조합법 형식의 비용 문자열을 재화별 합계로 파싱한다
    private bool TryParseCostsText(string costsText, int lineNumber, Dictionary<RewardCurrencyType, int> amountByCurrency)
    {
        if (string.IsNullOrWhiteSpace(costsText))
        {
            return true;
        }

        string[] entries = costsText.Split(';');
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
                AddMessage($"{lineNumber}행: EvolutionCosts 항목 형식이 유효하지 않아 해당 행을 건너뜁니다. 값: {entry}");
                return false;
            }

            string currencyText = ExtractEnumName(parts[0].Trim());
            if (!Enum.TryParse(currencyText, out RewardCurrencyType currencyType))
            {
                AddMessage($"{lineNumber}행: EvolutionCosts 재화 값이 유효하지 않아 해당 행을 건너뜁니다. 값: {parts[0]}");
                return false;
            }

            if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
            {
                AddMessage($"{lineNumber}행: EvolutionCosts 수량 값이 유효하지 않아 해당 행을 건너뜁니다. 값: {parts[1]}");
                return false;
            }

            if (amount < 0)
            {
                AddMessage($"{lineNumber}행: EvolutionCosts 수량 값이 음수라 0으로 보정합니다. 값: {amount}");
                amount = 0;
            }

            if (amountByCurrency.TryGetValue(currencyType, out int existingAmount))
            {
                amountByCurrency[currencyType] = existingAmount + amount;
                AddMessage($"{lineNumber}행: {currencyType} 비용이 여러 번 입력되어 합산합니다.");
                continue;
            }

            amountByCurrency.Add(currencyType, amount);
        }

        return true;
    }

    // enum=값 형식 입력에서 enum 이름만 추출한다
    private static string ExtractEnumName(string text)
    {
        int equalsIndex = text.IndexOf('=');
        return equalsIndex >= 0 ? text.Substring(0, equalsIndex).Trim() : text;
    }

    // 직렬화된 evolutionCosts 배열에 비용 목록을 쓴다
    private static void ApplyCosts(SerializedProperty evolutionCosts, List<ResourceCost> costs)
    {
        evolutionCosts.arraySize = costs.Count;
        for (int i = 0; i < costs.Count; i++)
        {
            SerializedProperty cost = evolutionCosts.GetArrayElementAtIndex(i);
            cost.FindPropertyRelative(CURRENCY_TYPE_PROPERTY).enumValueIndex = GetRewardCurrencyIndex(costs[i].currencyType);
            cost.FindPropertyRelative(AMOUNT_PROPERTY).intValue = costs[i].amount;
        }
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

    // 상점 엔트리에 연결된 루트 터렛 Definition을 로드한다
    private static List<TurretDefinitionSO> LoadRootDefinitions()
    {
        string[] guids = AssetDatabase.FindAssets("t:TurretShopEntrySO");
        List<TurretShopEntrySO> shopEntries = new List<TurretShopEntrySO>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TurretShopEntrySO entry = AssetDatabase.LoadAssetAtPath<TurretShopEntrySO>(path);
            if (entry != null && entry.TurretDefinition != null)
            {
                shopEntries.Add(entry);
            }
        }

        shopEntries.Sort(CompareShopEntries);
        List<TurretDefinitionSO> roots = new List<TurretDefinitionSO>(shopEntries.Count);
        HashSet<TurretDefinitionSO> addedDefinitions = new HashSet<TurretDefinitionSO>();
        for (int i = 0; i < shopEntries.Count; i++)
        {
            TurretDefinitionSO definition = shopEntries[i].TurretDefinition;
            if (definition != null && addedDefinitions.Add(definition))
            {
                roots.Add(definition);
            }
        }

        return roots;
    }

    // 터렛 상점 엔트리를 표시 이름 기준으로 정렬한다
    private static int CompareShopEntries(TurretShopEntrySO left, TurretShopEntrySO right)
    {
        return string.Compare(GetShopEntryName(left), GetShopEntryName(right), StringComparison.Ordinal);
    }

    // 터렛 상점 엔트리의 표시 이름을 반환한다
    private static string GetShopEntryName(TurretShopEntrySO entry)
    {
        return entry == null ? string.Empty : entry.DisplayName;
    }

    // 터렛 Definition을 표시 이름 기준 딕셔너리로 만든다
    private bool TryBuildTurretDefinitionMap(out Dictionary<string, TurretDefinitionSO> definitionByDisplayName)
    {
        definitionByDisplayName = new Dictionary<string, TurretDefinitionSO>(StringComparer.Ordinal);
        List<TurretDefinitionSO> definitions = LoadTurretDefinitions();
        bool hasDuplicate = false;

        for (int i = 0; i < definitions.Count; i++)
        {
            TurretDefinitionSO definition = definitions[i];
            string displayName = definition == null ? string.Empty : (definition.displayName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                AddMessage("displayName이 비어 있는 터렛 Definition을 건너뜁니다: " + AssetDatabase.GetAssetPath(definition));
                continue;
            }

            if (definitionByDisplayName.TryGetValue(displayName, out TurretDefinitionSO existingDefinition))
            {
                hasDuplicate = true;
                AddMessage($"중복 displayName이 발견되어 임포트를 중단합니다. 이름: {displayName}");
                AddMessage(" - " + AssetDatabase.GetAssetPath(existingDefinition));
                AddMessage(" - " + AssetDatabase.GetAssetPath(definition));
                continue;
            }

            definitionByDisplayName.Add(displayName, definition);
        }

        return !hasDuplicate;
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

    // CSV 파일을 읽고 테이블로 파싱한다
    private bool TryReadCsv(string path, out List<List<string>> table)
    {
        table = null;
        if (!File.Exists(path))
        {
            AddMessage("CSV 파일을 찾을 수 없습니다: " + path);
            return false;
        }

        string text;
        try
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                text = reader.ReadToEnd();
            }
        }
        catch (IOException exception)
        {
            AddMessage("CSV 파일을 읽을 수 없습니다. Excel에서 저장 중이거나 파일을 독점 잠금 중일 수 있습니다.");
            AddMessage(exception.Message);
            return false;
        }

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

        AddMessage("필수 CSV 컬럼이 없습니다: SourceDisplayName");
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

        string[] requiredColumns = { "SourceDisplayName", "TargetDisplayName", "RequiredLevel", "EvolutionCosts" };
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
        string normalized = (header ?? string.Empty).Trim('\uFEFF').Trim();
        int descriptionStartIndex = normalized.IndexOf('(');
        if (descriptionStartIndex < 0)
        {
            descriptionStartIndex = normalized.IndexOf('（');
        }

        if (descriptionStartIndex >= 0)
        {
            normalized = normalized.Substring(0, descriptionStartIndex).Trim();
        }

        return normalized;
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
    private static void AppendCsvLine(StringBuilder builder, List<object> columns)
    {
        for (int i = 0; i < columns.Count; i++)
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
            Debug.Log("[TurretEvolutionCostCsvEditorTool]\n" + joinedMessage);
        }
        else
        {
            Debug.LogWarning("[TurretEvolutionCostCsvEditorTool]\n" + joinedMessage);
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
