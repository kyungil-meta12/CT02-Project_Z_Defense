using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 터렛 전투력, 비용, 보상 수급을 에디터에서 표로 확인하고 CSV로 내보내는 리포트 창.
internal sealed class TurretBalanceReportWindow : EditorWindow
{
    private const string MENU_PATH = "Tools/터렛 밸런스 리포트";
    private const int REPORT_LEVEL_MIN = 1;
    private const int REPORT_LEVEL_MAX = 100;
    private const string STAGES_PROPERTY = "stages";
    private const string NORMAL_ZOMBIE_ENTRIES_PROPERTY = "normalZombieEntries";
    private const string PREFAB_PROPERTY = "prefab";
    private const string WEIGHT_PROPERTY = "weight";
    private const string REWARD_PROFILE_OVERRIDE_PROPERTY = "rewardProfileOverride";
    private const string REWARDS_PROPERTY = "rewards";
    private const string CURRENCY_TYPE_PROPERTY = "currencyType";
    private const string AMOUNT_PROPERTY = "amount";
    private const string DROP_CHANCE_PROPERTY = "dropChance";
    private const string MIN_AMOUNT_MULTIPLIER_PROPERTY = "minAmountMultiplier";
    private const string MAX_AMOUNT_MULTIPLIER_PROPERTY = "maxAmountMultiplier";

    private readonly List<TurretDpsRow> turretDpsRows = new List<TurretDpsRow>();
    private readonly List<UpgradeCostRow> upgradeCostRows = new List<UpgradeCostRow>();
    private readonly List<EvolutionCostRow> evolutionCostRows = new List<EvolutionCostRow>();
    private readonly List<WaveRewardRow> waveRewardRows = new List<WaveRewardRow>();
    private readonly List<AffordabilityRow> affordabilityRows = new List<AffordabilityRow>();

    private Vector2 scrollPosition;
    private int selectedTab;
    private string lastRefreshLabel;

    // 터렛 밸런스 리포트 창을 연다
    [MenuItem(MENU_PATH)]
    private static void OpenWindow()
    {
        TurretBalanceReportWindow window = GetWindow<TurretBalanceReportWindow>("터렛 밸런스");
        window.minSize = new Vector2(980.0f, 520.0f);
        window.RefreshReport();
        window.Show();
    }

    // 열려 있는 터렛 밸런스 리포트 창을 모두 새로고침한다
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

    // 창이 활성화될 때 리포트 데이터를 갱신한다
    private void OnEnable()
    {
        RefreshReport();
    }

    // 에디터 창의 IMGUI를 그린다
    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.Space(6.0f);

