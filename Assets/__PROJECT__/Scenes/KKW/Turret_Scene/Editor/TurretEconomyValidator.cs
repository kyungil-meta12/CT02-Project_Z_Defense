using System.Text;
using UnityEditor;
using UnityEngine;

// 터렛 경제 데이터의 누락 및 잘못된 비용 값을 에디터에서 일괄 검증한다
internal static class TurretEconomyValidator
{
    private const string MENU_PATH = "Project Z Defense/Validation/Validate Turret Economy";
    private const string BASE_COSTS_PROPERTY = "baseCostsPerLevel";
    private const string EVOLUTION_COSTS_PROPERTY = "evolutionCosts";
    private const string COST_AMOUNT_PROPERTY = "amount";
    private const string PLACEMENT_COSTS_PROPERTY = "placementCosts";
    private const string PLACEMENT_COST_TIERS_PROPERTY = "placementCostTiers";
    private const string TIER_COSTS_PROPERTY = "costs";
    private const string TARGET_DEFINITION_PROPERTY = "targetDefinition";
    private const string EVOLUTION_ENTRIES_PROPERTY = "evolutionEntries";
    private const string BASE_COST_AMOUNT_PROPERTY = "amount";
    private const string ADDITIONAL_COST_PERCENT_PROPERTY = "additionalCostPercentPerTierLevel";
    private const float COST_PERCENT_TOLERANCE = 0.001f;

    // 터렛 경제 데이터 전체를 검사하고 콘솔에 결과를 출력한다
    [MenuItem(MENU_PATH)]
    private static void ValidateTurretEconomy()
    {
        ValidationStats stats = new ValidationStats();

        ValidateTurretDefinitions(stats);
        ValidateUpgradeCostProfiles(stats);
        ValidateEvolutionProgressions(stats);
        ValidateTurretShopEntries(stats);
        LogSummary(stats);
    }

