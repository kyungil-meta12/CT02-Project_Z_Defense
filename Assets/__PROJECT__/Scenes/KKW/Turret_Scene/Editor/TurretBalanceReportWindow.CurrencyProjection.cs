using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 밸런스 리포트의 필요 재화 필터와 CSV 기반 조합/분해 가상 수량을 계산한다.
internal static class TurretBalanceReportCurrencyProjector
{
    private const string ITEM_DATA_CSV_PATH = "Assets/__PROJECT__/Prefabs/InventorySystem/ItemData.csv";
    private const string TYPE_COLUMN = "Type";
    private const string CREATEABLE_COLUMN = "Createable";
    private const string COUNT_PER_CRAFT_COLUMN = "CountPerCraft";
    private const string CRAFT_COLUMN = "ItemsToCreate";
    private const string DECOMPOSABLE_COLUMN = "Decomposable";
    private const string DECOMPOSE_COLUMN = "ItemsFromDecompose";

    private static readonly Dictionary<RewardCurrencyType, ItemRelation> Relations = new Dictionary<RewardCurrencyType, ItemRelation>();
    private static readonly List<ItemRelation> RelationList = new List<ItemRelation>();
    private static bool hasLoadedRelations;
    private static string loadedRelationSignature = string.Empty;

    // 터렛 리포트 결과에서 설치/업그레이드/진화 비용에 필요한 재화 범위를 만든다
    public static HashSet<RewardCurrencyType> BuildTurretCurrencyScope(TurretBalanceReportResult report)
    {
        HashSet<RewardCurrencyType> directCosts = new HashSet<RewardCurrencyType>();
        if (report == null)
        {
            return ExpandCurrencyScope(directCosts);
        }

        AddTurretPlacementCosts(directCosts);
        for (int i = 0; i < report.SpeciesDetailRows.Count; i++)
        {
            TurretSpeciesDetailRow row = report.SpeciesDetailRows[i];
            AddCostKeys(directCosts, row.NextEvolutionCumulativeCost);
            if (row.LevelSamples == null)
            {
                continue;
            }

            for (int j = 0; j < row.LevelSamples.Count; j++)
            {
                AddCostKeys(directCosts, row.LevelSamples[j].CumulativeCost);
            }
        }

        return ExpandCurrencyScope(directCosts);
    }

