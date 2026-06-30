using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 터렛 웨이브 밸런스 리포트의 에디터 입력 데이터 수집을 담당한다.
internal sealed class TurretBalanceReportInputCollector
{
    private const string STAGES_PROPERTY = "stages";
    private const string NORMAL_ZOMBIE_ENTRIES_PROPERTY = "normalZombieEntries";
    private const string BOSS_ZOMBIE_ENTRIES_PROPERTY = "bossZombieEntries";
    private const string PREFAB_PROPERTY = "prefab";
    private const string WEIGHT_PROPERTY = "weight";
    private const string REWARD_PROFILE_OVERRIDE_PROPERTY = "rewardProfileOverride";
    private const string APPLY_INITIAL_WALLET_PROPERTY = "applyInitialWalletOnAwake";
    private const string INITIAL_WALLET_CURRENCIES_PROPERTY = "initialWalletCurrencies";
    private const string RESOURCE_CURRENCY_TYPE_PROPERTY = "currencyType";
    private const string RESOURCE_AMOUNT_PROPERTY = "amount";

    // 현재 에디터 상태에서 리포트 입력 스냅샷을 수집한다
    public TurretBalanceInputSnapshot Collect()
    {
        TurretBalanceInputSnapshot snapshot = new TurretBalanceInputSnapshot();
        snapshot.InitialWalletCoin = LoadInitialWalletCoin(snapshot.Warnings);
        snapshot.WaveClearCoinBonusPercentage = LoadWaveClearCoinBonusPercentage(snapshot.Warnings);
        LoadShopEntries(snapshot);
        LoadWaveProfiles(snapshot);
        return snapshot;
    }

    // 리포트 계산에 영향을 주는 에셋과 씬 오브젝트의 변경 서명을 만든다
    public string BuildDataSignature()
    {
        StringBuilder builder = new StringBuilder(2048);
        AppendAssetSignatures(builder, "t:TurretShopEntrySO");
        AppendAssetSignatures(builder, "t:TurretDefinitionSO");
        AppendAssetSignatures(builder, "t:TurretStatProfileSO");
        AppendAssetSignatures(builder, "t:TurretStatGrowthProfileSO");
        AppendAssetSignatures(builder, "t:TurretUpgradeCostProfileSO");
        AppendAssetSignatures(builder, "t:ZombieWaveSpawnProfileSO");
        AppendAssetSignatures(builder, "t:ZombieRewardProfileSO");
        AppendAssetSignatures(builder, "t:ObstacleBuildEntrySO");
        AppendAssetSignatures(builder, "t:ObstacleDefinitionSO");
        AppendAssetSignatures(builder, "t:ObstacleUpgradeCostProfileSO");
        AppendSceneInventorySignature(builder);
        AppendSceneGameManagerSignature(builder);
        return builder.ToString();
    }