        selectedTab = GUILayout.Toolbar(selectedTab, new[] { "터렛 DPS", "업그레이드 비용", "진화 비용", "웨이브 보상", "구매 가능성" });
        EditorGUILayout.Space(6.0f);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        switch (selectedTab)
        {
            case 0:
                DrawTurretDpsTable();
                break;
            case 1:
                DrawUpgradeCostTable();
                break;
            case 2:
                DrawEvolutionCostTable();
                break;
            case 3:
                DrawWaveRewardTable();
                break;
            case 4:
                DrawAffordabilityTable();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    // 상단 도구 버튼과 갱신 상태를 그린다
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(80.0f)))
        {
            RefreshReport();
        }

        if (GUILayout.Button("CSV 내보내기", EditorStyles.toolbarButton, GUILayout.Width(100.0f)))
        {
            ExportCsvFiles();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField(lastRefreshLabel ?? "새로고침 전", EditorStyles.miniLabel, GUILayout.Width(260.0f));
        EditorGUILayout.EndHorizontal();
    }

    // 모든 리포트 데이터를 다시 계산한다
    private void RefreshReport()
    {
        turretDpsRows.Clear();
        upgradeCostRows.Clear();
        evolutionCostRows.Clear();
        waveRewardRows.Clear();
        affordabilityRows.Clear();

        BuildTurretReports();
        BuildWaveRewardReports();
        BuildAffordabilityReports();

        lastRefreshLabel = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        Repaint();
    }

    // 터렛 정의 에셋에서 DPS, 업그레이드, 진화 비용 리포트를 만든다
    private void BuildTurretReports()
    {
        string[] guids = AssetDatabase.FindAssets("t:TurretDefinitionSO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TurretDefinitionSO definition = AssetDatabase.LoadAssetAtPath<TurretDefinitionSO>(path);
            if (definition == null)
            {
                continue;
            }

            AddTurretDpsRow(definition);
            AddUpgradeCostRow(definition);
            AddEvolutionCostRows(definition);
        }

        turretDpsRows.Sort((left, right) => string.Compare(left.TurretName, right.TurretName, StringComparison.Ordinal));
        upgradeCostRows.Sort((left, right) => string.Compare(left.TurretName, right.TurretName, StringComparison.Ordinal));
        evolutionCostRows.Sort((left, right) =>
        {
            int sourceCompare = string.Compare(left.SourceName, right.SourceName, StringComparison.Ordinal);
            return sourceCompare != 0 ? sourceCompare : string.Compare(left.TargetName, right.TargetName, StringComparison.Ordinal);
        });
    }

    // 터렛 정의 하나의 Lv1/Lv100 DPS 리포트 행을 추가한다
    private void AddTurretDpsRow(TurretDefinitionSO definition)
    {
        TurretRuntimeStat levelOne = TurretStatCalculator.Calculate(definition.baseStatProfile, definition.statGrowthProfile, REPORT_LEVEL_MIN);
        TurretRuntimeStat levelMax = TurretStatCalculator.Calculate(definition.baseStatProfile, definition.statGrowthProfile, REPORT_LEVEL_MAX);

        turretDpsRows.Add(new TurretDpsRow
        {
            TurretName = GetTurretName(definition),
            LevelOneDamage = levelOne.damage,
            LevelOneInterval = levelOne.fireInterval,
            LevelOneProjectileCount = levelOne.projectileCount,
            LevelOnePierceCount = levelOne.pierceCount,
            LevelOneDps = CalculateDps(levelOne),
            LevelMaxDamage = levelMax.damage,
            LevelMaxInterval = levelMax.fireInterval,
            LevelMaxProjectileCount = levelMax.projectileCount,
            LevelMaxPierceCount = levelMax.pierceCount,
            LevelMaxDps = CalculateDps(levelMax)
        });
    }

    // 터렛 정의 하나의 업그레이드 비용 리포트 행을 추가한다
    private void AddUpgradeCostRow(TurretDefinitionSO definition)
    {
        if (definition.upgradeCostProfile == null)
        {
            upgradeCostRows.Add(new UpgradeCostRow { TurretName = GetTurretName(definition), Note = "Missing upgradeCostProfile" });
            return;
        }

        upgradeCostRows.Add(new UpgradeCostRow
        {
            TurretName = GetTurretName(definition),
            LevelOneToTwo = GetCoinCost(definition.upgradeCostProfile.GetCosts(1, 2)),
            LevelFiftyToFiftyOne = GetCoinCost(definition.upgradeCostProfile.GetCosts(50, 51)),
            LevelNinetyNineToOneHundred = GetCoinCost(definition.upgradeCostProfile.GetCosts(99, 100)),
            LevelOneToOneHundred = GetCoinCost(definition.upgradeCostProfile.GetCosts(1, 100)),
            Note = string.Empty
        });
    }

    // 터렛 정의 하나의 진화 비용 리포트 행들을 추가한다
    private void AddEvolutionCostRows(TurretDefinitionSO definition)
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

            evolutionCostRows.Add(new EvolutionCostRow
            {
                SourceName = GetTurretName(definition),
                RequiredLevel = Mathf.Max(1, entry.requiredLevel),
                TargetName = GetTurretName(entry.targetDefinition),
                CoinCost = GetCoinCost(entry.evolutionCosts),
                FirePartCost = GetCurrencyCost(entry.evolutionCosts, RewardCurrencyType.FirePart),
                SpecialPartCost = GetCurrencyCost(entry.evolutionCosts, RewardCurrencyType.SpecialPart)
            });
        }
    }

    // 웨이브 스폰 프로필에서 평균 보상 리포트를 만든다
    private void BuildWaveRewardReports()
    {
        string[] guids = AssetDatabase.FindAssets("t:ZombieWaveSpawnProfileSO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ZombieWaveSpawnProfileSO profile = AssetDatabase.LoadAssetAtPath<ZombieWaveSpawnProfileSO>(path);
            if (profile == null)
            {
                continue;
            }

            AddWaveRewardRows(profile, path);
        }

        waveRewardRows.Sort((left, right) => left.MinWave.CompareTo(right.MinWave));
    }

    // 단일 웨이브 스폰 프로필의 스테이지 행들을 추가한다
    private void AddWaveRewardRows(ZombieWaveSpawnProfileSO profile, string path)
    {
        SerializedObject serializedProfile = new SerializedObject(profile);
        SerializedProperty stages = serializedProfile.FindProperty(STAGES_PROPERTY);
        if (stages == null || !stages.isArray)
        {
            return;
        }

        for (int i = 0; i < stages.arraySize; i++)
        {
            SerializedProperty stage = stages.GetArrayElementAtIndex(i);
            if (stage == null)
            {
                continue;
            }

            waveRewardRows.Add(CreateWaveRewardRow(stage, path));
        }
    }

    // 스폰 스테이지 하나를 평균 보상 행으로 변환한다
    private WaveRewardRow CreateWaveRewardRow(SerializedProperty stage, string path)
    {
        int minWave = GetRelativeInt(stage, "minWave", 1);
        int maxWave = GetRelativeInt(stage, "maxWave", 0);
        int spawnCount = GetRelativeInt(stage, "spawnCount", 0);
        float rewardMultiplier = SanitizeRuntimeMultiplier(GetRelativeFloat(stage, "rewardMultiplier", 1.0f));
        SerializedProperty normalEntries = stage.FindPropertyRelative(NORMAL_ZOMBIE_ENTRIES_PROPERTY);

        WeightedRewardSummary summary = CalculateWeightedRewardSummary(normalEntries, rewardMultiplier);
        return new WaveRewardRow
        {
            ProfilePath = path,
            MinWave = minWave,
            MaxWave = maxWave,
            SpawnCount = spawnCount,
            RewardMultiplier = rewardMultiplier,
            AverageCoinPerKill = summary.AverageCoinPerKill,
            MinCoinPerKill = summary.MinCoinPerKill,
            MaxCoinPerKill = summary.MaxCoinPerKill,
            AverageCoinPerWave = summary.AverageCoinPerKill * spawnCount,
            MinCoinPerWave = summary.MinCoinPerKill * spawnCount,
            MaxCoinPerWave = summary.MaxCoinPerKill * spawnCount,
            CandidateCount = summary.CandidateCount
        };
    }

    // 스폰 후보 가중치를 반영한 보상 요약을 계산한다
    private WeightedRewardSummary CalculateWeightedRewardSummary(SerializedProperty entries, float rewardMultiplier)
    {
        WeightedRewardSummary summary = new WeightedRewardSummary();
        if (entries == null || !entries.isArray)
        {
            return summary;
        }

        int totalWeight = 0;
        float weightedCoin = 0.0f;
        int minCoin = int.MaxValue;
        int maxCoin = 0;

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (entry == null)
            {
                continue;
            }

            int weight = Mathf.Max(0, GetRelativeInt(entry, WEIGHT_PROPERTY, 0));
            if (weight <= 0)
            {
                continue;
            }

            UnityEngine.Object prefabReference = GetObjectReference<UnityEngine.Object>(entry, PREFAB_PROPERTY);
            if (!TryGetPrefabCoinReward(prefabReference, out CoinRewardData reward))
            {
                continue;
            }

            totalWeight += weight;
            summary.CandidateCount++;
            weightedCoin += reward.ExpectedAmount * rewardMultiplier * weight;
            minCoin = Mathf.Min(minCoin, Mathf.FloorToInt(reward.Amount * reward.DropChance * rewardMultiplier * reward.MinMultiplier));
            maxCoin = Mathf.Max(maxCoin, Mathf.FloorToInt(reward.Amount * reward.DropChance * rewardMultiplier * reward.MaxMultiplier));
        }

        if (totalWeight <= 0)
        {
            return summary;
        }

        summary.AverageCoinPerKill = weightedCoin / totalWeight;
        summary.MinCoinPerKill = minCoin == int.MaxValue ? 0 : minCoin;
        summary.MaxCoinPerKill = maxCoin;
        return summary;
    }

    // 프리팹에 연결된 보상 프로필에서 Coin 보상 데이터를 읽는다
    private bool TryGetPrefabCoinReward(UnityEngine.Object prefabReference, out CoinRewardData reward)
    {
        reward = default(CoinRewardData);
        GameObject prefab = GetPrefabGameObject(prefabReference);
        if (prefab == null)
        {
            return false;
        }

        NormalZombie normalZombie = prefab.GetComponent<NormalZombie>();
        if (normalZombie != null && TryGetRewardProfile(normalZombie, out ZombieRewardProfileSO normalRewardProfile))
        {
            return TryGetCoinReward(normalRewardProfile, out reward);
        }

        BossZombie bossZombie = prefab.GetComponent<BossZombie>();
        if (bossZombie == null)
        {
            return false;
        }

        if (TryGetRewardProfile(bossZombie, out ZombieRewardProfileSO bossRewardProfile))
        {
            return TryGetCoinReward(bossRewardProfile, out reward);
        }

        return bossZombie.spec != null && TryGetCoinReward(bossZombie.spec.RewardProfile, out reward);
    }

    // 스폰 엔트리의 프리팹 참조를 GameObject로 변환한다
    private static GameObject GetPrefabGameObject(UnityEngine.Object prefabReference)
    {
        if (prefabReference == null)
        {
            return null;
        }

        GameObject gameObject = prefabReference as GameObject;
        if (gameObject != null)
        {
            return gameObject;
        }

        Component component = prefabReference as Component;
        return component == null ? null : component.gameObject;
    }

    // 좀비 컴포넌트의 rewardProfileOverride 직렬화 값을 읽는다
    private bool TryGetRewardProfile(UnityEngine.Object zombieComponent, out ZombieRewardProfileSO rewardProfile)
    {
        rewardProfile = null;
        SerializedObject serializedZombie = new SerializedObject(zombieComponent);
        SerializedProperty rewardProfileProperty = serializedZombie.FindProperty(REWARD_PROFILE_OVERRIDE_PROPERTY);
        if (rewardProfileProperty == null)
        {
            return false;
        }

        rewardProfile = rewardProfileProperty.objectReferenceValue as ZombieRewardProfileSO;
        return rewardProfile != null;
    }

    // 보상 프로필에서 첫 번째 Coin 보상 엔트리를 읽는다
    private bool TryGetCoinReward(ZombieRewardProfileSO rewardProfile, out CoinRewardData reward)
    {
        reward = default(CoinRewardData);
        if (rewardProfile == null)
        {
            return false;
        }

        SerializedObject serializedProfile = new SerializedObject(rewardProfile);
        SerializedProperty rewards = serializedProfile.FindProperty(REWARDS_PROPERTY);
        if (rewards == null || !rewards.isArray)
        {
            return false;
        }

        for (int i = 0; i < rewards.arraySize; i++)
        {
            SerializedProperty entry = rewards.GetArrayElementAtIndex(i);
            if (entry == null)
            {
                continue;
            }

            SerializedProperty currency = entry.FindPropertyRelative(CURRENCY_TYPE_PROPERTY);
            if (currency == null || currency.enumValueIndex != (int)RewardCurrencyType.Coin)
            {
                continue;
            }

            reward = new CoinRewardData
            {
                Amount = Mathf.Max(0, GetRelativeInt(entry, AMOUNT_PROPERTY, 0)),
                DropChance = Mathf.Clamp01(GetRelativeFloat(entry, DROP_CHANCE_PROPERTY, 1.0f)),
                MinMultiplier = Mathf.Max(0.0f, GetRelativeFloat(entry, MIN_AMOUNT_MULTIPLIER_PROPERTY, 1.0f)),
                MaxMultiplier = Mathf.Max(0.0f, GetRelativeFloat(entry, MAX_AMOUNT_MULTIPLIER_PROPERTY, 1.0f))
            };
            reward.MaxMultiplier = Mathf.Max(reward.MinMultiplier, reward.MaxMultiplier);
            return reward.Amount > 0;
        }

        return false;
    }

    // 웨이브 평균 보상으로 추가 업그레이드 가능한 레벨 수 리포트를 만든다
    private void BuildAffordabilityReports()
    {
        for (int i = 0; i < waveRewardRows.Count; i++)
        {
            WaveRewardRow rewardRow = waveRewardRows[i];
            int budget = Mathf.FloorToInt(rewardRow.AverageCoinPerWave);
            affordabilityRows.Add(new AffordabilityRow
            {
                WaveLabel = FormatWaveRange(rewardRow.MinWave, rewardRow.MaxWave),
                AverageCoinPerWave = rewardRow.AverageCoinPerWave,
                SentinelUpgradeCount = CalculateAffordableUpgradeCount(budget, 233, 1.0f),
                FirstEvolutionUpgradeCount = CalculateAffordableUpgradeCount(budget, 350, 2.0f),
                BranchEndUpgradeCount = CalculateAffordableUpgradeCount(budget, 640, 3.0f),
                SecondGenOneUpgradeCount = CalculateAffordableUpgradeCount(budget, 3200, 3.0f),
                SecondGenTwoUpgradeCount = CalculateAffordableUpgradeCount(budget, 5667, 4.0f),
                SecondGenThreeUpgradeCount = CalculateAffordableUpgradeCount(budget, 10571, 5.0f)
            });
        }
    }

    // 지정한 예산으로 레벨 1부터 단일 레벨업 비용을 지불할 때 추가 업그레이드 가능한 횟수를 계산한다
    private int CalculateAffordableUpgradeCount(int budget, int baseCoin, float additionalPercentPerTierLevel)
    {
        int level = REPORT_LEVEL_MIN;
        int upgradeCount = 0;
        int remainingBudget = Mathf.Max(0, budget);
        while (level < REPORT_LEVEL_MAX)
        {
            int targetLevel = level + 1;
            int nextCost = Mathf.CeilToInt(baseCoin * (1.0f + Mathf.Max(0, targetLevel - 1) * additionalPercentPerTierLevel * 0.01f));
            if (remainingBudget < nextCost)
            {
                break;
            }

            remainingBudget -= nextCost;
            level = targetLevel;
            upgradeCount++;
        }

        return upgradeCount;
    }

    // 터렛 DPS 표를 그린다
    private void DrawTurretDpsTable()
    {
        DrawInfoBox("DPS = damage * projectileCount / fireInterval. Pierce는 단일 대상 DPS에 포함하지 않습니다.");
        DrawHeader("Turret", "Lv1 DMG", "Lv1 Interval", "Lv1 Count", "Lv1 Pierce", "Lv1 DPS", "Lv100 DMG", "Lv100 Interval", "Lv100 Count", "Lv100 Pierce", "Lv100 DPS");
        for (int i = 0; i < turretDpsRows.Count; i++)
        {
            TurretDpsRow row = turretDpsRows[i];
            DrawRow(row.TurretName, FormatFloat(row.LevelOneDamage), FormatFloat(row.LevelOneInterval), row.LevelOneProjectileCount.ToString(), row.LevelOnePierceCount.ToString(), FormatFloat(row.LevelOneDps), FormatFloat(row.LevelMaxDamage), FormatFloat(row.LevelMaxInterval), row.LevelMaxProjectileCount.ToString(), row.LevelMaxPierceCount.ToString(), FormatFloat(row.LevelMaxDps));
        }
    }

    // 업그레이드 비용 표를 그린다
    private void DrawUpgradeCostTable()
    {
        DrawInfoBox("비용은 TurretUpgradeCostProfileSO.GetCosts 기준입니다.");
        DrawHeader("Turret", "Lv1->2", "Lv50->51", "Lv99->100", "Lv1->100", "Note");
        for (int i = 0; i < upgradeCostRows.Count; i++)
        {
            UpgradeCostRow row = upgradeCostRows[i];
            DrawRow(row.TurretName, FormatInt(row.LevelOneToTwo), FormatInt(row.LevelFiftyToFiftyOne), FormatInt(row.LevelNinetyNineToOneHundred), FormatInt(row.LevelOneToOneHundred), row.Note);
        }
    }

    // 진화 비용 표를 그린다
    private void DrawEvolutionCostTable()
    {
        DrawHeader("Source", "Required Lv", "Target", "Coin", "FirePart", "SpecialPart");
        for (int i = 0; i < evolutionCostRows.Count; i++)
        {
            EvolutionCostRow row = evolutionCostRows[i];
            DrawRow(row.SourceName, row.RequiredLevel.ToString(), row.TargetName, FormatInt(row.CoinCost), FormatInt(row.FirePartCost), FormatInt(row.SpecialPartCost));
        }
    }

    // 웨이브 보상 표를 그린다
    private void DrawWaveRewardTable()
    {
        DrawInfoBox("기본 Coin 보상과 stage rewardMultiplier 기준입니다. ZombieRewardModifier 조건부 보정은 별도 이벤트 밸런스로 보고 이 표에는 포함하지 않습니다.");
        DrawHeader("Wave", "Spawn", "Reward x", "Candidates", "Avg Coin/Kill", "Coin/Kill Range", "Avg Coin/Wave", "Coin/Wave Range");
        for (int i = 0; i < waveRewardRows.Count; i++)
        {
            WaveRewardRow row = waveRewardRows[i];
            DrawRow(FormatWaveRange(row.MinWave, row.MaxWave), row.SpawnCount.ToString(), FormatFloat(row.RewardMultiplier), row.CandidateCount.ToString(), FormatFloat(row.AverageCoinPerKill), $"{FormatInt(row.MinCoinPerKill)}~{FormatInt(row.MaxCoinPerKill)}", FormatFloat(row.AverageCoinPerWave), $"{FormatInt(row.MinCoinPerWave)}~{FormatInt(row.MaxCoinPerWave)}");
        }
    }

    // 웨이브 보상 기반 업그레이드 가능 레벨 표를 그린다
    private void DrawAffordabilityTable()
    {
        DrawInfoBox("평균 Wave Coin만으로 Lv1에서 단일 레벨업을 반복할 때 추가로 올릴 수 있는 레벨 수입니다. Lv0은 업그레이드 불가를 의미합니다. 진화 비용과 배치 비용은 제외합니다.");
        DrawHeader("Wave", "Avg Coin/Wave", "Sentinel", "Sentry/Vector", "Pulse/Vulcan", "2nd _1", "2nd _2", "2nd _3");
        for (int i = 0; i < affordabilityRows.Count; i++)
        {
            AffordabilityRow row = affordabilityRows[i];
            DrawRow(row.WaveLabel, FormatFloat(row.AverageCoinPerWave), FormatLevel(row.SentinelUpgradeCount), FormatLevel(row.FirstEvolutionUpgradeCount), FormatLevel(row.BranchEndUpgradeCount), FormatLevel(row.SecondGenOneUpgradeCount), FormatLevel(row.SecondGenTwoUpgradeCount), FormatLevel(row.SecondGenThreeUpgradeCount));
        }
    }

    // 안내 문구 박스를 그린다
    private static void DrawInfoBox(string message)
    {
        EditorGUILayout.HelpBox(message, MessageType.Info);
    }

    // 표 헤더 행을 그린다
    private static void DrawHeader(params string[] columns)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        for (int i = 0; i < columns.Length; i++)
        {
            EditorGUILayout.LabelField(columns[i], EditorStyles.boldLabel, GUILayout.MinWidth(90.0f));
        }

        EditorGUILayout.EndHorizontal();
    }

    // 표 데이터 행을 그린다
    private static void DrawRow(params string[] columns)
    {
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < columns.Length; i++)
        {
            EditorGUILayout.LabelField(columns[i], GUILayout.MinWidth(90.0f));
        }

        EditorGUILayout.EndHorizontal();
    }

    // 모든 리포트를 CSV 파일로 내보낸다
    private void ExportCsvFiles()
    {
        string folderPath = EditorUtility.SaveFolderPanel("Export Turret Balance CSV", Application.dataPath, "TurretBalanceReports");
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        WriteCsv(Path.Combine(folderPath, "turret_dps.csv"), BuildTurretDpsCsv());
        WriteCsv(Path.Combine(folderPath, "turret_upgrade_costs.csv"), BuildUpgradeCostCsv());
        WriteCsv(Path.Combine(folderPath, "turret_evolution_costs.csv"), BuildEvolutionCostCsv());
        WriteCsv(Path.Combine(folderPath, "wave_rewards.csv"), BuildWaveRewardCsv());
        WriteCsv(Path.Combine(folderPath, "wave_upgrade_affordability.csv"), BuildAffordabilityCsv());
        AssetDatabase.Refresh();
        Debug.Log($"[터렛 밸런스 리포트] CSV 내보내기 완료: {folderPath}");
    }

    // 터렛 DPS CSV 문자열을 만든다
    private string BuildTurretDpsCsv()
    {
        StringBuilder builder = new StringBuilder(4096);
        AppendCsvLine(builder, "Turret", "Lv1 Damage", "Lv1 Interval", "Lv1 Count", "Lv1 Pierce", "Lv1 DPS", "Lv100 Damage", "Lv100 Interval", "Lv100 Count", "Lv100 Pierce", "Lv100 DPS");
        for (int i = 0; i < turretDpsRows.Count; i++)
        {
            TurretDpsRow row = turretDpsRows[i];
            AppendCsvLine(builder, row.TurretName, row.LevelOneDamage, row.LevelOneInterval, row.LevelOneProjectileCount, row.LevelOnePierceCount, row.LevelOneDps, row.LevelMaxDamage, row.LevelMaxInterval, row.LevelMaxProjectileCount, row.LevelMaxPierceCount, row.LevelMaxDps);
        }

        return builder.ToString();
    }

    // 업그레이드 비용 CSV 문자열을 만든다
    private string BuildUpgradeCostCsv()
    {
        StringBuilder builder = new StringBuilder(2048);
        AppendCsvLine(builder, "Turret", "Lv1->2", "Lv50->51", "Lv99->100", "Lv1->100", "Note");
        for (int i = 0; i < upgradeCostRows.Count; i++)
        {
            UpgradeCostRow row = upgradeCostRows[i];
            AppendCsvLine(builder, row.TurretName, row.LevelOneToTwo, row.LevelFiftyToFiftyOne, row.LevelNinetyNineToOneHundred, row.LevelOneToOneHundred, row.Note);
        }

        return builder.ToString();
    }

    // 진화 비용 CSV 문자열을 만든다
    private string BuildEvolutionCostCsv()
    {
        StringBuilder builder = new StringBuilder(2048);
        AppendCsvLine(builder, "Source", "Required Level", "Target", "Coin", "FirePart", "SpecialPart");
        for (int i = 0; i < evolutionCostRows.Count; i++)
        {
            EvolutionCostRow row = evolutionCostRows[i];
            AppendCsvLine(builder, row.SourceName, row.RequiredLevel, row.TargetName, row.CoinCost, row.FirePartCost, row.SpecialPartCost);
        }

        return builder.ToString();
    }

    // 웨이브 보상 CSV 문자열을 만든다
    private string BuildWaveRewardCsv()
    {
        StringBuilder builder = new StringBuilder(2048);
        AppendCsvLine(builder, "Wave", "Spawn", "Reward Multiplier", "Candidates", "Avg Coin/Kill", "Min Coin/Kill", "Max Coin/Kill", "Avg Coin/Wave", "Min Coin/Wave", "Max Coin/Wave");
        for (int i = 0; i < waveRewardRows.Count; i++)
        {
            WaveRewardRow row = waveRewardRows[i];
            AppendCsvLine(builder, FormatWaveRange(row.MinWave, row.MaxWave), row.SpawnCount, row.RewardMultiplier, row.CandidateCount, row.AverageCoinPerKill, row.MinCoinPerKill, row.MaxCoinPerKill, row.AverageCoinPerWave, row.MinCoinPerWave, row.MaxCoinPerWave);
        }

        return builder.ToString();
    }

    // 업그레이드 가능 레벨 CSV 문자열을 만든다
    private string BuildAffordabilityCsv()
    {
        StringBuilder builder = new StringBuilder(2048);
        AppendCsvLine(builder, "Wave", "Avg Coin/Wave", "Sentinel Upgrade Count", "Sentry/Vector Upgrade Count", "Pulse/Vulcan Upgrade Count", "2nd _1 Upgrade Count", "2nd _2 Upgrade Count", "2nd _3 Upgrade Count");
        for (int i = 0; i < affordabilityRows.Count; i++)
        {
            AffordabilityRow row = affordabilityRows[i];
            AppendCsvLine(builder, row.WaveLabel, row.AverageCoinPerWave, row.SentinelUpgradeCount, row.FirstEvolutionUpgradeCount, row.BranchEndUpgradeCount, row.SecondGenOneUpgradeCount, row.SecondGenTwoUpgradeCount, row.SecondGenThreeUpgradeCount);
        }

        return builder.ToString();
    }

    // CSV 파일을 UTF-8로 저장한다
    private static void WriteCsv(string path, string contents)
    {
        File.WriteAllText(path, contents, new UTF8Encoding(true));
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
        if (!mustQuote)
        {
            return text;
        }

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    // 터렛 런타임 스탯에서 단일 대상 DPS를 계산한다
    private static float CalculateDps(TurretRuntimeStat stat)
    {
        return stat.fireInterval <= 0.0f ? 0.0f : stat.damage * Mathf.Max(1, stat.projectileCount) / stat.fireInterval;
    }

    // 비용 배열에서 Coin 비용 합계를 반환한다
    private static int GetCoinCost(ResourceCost[] costs)
    {
        return GetCurrencyCost(costs, RewardCurrencyType.Coin);
    }

    // 비용 배열에서 지정한 재화 비용 합계를 반환한다
    private static int GetCurrencyCost(ResourceCost[] costs, RewardCurrencyType currencyType)
    {
        if (costs == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost != null && cost.currencyType == currencyType)
            {
                total += Mathf.Max(0, cost.amount);
            }
        }

        return total;
    }

    // 터렛 표시 이름을 반환한다
    private static string GetTurretName(TurretDefinitionSO definition)
    {
        if (definition == null)
        {
            return "None";
        }

        return string.IsNullOrWhiteSpace(definition.displayName) ? definition.name : definition.displayName;
    }

    // SerializedProperty의 상대 정수 값을 읽는다
    private static int GetRelativeInt(SerializedProperty property, string relativePath, int fallback)
    {
        SerializedProperty relative = property == null ? null : property.FindPropertyRelative(relativePath);
        return relative == null ? fallback : relative.intValue;
    }

    // SerializedProperty의 상대 실수 값을 읽는다
    private static float GetRelativeFloat(SerializedProperty property, string relativePath, float fallback)
    {
        SerializedProperty relative = property == null ? null : property.FindPropertyRelative(relativePath);
        return relative == null ? fallback : relative.floatValue;
    }

    // SerializedProperty의 상대 오브젝트 참조 값을 읽는다
    private static T GetObjectReference<T>(SerializedProperty property, string relativePath) where T : UnityEngine.Object
    {
        SerializedProperty relative = property == null ? null : property.FindPropertyRelative(relativePath);
        return relative == null ? null : relative.objectReferenceValue as T;
    }

    // 런타임 스폰 배율의 0 이하 보정 규칙을 리포트 계산에 반영한다
    private static float SanitizeRuntimeMultiplier(float value)
    {
        return value > 0.0f ? value : 1.0f;
    }

    // 웨이브 범위를 표시 문자열로 변환한다
    private static string FormatWaveRange(int minWave, int maxWave)
    {
        return maxWave <= 0 ? $"{minWave}+" : $"{minWave}~{maxWave}";
    }

    // 레벨 표시 문자열을 만든다
    private static string FormatLevel(int level)
    {
        return $"Lv{level}";
    }

    // 정수 값을 표기용 문자열로 변환한다
    private static string FormatInt(int value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    // 실수 값을 표기용 문자열로 변환한다
    private static string FormatFloat(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    // 터렛 DPS 표의 행 데이터.
    private struct TurretDpsRow
    {
        public string TurretName;
        public float LevelOneDamage;
        public float LevelOneInterval;
        public int LevelOneProjectileCount;
        public int LevelOnePierceCount;
        public float LevelOneDps;
        public float LevelMaxDamage;
        public float LevelMaxInterval;
        public int LevelMaxProjectileCount;
        public int LevelMaxPierceCount;
        public float LevelMaxDps;
    }

    // 업그레이드 비용 표의 행 데이터.
    private struct UpgradeCostRow
    {
        public string TurretName;
        public int LevelOneToTwo;
        public int LevelFiftyToFiftyOne;
        public int LevelNinetyNineToOneHundred;
        public int LevelOneToOneHundred;
        public string Note;
    }

    // 진화 비용 표의 행 데이터.
    private struct EvolutionCostRow
    {
        public string SourceName;
        public int RequiredLevel;
        public string TargetName;
        public int CoinCost;
        public int FirePartCost;
        public int SpecialPartCost;
    }

    // 웨이브 보상 표의 행 데이터.
    private struct WaveRewardRow
    {
        public string ProfilePath;
        public int MinWave;
        public int MaxWave;
        public int SpawnCount;
        public float RewardMultiplier;
        public int CandidateCount;
        public float AverageCoinPerKill;
        public int MinCoinPerKill;
        public int MaxCoinPerKill;
        public float AverageCoinPerWave;
        public int MinCoinPerWave;
        public int MaxCoinPerWave;
    }

    // 웨이브 보상 기반 업그레이드 가능 레벨 표의 행 데이터.
    private struct AffordabilityRow
    {
        public string WaveLabel;
        public float AverageCoinPerWave;
        public int SentinelUpgradeCount;
        public int FirstEvolutionUpgradeCount;
        public int BranchEndUpgradeCount;
        public int SecondGenOneUpgradeCount;
        public int SecondGenTwoUpgradeCount;
        public int SecondGenThreeUpgradeCount;
    }

    // Coin 보상 엔트리의 계산용 데이터.
    private struct CoinRewardData
    {
        public int Amount;
        public float DropChance;
        public float MinMultiplier;
        public float MaxMultiplier;

        public float ExpectedAmount => Amount * DropChance * ((MinMultiplier + MaxMultiplier) * 0.5f);
    }

    // 스폰 후보 가중치를 반영한 보상 요약 데이터.
    private struct WeightedRewardSummary
    {
        public int CandidateCount;
        public float AverageCoinPerKill;
        public int MinCoinPerKill;
        public int MaxCoinPerKill;
    }
}