    // 터렛 상점 엔트리의 설치 비용 재화를 비용 범위에 추가한다
    private static void AddTurretPlacementCosts(HashSet<RewardCurrencyType> directCosts)
    {
        if (directCosts == null)
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:TurretShopEntrySO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TurretShopEntrySO entry = AssetDatabase.LoadAssetAtPath<TurretShopEntrySO>(path);
            if (entry == null)
            {
                continue;
            }

            AddCostKeys(directCosts, ExtractCostKeys(entry.GetPlacementCosts(0)));
        }
    }

    // 장애물 항목에서 건설/업그레이드 비용에 필요한 재화 범위를 만든다
    public static HashSet<RewardCurrencyType> BuildObstacleCurrencyScope(List<ObstacleEntrySpec> entries)
    {
        HashSet<RewardCurrencyType> directCosts = new HashSet<RewardCurrencyType>();
        if (entries == null)
        {
            return ExpandCurrencyScope(directCosts);
        }

        for (int i = 0; i < entries.Count; i++)
        {
            ObstacleEntrySpec entry = entries[i];
            AddCostKeys(directCosts, entry.BuildCosts);
            ObstacleDefinitionSO definition = entry.Definition;
            if (definition == null)
            {
                continue;
            }

            int maxLevel = Mathf.Max(1, definition.MaxLevel);
            AddCostKeys(directCosts, ExtractCostKeys(definition.GetUpgradeCosts(1, maxLevel)));
        }

        return ExpandCurrencyScope(directCosts);
    }

    // RewardCurrencyType enum 전체를 아이템 밸런스 계산 범위로 만든다
    public static HashSet<RewardCurrencyType> BuildAllCurrencyScope()
    {
        HashSet<RewardCurrencyType> currencyScope = new HashSet<RewardCurrencyType>();
        Array values = Enum.GetValues(typeof(RewardCurrencyType));
        for (int i = 0; i < values.Length; i++)
        {
            currencyScope.Add((RewardCurrencyType)values.GetValue(i));
        }

        return ExpandCurrencyScope(currencyScope);
    }

    // 이미 계산된 아이템 밸런스 행에서 지정 범위의 재화만 복사한다
    public static Dictionary<RewardCurrencyType, float> FilterItemAmounts(ItemBalanceRow itemRow, HashSet<RewardCurrencyType> currencyScope)
    {
        Dictionary<RewardCurrencyType, float> result = new Dictionary<RewardCurrencyType, float>();
        if (currencyScope == null || itemRow.MaxItemAmounts == null)
        {
            return result;
        }

        foreach (RewardCurrencyType currencyType in currencyScope)
        {
            if (itemRow.MaxItemAmounts.TryGetValue(currencyType, out float amount) && amount > 0f)
            {
                result[currencyType] = amount;
            }
        }

        return result;
    }

    // 원본 누적 보상에서 필요 재화만 남기고 조합/분해 가상 수량을 반영한다
    public static Dictionary<RewardCurrencyType, float> ProjectRewards(Dictionary<RewardCurrencyType, float> source, HashSet<RewardCurrencyType> currencyScope)
    {
        Dictionary<RewardCurrencyType, float> projected = new Dictionary<RewardCurrencyType, float>();
        if (currencyScope == null || currencyScope.Count == 0)
        {
            return projected;
        }

        foreach (RewardCurrencyType currencyType in currencyScope)
        {
            Dictionary<RewardCurrencyType, float> cache = new Dictionary<RewardCurrencyType, float>();
            HashSet<RewardCurrencyType> visiting = new HashSet<RewardCurrencyType>();
            float amount = CalculateVirtualAmount(currencyType, source, currencyScope, cache, visiting);
            if (amount > 0f)
            {
                projected[currencyType] = amount;
            }
        }

        return projected;
    }

    // 웨이브 예산용 원본 재화 표를 만든다
    public static Dictionary<RewardCurrencyType, float> BuildWaveBudgetSource(WaveSummaryRow wave)
    {
        Dictionary<RewardCurrencyType, float> budget = new Dictionary<RewardCurrencyType, float>();
        budget[RewardCurrencyType.Coin] = Mathf.Max(0.0f, wave.AvailableBudgetCoin);
        if (wave.CumulativeReward == null)
        {
            return budget;
        }

        foreach (KeyValuePair<RewardCurrencyType, float> pair in wave.CumulativeReward)
        {
            if (pair.Key == RewardCurrencyType.Coin)
            {
                continue;
            }

            budget[pair.Key] = pair.Value;
        }

        return budget;
    }

    // 비용 Dictionary의 재화 키를 대상 집합에 추가한다
    private static void AddCostKeys(HashSet<RewardCurrencyType> target, Dictionary<RewardCurrencyType, int> costs)
    {
        if (target == null || costs == null)
        {
            return;
        }

        foreach (KeyValuePair<RewardCurrencyType, int> pair in costs)
        {
            if (pair.Value > 0)
            {
                target.Add(pair.Key);
            }
        }
    }

    // ResourceCost 배열을 재화 키 Dictionary로 변환한다
    private static Dictionary<RewardCurrencyType, int> ExtractCostKeys(ResourceCost[] costs)
    {
        Dictionary<RewardCurrencyType, int> result = new Dictionary<RewardCurrencyType, int>();
        if (costs == null)
        {
            return result;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            result.TryGetValue(cost.currencyType, out int existing);
            result[cost.currencyType] = existing + cost.amount;
        }

        return result;
    }

    // 직접 비용 재화에서 조합 재료와 분해 원본을 포함한 표시 범위를 확장한다
    private static HashSet<RewardCurrencyType> ExpandCurrencyScope(HashSet<RewardCurrencyType> directCosts)
    {
        EnsureRelationsLoaded();
        HashSet<RewardCurrencyType> scope = new HashSet<RewardCurrencyType>();
        HashSet<RewardCurrencyType> visiting = new HashSet<RewardCurrencyType>();
        foreach (RewardCurrencyType currencyType in directCosts)
        {
            ExpandCurrency(currencyType, scope, visiting);
        }

        return scope;
    }

    // 지정 재화와 연결된 조합 입력 및 분해 원본을 재귀적으로 추가한다
    private static void ExpandCurrency(RewardCurrencyType currencyType, HashSet<RewardCurrencyType> scope, HashSet<RewardCurrencyType> visiting)
    {
        if (!scope.Add(currencyType) && visiting.Contains(currencyType))
        {
            return;
        }

        if (!visiting.Add(currencyType))
        {
            return;
        }

        if (Relations.TryGetValue(currencyType, out ItemRelation relation) && relation.Createable && relation.CountPerCraft > 0)
        {
            for (int i = 0; i < relation.CraftInputs.Count; i++)
            {
                ExpandCurrency(relation.CraftInputs[i].Type, scope, visiting);
            }
        }

        for (int i = 0; i < RelationList.Count; i++)
        {
            ItemRelation source = RelationList[i];
            if (!source.Decomposable)
            {
                continue;
            }

            if (ContainsDecomposeOutput(source, currencyType))
            {
                ExpandCurrency(source.Type, scope, visiting);
            }
        }

        visiting.Remove(currencyType);
    }

    // 지정 재화의 직접/분해/조합 가상 수량을 계산한다
    private static float CalculateVirtualAmount(
        RewardCurrencyType currencyType,
        Dictionary<RewardCurrencyType, float> source,
        HashSet<RewardCurrencyType> currencyScope,
        Dictionary<RewardCurrencyType, float> cache,
        HashSet<RewardCurrencyType> visiting)
    {
        if (cache.TryGetValue(currencyType, out float cached))
        {
            return cached;
        }

        float amount = GetSourceAmount(source, currencyType);
        if (!visiting.Add(currencyType))
        {
            return amount;
        }

        for (int i = 0; i < RelationList.Count; i++)
        {
            ItemRelation relation = RelationList[i];
            if (!relation.Decomposable || !currencyScope.Contains(relation.Type) || !ContainsDecomposeOutput(relation, currencyType))
            {
                continue;
            }

            float sourceAmount = CalculateVirtualAmount(relation.Type, source, currencyScope, cache, visiting);
            if (sourceAmount <= 0f)
            {
                continue;
            }

            amount += sourceAmount * CalculateExpectedDecomposeAmount(relation, currencyType);
        }

        if (Relations.TryGetValue(currencyType, out ItemRelation craftRelation)
            && craftRelation.Createable
            && craftRelation.CountPerCraft > 0
            && craftRelation.CraftInputs.Count > 0)
        {
            int craftCount = int.MaxValue;
            for (int i = 0; i < craftRelation.CraftInputs.Count; i++)
            {
                ItemAmount input = craftRelation.CraftInputs[i];
                if (input.Count <= 0)
                {
                    continue;
                }

                float inputAmount = CalculateVirtualAmount(input.Type, source, currencyScope, cache, visiting);
                craftCount = Mathf.Min(craftCount, Mathf.FloorToInt(Mathf.Max(0f, inputAmount) / input.Count));
            }

            if (craftCount != int.MaxValue && craftCount > 0)
            {
                amount += craftCount * craftRelation.CountPerCraft;
            }
        }

        visiting.Remove(currencyType);
        cache[currencyType] = amount;
        return amount;
    }

    // 원본 재화 표에서 지정 재화 수량을 가져온다
    private static float GetSourceAmount(Dictionary<RewardCurrencyType, float> source, RewardCurrencyType currencyType)
    {
        return source != null && source.TryGetValue(currencyType, out float amount) ? Mathf.Max(0f, amount) : 0f;
    }

    // 분해 결과에 대상 재화가 있는지 확인한다
    private static bool ContainsDecomposeOutput(ItemRelation relation, RewardCurrencyType targetType)
    {
        for (int i = 0; i < relation.DecomposeOutputs.Count; i++)
        {
            if (relation.DecomposeOutputs[i].Type == targetType)
            {
                return true;
            }
        }

        return false;
    }

    // 지정 재화에 대한 분해 기대 수량을 계산한다
    private static float CalculateExpectedDecomposeAmount(ItemRelation relation, RewardCurrencyType targetType)
    {
        float amount = 0f;
        for (int i = 0; i < relation.DecomposeOutputs.Count; i++)
        {
            DecomposeAmount output = relation.DecomposeOutputs[i];
            if (output.Type == targetType)
            {
                amount += (Mathf.Max(0, output.Min) + Mathf.Max(0, output.Max)) * 0.5f;
            }
        }

        return amount;
    }

    // ItemData CSV 관계 데이터를 한 번 로드한다
    private static void EnsureRelationsLoaded()
    {
        string relationSignature = BuildRelationSignature();
        if (hasLoadedRelations && string.Equals(loadedRelationSignature, relationSignature, StringComparison.Ordinal))
        {
            return;
        }

        Relations.Clear();
        RelationList.Clear();
        loadedRelationSignature = relationSignature;
        if (!File.Exists(ITEM_DATA_CSV_PATH))
        {
            Debug.LogWarning($"아이템 데이터 CSV를 찾을 수 없어 누적재화 조합/분해 관계를 반영하지 않습니다. 경로: {ITEM_DATA_CSV_PATH}");
            hasLoadedRelations = true;
            return;
        }

        string csvText = File.ReadAllText(ITEM_DATA_CSV_PATH, Encoding.UTF8);
        List<string[]> rows = ParseCsv(csvText);
        if (rows.Count <= 1)
        {
            hasLoadedRelations = true;
            return;
        }

        string[] header = rows[0];
        int typeIndex = FindColumnIndex(header, TYPE_COLUMN);
        int createableIndex = FindColumnIndex(header, CREATEABLE_COLUMN);
        int countPerCraftIndex = FindColumnIndex(header, COUNT_PER_CRAFT_COLUMN);
        int craftIndex = FindColumnIndex(header, CRAFT_COLUMN);
        int decomposableIndex = FindColumnIndex(header, DECOMPOSABLE_COLUMN);
        int decomposeIndex = FindColumnIndex(header, DECOMPOSE_COLUMN);
        for (int i = 1; i < rows.Count; i++)
        {
            if (!TryParseRelation(rows[i], typeIndex, createableIndex, countPerCraftIndex, craftIndex, decomposableIndex, decomposeIndex, out ItemRelation relation))
            {
                continue;
            }

            Relations[relation.Type] = relation;
            RelationList.Add(relation);
        }

        hasLoadedRelations = true;
    }

    // 아이템 CSV 캐시 갱신 여부를 판단할 서명을 만든다
    private static string BuildRelationSignature()
    {
        if (!File.Exists(ITEM_DATA_CSV_PATH))
        {
            return "missing";
        }

        FileInfo fileInfo = new FileInfo(ITEM_DATA_CSV_PATH);
        return fileInfo.LastWriteTimeUtc.Ticks.ToString() + "|" + fileInfo.Length.ToString();
    }

    // CSV 행을 재화 관계 데이터로 변환한다
    private static bool TryParseRelation(
        string[] row,
        int typeIndex,
        int createableIndex,
        int countPerCraftIndex,
        int craftIndex,
        int decomposableIndex,
        int decomposeIndex,
        out ItemRelation relation)
    {
        relation = new ItemRelation();
        if (!TryGetCell(row, typeIndex, out string typeText) || !TryParseCurrencyType(typeText, out RewardCurrencyType type))
        {
            return false;
        }

        relation.Type = type;
        relation.Createable = TryGetCell(row, createableIndex, out string createableText) && bool.TryParse(createableText, out bool createable) && createable;
        relation.CountPerCraft = TryGetCell(row, countPerCraftIndex, out string countText) && int.TryParse(countText, out int countPerCraft)
            ? Mathf.Max(0, countPerCraft)
            : 0;
        relation.Decomposable = TryGetCell(row, decomposableIndex, out string decomposableText) && bool.TryParse(decomposableText, out bool decomposable) && decomposable;
        relation.CraftInputs = new List<ItemAmount>();
        relation.DecomposeOutputs = new List<DecomposeAmount>();

        if (TryGetCell(row, craftIndex, out string craftText))
        {
            ParseCraftItems(craftText, relation.CraftInputs);
        }

        if (TryGetCell(row, decomposeIndex, out string decomposeText))
        {
            ParseDecomposeItems(decomposeText, relation.DecomposeOutputs);
        }

        return true;
    }

    // CSV 헤더에서 지정 컬럼의 인덱스를 찾는다
    private static int FindColumnIndex(string[] header, string columnName)
    {
        if (header == null)
        {
            return -1;
        }

        for (int i = 0; i < header.Length; i++)
        {
            string normalized = NormalizeColumnName(header[i]);
            if (string.Equals(normalized, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    // 컬럼명에서 타입 설명을 제거한다
    private static string NormalizeColumnName(string columnName)
    {
        string safeName = columnName == null ? string.Empty : columnName.Trim();
        int genericIndex = safeName.IndexOf('<');
        return genericIndex >= 0 ? safeName.Substring(0, genericIndex) : safeName;
    }

    // 행에서 지정 셀 문자열을 가져온다
    private static bool TryGetCell(string[] row, int index, out string value)
    {
        value = string.Empty;
        if (row == null || index < 0 || index >= row.Length)
        {
            return false;
        }

        value = row[index] == null ? string.Empty : row[index].Trim();
        return true;
    }

    // 제작 재료 문자열을 목록으로 변환한다
    private static void ParseCraftItems(string craftText, List<ItemAmount> craftItems)
    {
        if (string.IsNullOrWhiteSpace(craftText) || craftItems == null)
        {
            return;
        }

        string[] entries = craftText.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i].Trim();
            if (string.IsNullOrEmpty(entry))
            {
                continue;
            }

            string[] parts = entry.Split(':');
            if (parts.Length != 2 || !TryParseCurrencyType(parts[0], out RewardCurrencyType type) || !int.TryParse(parts[1].Trim(), out int count) || count <= 0)
            {
                continue;
            }

            craftItems.Add(new ItemAmount { Type = type, Count = count });
        }
    }

    // 분해 결과 문자열을 목록으로 변환한다
    private static void ParseDecomposeItems(string decomposeText, List<DecomposeAmount> decomposeItems)
    {
        if (string.IsNullOrWhiteSpace(decomposeText) || decomposeItems == null)
        {
            return;
        }

        string[] entries = decomposeText.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i].Trim();
            if (string.IsNullOrEmpty(entry))
            {
                continue;
            }

            string[] parts = entry.Split(':');
            if (parts.Length != 2 || !TryParseCurrencyType(parts[0], out RewardCurrencyType type))
            {
                continue;
            }

            string[] rangeParts = parts[1].Trim().Split('~');
            if (rangeParts.Length != 2 || !int.TryParse(rangeParts[0].Trim(), out int min) || !int.TryParse(rangeParts[1].Trim(), out int max))
            {
                continue;
            }

            decomposeItems.Add(new DecomposeAmount { Type = type, Min = Mathf.Max(0, min), Max = Mathf.Max(Mathf.Max(0, min), max) });
        }
    }

    // CSV의 enum 셀을 RewardCurrencyType으로 변환한다
    private static bool TryParseCurrencyType(string text, out RewardCurrencyType currencyType)
    {
        currencyType = default;
        string normalized = text == null ? string.Empty : text.Trim();
        int valueSeparatorIndex = normalized.IndexOf('=');
        if (valueSeparatorIndex >= 0)
        {
            normalized = normalized.Substring(0, valueSeparatorIndex).Trim();
        }

        return Enum.TryParse(normalized, true, out currencyType);
    }

    // 쉼표와 따옴표를 포함하는 CSV 텍스트를 행/열 목록으로 파싱한다
    private static List<string[]> ParseCsv(string csvText)
    {
        List<string[]> rows = new List<string[]>();
        List<string> currentRow = new List<string>();
        StringBuilder cell = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < csvText.Length; i++)
        {
            char c = csvText[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < csvText.Length && csvText[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (c == ',' && !inQuotes)
            {
                currentRow.Add(cell.ToString());
                cell.Length = 0;
                continue;
            }

            if ((c == '\n' || c == '\r') && !inQuotes)
            {
                if (c == '\r' && i + 1 < csvText.Length && csvText[i + 1] == '\n')
                {
                    i++;
                }

                currentRow.Add(cell.ToString());
                rows.Add(currentRow.ToArray());
                currentRow.Clear();
                cell.Length = 0;
                continue;
            }

            cell.Append(c);
        }

        if (cell.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(cell.ToString());
            rows.Add(currentRow.ToArray());
        }

        return rows;
    }

    // 조합 입력 수량을 보관한다.
    private struct ItemAmount
    {
        public RewardCurrencyType Type;
        public int Count;
    }

    // 분해 출력 수량 범위를 보관한다.
    private struct DecomposeAmount
    {
        public RewardCurrencyType Type;
        public int Min;
        public int Max;
    }

    // 한 아이템의 조합/분해 관계를 보관한다.
    private struct ItemRelation
    {
        public RewardCurrencyType Type;
        public bool Createable;
        public int CountPerCraft;
        public bool Decomposable;
        public List<ItemAmount> CraftInputs;
        public List<DecomposeAmount> DecomposeOutputs;
    }
}