    // 지정 타입 에셋들의 경로, 의존성 해시, dirty 카운트를 서명에 추가한다
    private static void AppendAssetSignatures(StringBuilder builder, string filter)
    {
        string[] guids = AssetDatabase.FindAssets(filter);
        Array.Sort(guids, StringComparer.Ordinal);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            builder.Append(path);
            builder.Append('|');
            builder.Append(AssetDatabase.GetAssetDependencyHash(path));
            builder.Append('|');
            builder.Append(asset == null ? 0 : EditorUtility.GetDirtyCount(asset));
            builder.AppendLine();
        }
    }

    // 열린 씬 인벤토리 시스템의 초기 지갑 서명을 추가한다
    private static void AppendSceneInventorySignature(StringBuilder builder)
    {
        InventorySystem inventorySystem = FindSceneInventorySystem();
        if (inventorySystem == null)
        {
            inventorySystem = FindPrefabInventorySystem();
        }

        builder.Append("InventorySystem|");
        builder.Append(inventorySystem == null ? 0 : inventorySystem.GetInstanceID());
        builder.Append('|');
        builder.Append(inventorySystem == null ? 0 : EditorUtility.GetDirtyCount(inventorySystem));
        builder.Append('|');
        builder.Append(ReadInitialWalletCoin(inventorySystem));
        builder.AppendLine();
    }

    // 인벤토리 시스템의 초기 지갑 코인을 읽는다
    private static int LoadInitialWalletCoin(List<ReportWarning> warnings)
    {
        InventorySystem inventorySystem = FindSceneInventorySystem();
        if (inventorySystem != null)
        {
            return ReadInitialWalletCoin(inventorySystem);
        }

        inventorySystem = FindPrefabInventorySystem();
        if (inventorySystem != null)
        {
            ReportWarning.Add(warnings, ReportWarningSeverity.Info, "InventorySystem", AssetDatabase.GetAssetPath(inventorySystem.gameObject), "열려 있는 씬의 InventorySystem을 찾지 못해 프리팹 초기 지갑 값을 사용했습니다.");
            return ReadInitialWalletCoin(inventorySystem);
        }

        ReportWarning.Add(warnings, ReportWarningSeverity.Warning, "InventorySystem", "AssetDatabase", "InventorySystem을 찾지 못해 초기 코인을 0으로 계산했습니다.");
        return 0;
    }

    // 열려 있는 씬의 인벤토리 시스템을 찾는다
    private static InventorySystem FindSceneInventorySystem()
    {
        InventorySystem[] systems = Resources.FindObjectsOfTypeAll<InventorySystem>();
        for (int i = 0; i < systems.Length; i++)
        {
            InventorySystem system = systems[i];
            if (system != null && system.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(system))
            {
                return system;
            }
        }

        return null;
    }

    // 프리팹 에셋에서 인벤토리 시스템을 찾는다
    private static InventorySystem FindPrefabInventorySystem()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            InventorySystem inventorySystem = prefab.GetComponentInChildren<InventorySystem>(true);
            if (inventorySystem != null)
            {
                return inventorySystem;
            }
        }

        return null;
    }

    // 열린 씬 게임 매니저의 웨이브 클리어 보너스 서명을 추가한다
    private static void AppendSceneGameManagerSignature(StringBuilder builder)
    {
        GameManager gameManager = FindSceneGameManager();
        if (gameManager == null)
        {
            gameManager = FindPrefabGameManager();
        }

        builder.Append("GameManager|");
        builder.Append(gameManager == null ? 0 : gameManager.GetInstanceID());
        builder.Append('|');
        builder.Append(gameManager == null ? 0 : EditorUtility.GetDirtyCount(gameManager));
        builder.Append('|');
        builder.Append(gameManager == null ? 0 : gameManager.waveClearCoinBonusPercentage);
        builder.AppendLine();
    }

    // 게임 매니저의 웨이브 클리어 코인 보너스 퍼센트를 읽는다
    private static int LoadWaveClearCoinBonusPercentage(List<ReportWarning> warnings)
    {
        GameManager gameManager = FindSceneGameManager();
        if (gameManager != null)
        {
            return Mathf.Max(0, gameManager.waveClearCoinBonusPercentage);
        }

        gameManager = FindPrefabGameManager();
        if (gameManager != null)
        {
            ReportWarning.Add(warnings, ReportWarningSeverity.Info, "GameManager", AssetDatabase.GetAssetPath(gameManager.gameObject), "열려 있는 씬의 GameManager를 찾지 못해 프리팹 웨이브 클리어 보너스 값을 사용했습니다.");
            return Mathf.Max(0, gameManager.waveClearCoinBonusPercentage);
        }

        ReportWarning.Add(warnings, ReportWarningSeverity.Warning, "GameManager", "AssetDatabase", "GameManager를 찾지 못해 웨이브 클리어 보너스를 0%로 계산했습니다.");
        return 0;
    }

    // 열려 있는 씬의 게임 매니저를 찾는다
    private static GameManager FindSceneGameManager()
    {
        GameManager[] managers = Resources.FindObjectsOfTypeAll<GameManager>();
        for (int i = 0; i < managers.Length; i++)
        {
            GameManager manager = managers[i];
            if (manager != null && manager.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(manager))
            {
                return manager;
            }
        }

        return null;
    }

    // 프리팹 에셋에서 게임 매니저를 찾는다
    private static GameManager FindPrefabGameManager()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            GameManager gameManager = prefab.GetComponentInChildren<GameManager>(true);
            if (gameManager != null)
            {
                return gameManager;
            }
        }

        return null;
    }

    // 인벤토리 시스템 직렬화 값에서 초기 코인을 계산한다
    private static int ReadInitialWalletCoin(InventorySystem inventorySystem)
    {
        if (inventorySystem == null)
        {
            return 0;
        }

        SerializedObject serializedObject = new SerializedObject(inventorySystem);
        SerializedProperty applyInitialWallet = serializedObject.FindProperty(APPLY_INITIAL_WALLET_PROPERTY);
        if (applyInitialWallet != null && !applyInitialWallet.boolValue)
        {
            return 0;
        }

        SerializedProperty currencies = serializedObject.FindProperty(INITIAL_WALLET_CURRENCIES_PROPERTY);
        if (currencies == null || !currencies.isArray)
        {
            return 0;
        }

        int totalCoin = 0;
        for (int i = 0; i < currencies.arraySize; i++)
        {
            SerializedProperty currency = currencies.GetArrayElementAtIndex(i);
            if (currency == null)
            {
                continue;
            }

            SerializedProperty currencyType = currency.FindPropertyRelative(RESOURCE_CURRENCY_TYPE_PROPERTY);
            SerializedProperty amount = currency.FindPropertyRelative(RESOURCE_AMOUNT_PROPERTY);
            if (currencyType != null && amount != null && currencyType.enumValueIndex == (int)RewardCurrencyType.Coin)
            {
                totalCoin += Mathf.Max(0, amount.intValue);
            }
        }

        return totalCoin;
    }

    // 터렛 상점 엔트리 에셋을 로드하고 필수 참조를 검증한다
    private static void LoadShopEntries(TurretBalanceInputSnapshot snapshot)
    {
        string[] guids = AssetDatabase.FindAssets("t:TurretShopEntrySO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TurretShopEntrySO entry = AssetDatabase.LoadAssetAtPath<TurretShopEntrySO>(path);
            if (entry == null)
            {
                continue;
            }

            if (entry.TurretDefinition == null)
            {
                ReportWarning.Add(snapshot.Warnings, ReportWarningSeverity.Warning, "TurretShopEntrySO", path, "TurretDefinition이 비어 있어 시뮬레이션에서 제외됩니다.");
                continue;
            }

            snapshot.ShopEntries.Add(entry);
            ValidateShopEntry(entry, path, snapshot.Warnings);
        }

        snapshot.ShopEntries.Sort(CompareShopEntries);
    }

    // 상점 엔트리 정렬 순서를 비교한다
    private static int CompareShopEntries(TurretShopEntrySO left, TurretShopEntrySO right)
    {
        return string.Compare(GetShopEntryName(left), GetShopEntryName(right), StringComparison.Ordinal);
    }

    // 상점 엔트리 표시 이름을 반환한다
    private static string GetShopEntryName(TurretShopEntrySO entry)
    {
        return entry == null ? "None" : entry.DisplayName;
    }

    // 상점 엔트리의 필수 참조와 Coin 외 비용을 점검한다
    private static void ValidateShopEntry(TurretShopEntrySO entry, string path, List<ReportWarning> warnings)
    {
        TurretDefinitionSO definition = entry.TurretDefinition;
        if (definition.baseStatProfile == null)
        {
            ReportWarning.Add(warnings, ReportWarningSeverity.Warning, "TurretShopEntrySO", path, "baseStatProfile이 없어 DPS가 0으로 계산됩니다.");
        }

        if (definition.statGrowthProfile == null)
        {
            ReportWarning.Add(warnings, ReportWarningSeverity.Info, "TurretShopEntrySO", path, "statGrowthProfile이 없어 Lv1 스탯만 사용됩니다.");
        }

        if (definition.upgradeCostProfile == null)
        {
            ReportWarning.Add(warnings, ReportWarningSeverity.Warning, "TurretShopEntrySO", path, "upgradeCostProfile이 없어 업그레이드가 불가능한 것으로 계산됩니다.");
        }

        if (HasNonCoinCost(entry.GetPlacementCosts(0)))
        {
            ReportWarning.Add(warnings, ReportWarningSeverity.Info, "TurretShopEntrySO", path, "설치 비용에 Coin 외 재화가 있어 Coin 시뮬레이션에서는 Note로만 표시됩니다.");
        }
    }

    // 비용 배열에서 Coin 외 재화가 있는지 확인한다
    private static bool HasNonCoinCost(ResourceCost[] costs)
    {
        if (costs == null)
        {
            return false;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost != null && cost.amount > 0 && cost.currencyType != RewardCurrencyType.Coin)
            {
                return true;
            }
        }

        return false;
    }

    // 웨이브 스폰 프로필 에셋을 로드한다
    private static void LoadWaveProfiles(TurretBalanceInputSnapshot snapshot)
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

            snapshot.WaveProfiles.Add(CreateWaveProfileInput(profile, path, snapshot.Warnings));
        }
    }

    // 웨이브 스폰 프로필을 계산 입력 DTO로 변환한다
    private static WaveProfileInput CreateWaveProfileInput(ZombieWaveSpawnProfileSO profile, string path, List<ReportWarning> warnings)
    {
        WaveProfileInput input = new WaveProfileInput
        {
            Path = path
        };

        SerializedObject serializedProfile = new SerializedObject(profile);
        SerializedProperty stages = serializedProfile.FindProperty(STAGES_PROPERTY);
        if (stages == null || !stages.isArray)
        {
            ReportWarning.Add(warnings, ReportWarningSeverity.Warning, "ZombieWaveSpawnProfileSO", path, "stages 배열을 찾을 수 없습니다.");
            return input;
        }

        for (int i = 0; i < stages.arraySize; i++)
        {
            SerializedProperty stage = stages.GetArrayElementAtIndex(i);
            if (stage == null)
            {
                continue;
            }

            input.Stages.Add(CreateStageInput(stage));
        }

        return input;
    }

    // 직렬화된 스테이지 값을 계산 입력 DTO로 변환한다
    private static WaveStageInput CreateStageInput(SerializedProperty stage)
    {
        WaveStageInput input = new WaveStageInput
        {
            MinWave = Mathf.Max(1, GetRelativeInt(stage, "minWave", 1)),
            MaxWave = GetRelativeInt(stage, "maxWave", 0),
            SpawnCount = Mathf.Max(0, GetRelativeInt(stage, "spawnCount", 0)),
            SpawnBossAsLastEnemy = GetRelativeBool(stage, "spawnBossAsLastEnemy", false),
            HpMultiplier = SanitizeRuntimeMultiplier(GetRelativeFloat(stage, "hpMultiplier", 1.0f)),
            AttackDamageMultiplier = SanitizeRuntimeMultiplier(GetRelativeFloat(stage, "attackDamageMultiplier", 1.0f)),
            MoveAttackSpeedMultiplier = SanitizeRuntimeMultiplier(GetRelativeFloat(stage, "moveAttackSpeedMultiplier", 1.0f)),
            RewardMultiplier = SanitizeRuntimeMultiplier(GetRelativeFloat(stage, "rewardMultiplier", 1.0f))
        };

        AddSpawnEntries(stage.FindPropertyRelative(NORMAL_ZOMBIE_ENTRIES_PROPERTY), input.NormalEntries);
        AddSpawnEntries(stage.FindPropertyRelative(BOSS_ZOMBIE_ENTRIES_PROPERTY), input.BossEntries);
        return input;
    }

    // 직렬화된 스폰 후보 배열을 계산 입력 DTO로 변환한다
    private static void AddSpawnEntries(SerializedProperty entries, List<SpawnEntryInput> target)
    {
        if (entries == null || !entries.isArray)
        {
            return;
        }

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (entry == null)
            {
                continue;
            }

            UnityEngine.Object prefabReference = GetObjectReference<UnityEngine.Object>(entry, PREFAB_PROPERTY);
            target.Add(CreateSpawnEntryInput(entry, prefabReference));
        }
    }

    // 직렬화된 스폰 후보 하나를 계산 입력 DTO로 변환한다
    private static SpawnEntryInput CreateSpawnEntryInput(SerializedProperty entry, UnityEngine.Object prefabReference)
    {
        GameObject prefab = GetPrefabGameObject(prefabReference);
        NormalZombie normalZombie = prefab == null ? null : prefab.GetComponent<NormalZombie>();
        BossZombie bossZombie = prefab == null ? null : prefab.GetComponent<BossZombie>();
        UnityEngine.Object zombieComponent = normalZombie != null ? normalZombie : (UnityEngine.Object)bossZombie;
        ZombieRewardProfileSO rewardProfileOverride = null;
        if (zombieComponent != null)
        {
            TryGetRewardProfile(zombieComponent, out rewardProfileOverride);
        }

        return new SpawnEntryInput
        {
            PrefabReference = prefabReference,
            Weight = Mathf.Max(0, GetRelativeInt(entry, WEIGHT_PROPERTY, 0)),
            MinWave = Mathf.Max(1, GetRelativeInt(entry, "minWave", 1)),
            MaxWave = GetRelativeInt(entry, "maxWave", 0),
            Prefab = prefab,
            NormalZombie = normalZombie,
            BossZombie = bossZombie,
            SourceSpec = normalZombie != null ? normalZombie.spec : bossZombie == null ? null : bossZombie.spec,
            RewardProfileOverride = rewardProfileOverride
        };
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
    private static bool TryGetRewardProfile(UnityEngine.Object zombieComponent, out ZombieRewardProfileSO rewardProfile)
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

    // SerializedProperty의 상대 bool 값을 읽는다
    private static bool GetRelativeBool(SerializedProperty property, string relativePath, bool fallback)
    {
        SerializedProperty relative = property == null ? null : property.FindPropertyRelative(relativePath);
        return relative == null ? fallback : relative.boolValue;
    }

    // SerializedProperty의 상대 오브젝트 참조 값을 읽는다
    private static T GetObjectReference<T>(SerializedProperty property, string relativePath) where T : UnityEngine.Object
    {
        SerializedProperty relative = property == null ? null : property.FindPropertyRelative(relativePath);
        return relative == null ? null : relative.objectReferenceValue as T;
    }

    // 런타임 배율의 0 이하 보정 규칙을 리포트 계산에 반영한다
    private static float SanitizeRuntimeMultiplier(float value)
    {
        return value > 0.0f ? value : 1.0f;
    }
}