    // 모든 터렛 정의 에셋의 업그레이드 비용 프로필 연결 여부를 검사한다
    private static void ValidateTurretDefinitions(ValidationStats stats)
    {
        string[] guids = AssetDatabase.FindAssets("t:TurretDefinitionSO");
        stats.TurretDefinitionCount = guids.Length;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TurretDefinitionSO definition = AssetDatabase.LoadAssetAtPath<TurretDefinitionSO>(path);
            if (definition == null)
            {
                continue;
            }

            if (definition.upgradeCostProfile == null)
            {
                stats.MissingUpgradeCostProfileCount++;
                LogIssue("업그레이드 비용 프로필이 연결되지 않은 터렛 정의입니다.", path, definition);
            }

            if (definition.maxLevel > 0 && definition.evolutionProgressionProfile != null)
            {
                stats.MaxLevelEvolutionConflictCount++;
                LogIssue("maxLevel과 evolutionProgressionProfile이 함께 설정되어 있습니다. 진화 대기 레벨은 Evolution Progression SO에서 관리하고, maxLevel은 최종 터렛에만 사용해야 합니다.", path, definition);
            }

            ValidateSelfTargetEvolution(definition, path, stats);
            ValidateDefinitionEvolutionCostRamp(definition, path, stats);
            ValidateDefinitionCostRamp(definition, path, stats);
            ValidateFrostStatusProfile(definition, path, stats);
        }
    }

    // Frost 터렛 정의에 Frost 상태 프로필이 연결되어 있는지 검사한다
    private static void ValidateFrostStatusProfile(TurretDefinitionSO definition, string path, ValidationStats stats)
    {
        if (!IsFrostTurretDefinition(definition))
        {
            return;
        }

        if (definition.frostStatusProfile != null)
        {
            return;
        }

        stats.MissingFrostStatusProfileCount++;
        LogIssue("Frost 터렛 정의에 frostStatusProfile이 연결되지 않았습니다. Frost 슬로우/빙결/폭발 효과가 적용되지 않습니다.", path, definition);
    }

    // 터렛 정의가 Frost 터렛인지 안정적인 ID와 표시명으로 확인한다
    private static bool IsFrostTurretDefinition(TurretDefinitionSO definition)
    {
        if (definition == null)
        {
            return false;
        }

        if (definition.turretId == "frost_turret")
        {
            return true;
        }

        return definition.displayName == "Frost_Turret";
    }

    // 터렛 정의의 진화 엔트리가 자기 자신을 목표로 가리키는지 검사한다
    private static void ValidateSelfTargetEvolution(TurretDefinitionSO definition, string path, ValidationStats stats)
    {
        TurretEvolutionProgressionSO progression = definition.evolutionProgressionProfile;
        if (progression == null || progression.evolutionEntries == null)
        {
            return;
        }

        for (int i = 0; i < progression.evolutionEntries.Length; i++)
        {
            TurretEvolutionEntry entry = progression.evolutionEntries[i];
            if (entry == null || entry.targetDefinition == null || entry.targetDefinition != definition)
            {
                continue;
            }

            stats.SelfTargetEvolutionCount++;
            LogIssue($"진화 엔트리 {i}번이 자기 자신을 Target Definition으로 가리킵니다.", path, definition);
        }
    }

    // 터렛 정의의 진화 비용이 현재 경제 단계와 일치하는지 검사한다
    private static void ValidateDefinitionEvolutionCostRamp(TurretDefinitionSO definition, string path, ValidationStats stats)
    {
        TurretEvolutionProgressionSO progression = definition.evolutionProgressionProfile;
        if (progression == null || progression.evolutionEntries == null)
        {
            return;
        }

        for (int i = 0; i < progression.evolutionEntries.Length; i++)
        {
            TurretEvolutionEntry entry = progression.evolutionEntries[i];
            if (entry == null || entry.targetDefinition == null)
            {
                continue;
            }

            if (!TryGetExpectedEvolutionCoin(definition, entry.targetDefinition, out int expectedCoin))
            {
                continue;
            }

            if (!TryGetCoinAmount(entry.evolutionCosts, out int actualCoin))
            {
                continue;
            }

            if (actualCoin == expectedCoin)
            {
                continue;
            }

            stats.EvolutionCostRampMismatchCount++;
            LogIssue($"진화 엔트리 {i}번의 비용 램프가 예상값과 다릅니다. {definition.displayName} -> {entry.targetDefinition.displayName}, 예상 Coin {expectedCoin}, 현재 Coin {actualCoin}", path, definition);
        }
    }

    // 진화 경로 이름 규칙으로 기대 진화 비용을 계산한다
    private static bool TryGetExpectedEvolutionCoin(TurretDefinitionSO sourceDefinition, TurretDefinitionSO targetDefinition, out int expectedCoin)
    {
        expectedCoin = 0;
        string sourceName = sourceDefinition == null ? string.Empty : sourceDefinition.displayName;
        string targetName = targetDefinition == null ? string.Empty : targetDefinition.displayName;
        if (string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(targetName))
        {
            return false;
        }

        if (sourceName == "Sentinel-01" && (targetName == "Sentry Pulse" || targetName == "Vector MG"))
        {
            expectedCoin = 10000;
            return true;
        }

        if ((sourceName == "Sentry Pulse" && targetName == "Pulse Repeater") ||
            (sourceName == "Vector MG" && targetName == "Vulcan Node"))
        {
            expectedCoin = 60000;
            return true;
        }

        if ((sourceName == "Pulse Repeater" || sourceName == "Vulcan Node") && targetName.EndsWith("_1", System.StringComparison.Ordinal))
        {
            expectedCoin = 180000;
            return true;
        }

        if (sourceName.EndsWith("_1", System.StringComparison.Ordinal) && targetName.EndsWith("_2", System.StringComparison.Ordinal))
        {
            expectedCoin = 300000;
            return true;
        }

        if (sourceName.EndsWith("_2", System.StringComparison.Ordinal) && targetName.EndsWith("_3", System.StringComparison.Ordinal))
        {
            expectedCoin = 450000;
            return true;
        }

        return false;
    }

    // 비용 배열에서 Coin 비용 합계를 읽는다
    private static bool TryGetCoinAmount(ResourceCost[] costs, out int coinAmount)
    {
        coinAmount = 0;
        if (costs == null)
        {
            return false;
        }

        bool hasCoinCost = false;
        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null || cost.currencyType != RewardCurrencyType.Coin)
            {
                continue;
            }

            hasCoinCost = true;
            coinAmount += Mathf.Max(0, cost.amount);
        }

        return hasCoinCost;
    }

    // 터렛 정의 이름과 연결된 업그레이드 비용 프로필의 경제 램프가 일치하는지 검사한다
    private static void ValidateDefinitionCostRamp(TurretDefinitionSO definition, string path, ValidationStats stats)
    {
        if (definition.upgradeCostProfile == null)
        {
            return;
        }

        if (!TryGetExpectedCostRamp(definition, out int expectedBaseCoin, out float expectedPercent))
        {
            return;
        }

        if (!TryGetCostProfileValues(definition.upgradeCostProfile, out RewardCurrencyType currencyType, out int baseAmount, out float percent))
        {
            return;
        }

        if (currencyType != RewardCurrencyType.Coin || baseAmount != expectedBaseCoin || Mathf.Abs(percent - expectedPercent) > COST_PERCENT_TOLERANCE)
        {
            stats.CostRampMismatchCount++;
            LogIssue($"업그레이드 비용 램프가 예상값과 다릅니다. 예상: Coin {expectedBaseCoin}, {expectedPercent:0.###}% / 현재: {currencyType} {baseAmount}, {percent:0.###}%", path, definition);
        }
    }

    // 터렛 정의의 이름 규칙으로 기대 비용 램프를 계산한다
    private static bool TryGetExpectedCostRamp(TurretDefinitionSO definition, out int expectedBaseCoin, out float expectedPercent)
    {
        expectedBaseCoin = 0;
        expectedPercent = 0.0f;

        string displayName = definition == null ? string.Empty : definition.displayName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        if (displayName == "Sentinel-01")
        {
            expectedBaseCoin = 233;
            expectedPercent = 1.0f;
            return true;
        }

        if (displayName == "Sentry Pulse" || displayName == "Vector MG")
        {
            expectedBaseCoin = 350;
            expectedPercent = 2.0f;
            return true;
        }

        if (displayName == "Pulse Repeater" || displayName == "Vulcan Node")
        {
            expectedBaseCoin = 640;
            expectedPercent = 3.0f;
            return true;
        }

        if (displayName.EndsWith("_1", System.StringComparison.Ordinal))
        {
            expectedBaseCoin = 3200;
            expectedPercent = 3.0f;
            return true;
        }

        if (displayName.EndsWith("_2", System.StringComparison.Ordinal))
        {
            expectedBaseCoin = 5667;
            expectedPercent = 4.0f;
            return true;
        }

        if (displayName.EndsWith("_3", System.StringComparison.Ordinal))
        {
            expectedBaseCoin = 10571;
            expectedPercent = 5.0f;
            return true;
        }

        return false;
    }

    // 업그레이드 비용 프로필의 첫 번째 기본 비용과 추가 퍼센트를 읽는다
    private static bool TryGetCostProfileValues(TurretUpgradeCostProfileSO profile, out RewardCurrencyType currencyType, out int baseAmount, out float percent)
    {
        currencyType = RewardCurrencyType.Coin;
        baseAmount = 0;
        percent = 0.0f;

        SerializedObject serializedProfile = new SerializedObject(profile);
        SerializedProperty baseCosts = serializedProfile.FindProperty(BASE_COSTS_PROPERTY);
        SerializedProperty additionalPercent = serializedProfile.FindProperty(ADDITIONAL_COST_PERCENT_PROPERTY);
        if (baseCosts == null || !baseCosts.isArray || baseCosts.arraySize == 0 || additionalPercent == null)
        {
            return false;
        }

        SerializedProperty firstCost = baseCosts.GetArrayElementAtIndex(0);
        if (firstCost == null)
        {
            return false;
        }

        SerializedProperty amount = firstCost.FindPropertyRelative(BASE_COST_AMOUNT_PROPERTY);
        SerializedProperty currency = firstCost.FindPropertyRelative("currencyType");
        if (amount == null || currency == null)
        {
            return false;
        }

        currencyType = (RewardCurrencyType)currency.enumValueIndex;
        baseAmount = amount.intValue;
        percent = additionalPercent.floatValue;
        return true;
    }

    // 모든 터렛 배치 엔트리의 배치 비용 설정 여부와 음수 비용을 검사한다
    private static void ValidateTurretShopEntries(ValidationStats stats)
    {
        string[] guids = AssetDatabase.FindAssets("t:TurretShopEntrySO");
        stats.TurretShopEntryCount = guids.Length;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TurretShopEntrySO shopEntry = AssetDatabase.LoadAssetAtPath<TurretShopEntrySO>(path);
            if (shopEntry == null)
            {
                continue;
            }

            SerializedObject serializedShopEntry = new SerializedObject(shopEntry);
            SerializedProperty placementCosts = serializedShopEntry.FindProperty(PLACEMENT_COSTS_PROPERTY);
            bool hasPlacementCosts = HasPayableCosts(placementCosts);

            if (!hasPlacementCosts)
            {
                stats.MissingPlacementCostCount++;
                LogIssue("터렛 배치 엔트리의 Placement Costs가 비어 있습니다.", path, shopEntry);
                continue;
            }

            if (placementCosts != null && placementCosts.isArray)
            {
                ValidateCostArray(placementCosts, path, shopEntry, stats, "터렛 배치 비용");
            }

            ValidatePlacementCostTiers(serializedShopEntry, path, shopEntry, stats);
        }
    }

    // 터렛 배치 엔트리의 설치 횟수별 비용 단계 배열을 검사한다
    private static void ValidatePlacementCostTiers(SerializedObject serializedShopEntry, string path, Object context, ValidationStats stats)
    {
        SerializedProperty tiers = serializedShopEntry.FindProperty(PLACEMENT_COST_TIERS_PROPERTY);
        if (tiers == null || !tiers.isArray)
        {
            return;
        }

        for (int tierIndex = 0; tierIndex < tiers.arraySize; tierIndex++)
        {
            SerializedProperty tier = tiers.GetArrayElementAtIndex(tierIndex);
            if (tier == null)
            {
                continue;
            }

            SerializedProperty costs = tier.FindPropertyRelative(TIER_COSTS_PROPERTY);
            if (costs == null || !costs.isArray)
            {
                continue;
            }

            ValidateCostArray(costs, path, context, stats, $"터렛 배치 비용 단계 {tierIndex}번");
        }
    }

    // 모든 터렛 업그레이드 비용 프로필의 빈 비용 배열과 음수 비용을 검사한다
    private static void ValidateUpgradeCostProfiles(ValidationStats stats)
    {
        string[] guids = AssetDatabase.FindAssets("t:TurretUpgradeCostProfileSO");
        stats.UpgradeCostProfileCount = guids.Length;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TurretUpgradeCostProfileSO profile = AssetDatabase.LoadAssetAtPath<TurretUpgradeCostProfileSO>(path);
            if (profile == null)
            {
                continue;
            }

            SerializedObject serializedProfile = new SerializedObject(profile);
            SerializedProperty baseCosts = serializedProfile.FindProperty(BASE_COSTS_PROPERTY);
            if (baseCosts == null || !baseCosts.isArray || baseCosts.arraySize == 0)
            {
                stats.EmptyUpgradeCostProfileCount++;
                LogIssue("업그레이드 비용 프로필의 Base Costs Per Level이 비어 있습니다.", path, profile);
                continue;
            }

            ValidateCostArray(baseCosts, path, profile, stats, "업그레이드 비용 프로필");
        }
    }

    // 모든 진화 진행 에셋의 진화 비용 설정 여부와 음수 비용을 검사한다
    private static void ValidateEvolutionProgressions(ValidationStats stats)
    {
        string[] guids = AssetDatabase.FindAssets("t:TurretEvolutionProgressionSO");
        stats.EvolutionProgressionCount = guids.Length;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TurretEvolutionProgressionSO progression = AssetDatabase.LoadAssetAtPath<TurretEvolutionProgressionSO>(path);
            if (progression == null)
            {
                continue;
            }

            SerializedObject serializedProgression = new SerializedObject(progression);
            SerializedProperty entries = serializedProgression.FindProperty(EVOLUTION_ENTRIES_PROPERTY);
            if (entries == null || !entries.isArray)
            {
                continue;
            }

            for (int entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(entryIndex);
                ValidateEvolutionEntry(entry, entryIndex, path, progression, stats);
            }
        }
    }

    // 단일 진화 엔트리의 목표 정의와 비용 배열을 검사한다
    private static void ValidateEvolutionEntry(SerializedProperty entry, int entryIndex, string path, Object context, ValidationStats stats)
    {
        if (entry == null)
        {
            return;
        }

        SerializedProperty targetDefinition = entry.FindPropertyRelative(TARGET_DEFINITION_PROPERTY);
        SerializedProperty evolutionCosts = entry.FindPropertyRelative(EVOLUTION_COSTS_PROPERTY);
        bool hasTargetDefinition = targetDefinition != null && targetDefinition.objectReferenceValue != null;

        if (!hasTargetDefinition)
        {
            return;
        }

        if (evolutionCosts == null || !evolutionCosts.isArray || evolutionCosts.arraySize == 0)
        {
            stats.MissingEvolutionCostCount++;
            LogIssue($"진화 엔트리 {entryIndex}번의 Evolution Costs가 비어 있습니다.", path, context);
            return;
        }

        ValidateCostArray(evolutionCosts, path, context, stats, $"진화 엔트리 {entryIndex}번 비용");
    }

    // ResourceCost 배열 내부의 음수 비용을 검사한다
    private static void ValidateCostArray(SerializedProperty costs, string path, Object context, ValidationStats stats, string label)
    {
        for (int costIndex = 0; costIndex < costs.arraySize; costIndex++)
        {
            SerializedProperty cost = costs.GetArrayElementAtIndex(costIndex);
            if (cost == null)
            {
                continue;
            }

            SerializedProperty amount = cost.FindPropertyRelative(COST_AMOUNT_PROPERTY);
            if (amount == null || amount.intValue >= 0)
            {
                continue;
            }

            stats.NegativeCostCount++;
            LogIssue($"{label}의 {costIndex}번 비용이 음수입니다. amount={amount.intValue}", path, context);
        }
    }

    // ResourceCost 배열에 실제 지불할 비용 항목이 있는지 확인한다
    private static bool HasPayableCosts(SerializedProperty costs)
    {
        if (costs == null || !costs.isArray)
        {
            return false;
        }

        for (int i = 0; i < costs.arraySize; i++)
        {
            SerializedProperty cost = costs.GetArrayElementAtIndex(i);
            SerializedProperty amount = cost == null ? null : cost.FindPropertyRelative(COST_AMOUNT_PROPERTY);
            if (amount != null && amount.intValue > 0)
            {
                return true;
            }
        }

        return false;
    }

    // 검증 문제를 에셋 컨텍스트와 함께 경고 로그로 출력한다
    private static void LogIssue(string message, string path, Object context)
    {
        Debug.LogWarning($"[터렛 경제 검증] {message}\n경로: {path}", context);
    }

    // 검증 결과 요약을 콘솔에 출력한다
    private static void LogSummary(ValidationStats stats)
    {
        int issueCount = stats.TotalIssueCount;
        StringBuilder builder = new StringBuilder(256);
        builder.AppendLine("[터렛 경제 검증] 완료");
        builder.AppendLine($"터렛 정의: {stats.TurretDefinitionCount}개");
        builder.AppendLine($"업그레이드 비용 프로필: {stats.UpgradeCostProfileCount}개");
        builder.AppendLine($"진화 진행 프로필: {stats.EvolutionProgressionCount}개");
        builder.AppendLine($"터렛 배치 엔트리: {stats.TurretShopEntryCount}개");
        builder.AppendLine($"누락된 upgradeCostProfile: {stats.MissingUpgradeCostProfileCount}개");
        builder.AppendLine($"비어 있는 baseCostsPerLevel: {stats.EmptyUpgradeCostProfileCount}개");
        builder.AppendLine($"미설정 evolutionCosts: {stats.MissingEvolutionCostCount}개");
        builder.AppendLine($"미설정 placementCosts: {stats.MissingPlacementCostCount}개");
        builder.AppendLine($"음수 비용: {stats.NegativeCostCount}개");
        builder.AppendLine($"비용 램프 불일치: {stats.CostRampMismatchCount}개");
        builder.AppendLine($"진화 비용 램프 불일치: {stats.EvolutionCostRampMismatchCount}개");
        builder.AppendLine($"maxLevel/진화 프로필 충돌: {stats.MaxLevelEvolutionConflictCount}개");
        builder.AppendLine($"자기 자신 진화 Target: {stats.SelfTargetEvolutionCount}개");
        builder.AppendLine($"누락된 Frost 상태 프로필: {stats.MissingFrostStatusProfileCount}개");

        if (issueCount > 0)
        {
            Debug.LogWarning(builder.ToString());
            return;
        }

        Debug.Log(builder.ToString());
    }

    // 검증 중 집계한 에셋 수와 문제 수를 보관한다
    private sealed class ValidationStats
    {
        public int TurretDefinitionCount;
        public int UpgradeCostProfileCount;
        public int EvolutionProgressionCount;
        public int TurretShopEntryCount;
        public int MissingUpgradeCostProfileCount;
        public int EmptyUpgradeCostProfileCount;
        public int MissingEvolutionCostCount;
        public int MissingPlacementCostCount;
        public int NegativeCostCount;
        public int CostRampMismatchCount;
        public int EvolutionCostRampMismatchCount;
        public int MaxLevelEvolutionConflictCount;
        public int SelfTargetEvolutionCount;
        public int MissingFrostStatusProfileCount;

        public int TotalIssueCount => MissingUpgradeCostProfileCount + EmptyUpgradeCostProfileCount + MissingEvolutionCostCount + MissingPlacementCostCount + NegativeCostCount + CostRampMismatchCount + EvolutionCostRampMismatchCount + MaxLevelEvolutionConflictCount + SelfTargetEvolutionCount + MissingFrostStatusProfileCount;
    }
}
