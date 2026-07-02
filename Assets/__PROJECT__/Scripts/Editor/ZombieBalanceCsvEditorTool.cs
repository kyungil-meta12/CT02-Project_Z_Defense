#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 좀비 웨이브 스폰, 처치 보상, 생존자 구출 스폰 프로필을 CSV로 임포트/익스포트한다.
/// </summary>
public class ZombieBalanceCsvEditorTool : EditorWindow
{
    private const string WAVE_PROFILE_PATH = "Assets/__PROJECT__/Scenes/KKW/Turret_Scene/SO/Zombie Wave Spawn Profile/ZombieWaveSpawnProfile.asset";
    private const string WAVE_CSV_PATH = "Assets/__PROJECT__/Scenes/KKW/Turret_Scene/SO/Zombie Wave Spawn Profile/ZombieWaveSpawnProfile.csv";
    private const string REWARD_ROOT_PATH = "Assets/__PROJECT__/Scenes/KKW/Turret_Scene/SO/Rewards";
    private const string REWARD_CSV_PATH = "Assets/__PROJECT__/Scenes/KKW/Turret_Scene/SO/Rewards/ZombieRewardProfiles.csv";
    private const string SURVIVOR_RESCUE_PROFILE_PATH = "Assets/__PROJECT__/Prefabs/Survivor/SurvivorRescueSpawnProfile.asset";
    private const string SURVIVOR_RESCUE_CSV_PATH = "Assets/__PROJECT__/Prefabs/Survivor/SurvivorRescueSpawnProfile.csv";

    private Vector2 scrollPosition;
    private readonly List<string> lastMessages = new List<string>(32);

    [MenuItem("Tools/좀비 밸런스 CSV 관리 도구")]
    // 좀비 밸런스 CSV 관리 창을 연다
    public static void ShowWindow()
    {
        GetWindow<ZombieBalanceCsvEditorTool>("좀비 밸런스 CSV");
    }

    // 에디터 창 UI를 그린다
    private void OnGUI()
    {
        EditorGUILayout.LabelField("좀비 밸런스 CSV 관리 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        DrawFixedPaths();
        EditorGUILayout.Space();

        DrawWaveButtons();
        DrawRewardButtons();
        DrawSurvivorRescueButtons();
        DrawMessages();
    }

    // 고정된 CSV와 프로필 경로를 표시한다
    private void DrawFixedPaths()
    {
        EditorGUILayout.LabelField("웨이브 프로필", WAVE_PROFILE_PATH);
        EditorGUILayout.LabelField("웨이브 CSV", WAVE_CSV_PATH);
        EditorGUILayout.LabelField("보상 폴더", REWARD_ROOT_PATH);
        EditorGUILayout.LabelField("보상 CSV", REWARD_CSV_PATH);
        EditorGUILayout.LabelField("생존자 구출 프로필", SURVIVOR_RESCUE_PROFILE_PATH);
        EditorGUILayout.LabelField("생존자 구출 CSV", SURVIVOR_RESCUE_CSV_PATH);
    }

    // 웨이브 스폰 프로필 CSV 버튼을 그린다
    private void DrawWaveButtons()
    {
        EditorGUILayout.LabelField("Zombie Wave Spawn Profile", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("웨이브 CSV 익스포트", GUILayout.Height(32)))
        {
            ExecuteSafely(ExportWaveSpawnProfile);
        }

        if (GUILayout.Button("웨이브 CSV 임포트", GUILayout.Height(32)))
        {
            ExecuteSafely(ImportWaveSpawnProfile);
        }

        if (GUILayout.Button("웨이브 CSV 열기", GUILayout.Height(32)))
        {
            ExecuteSafely(OpenWaveCsv);
        }
        EditorGUILayout.EndHorizontal();
    }

    // 좀비 보상 프로필 CSV 버튼을 그린다
    private void DrawRewardButtons()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Zombie Reward Profiles", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("보상 CSV 익스포트", GUILayout.Height(32)))
        {
            ExecuteSafely(ExportRewardProfiles);
        }

        if (GUILayout.Button("보상 CSV 임포트", GUILayout.Height(32)))
        {
            ExecuteSafely(ImportRewardProfiles);
        }

        if (GUILayout.Button("보상 CSV 열기", GUILayout.Height(32)))
        {
            ExecuteSafely(OpenRewardCsv);
        }
        EditorGUILayout.EndHorizontal();
    }

    // 생존자 구출 스폰 프로필 CSV 버튼을 그린다
    private void DrawSurvivorRescueButtons()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Survivor Rescue Spawn Profile", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("구출 CSV 익스포트", GUILayout.Height(32)))
        {
            ExecuteSafely(ExportSurvivorRescueProfile);
        }

        if (GUILayout.Button("구출 CSV 임포트", GUILayout.Height(32)))
        {
            ExecuteSafely(ImportSurvivorRescueProfile);
        }

        if (GUILayout.Button("구출 CSV 열기", GUILayout.Height(32)))
        {
            ExecuteSafely(OpenSurvivorRescueCsv);
        }
        EditorGUILayout.EndHorizontal();
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

    // 웨이브 스폰 프로필을 CSV로 내보낸다
    private void ExportWaveSpawnProfile()
    {
        ClearRunState();
        ZombieWaveSpawnProfileSO profile = AssetDatabase.LoadAssetAtPath<ZombieWaveSpawnProfileSO>(WAVE_PROFILE_PATH);
        if (profile == null)
        {
            AddMessage("웨이브 스폰 프로필을 찾을 수 없습니다: " + WAVE_PROFILE_PATH);
            FlushMessagesToConsole(false);
            return;
        }

        SerializedObject serializedObject = new SerializedObject(profile);
        SerializedProperty stages = serializedObject.FindProperty("stages");
        List<ZombieWaveDpsMeasurementProfileSO> dpsProfiles = LoadZombieWaveDpsMeasurementProfiles();
        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine("MinWave(시작 웨이브),MaxWave(종료 웨이브),SpawnInterval(스폰 간격),SpawnCount(스폰 수),SpawnBossAsLastEnemy(마지막 적 보스 여부),HpMultiplier(체력 배율),AttackDamageMultiplier(공격력 배율),MoveAttackSpeedMultiplier(이동/공격 속도 배율),RewardMultiplier(보상 배율),NormalZombieEntries(일반 좀비 후보),BossZombieEntries(보스 좀비 후보),MeasuredNormalDps(일반 실측/정규화 DPS),MeasuredBossDps_Boomer(부머 실측/정규화 DPS),MeasuredBossDps_Screamer(스크리머 실측/정규화 DPS),MeasuredBossDps_Tank(탱크 실측/정규화 DPS)");
        for (int i = 0; i < stages.arraySize; i++)
        {
            SerializedProperty stage = stages.GetArrayElementAtIndex(i);
            AppendWaveStageCsvLine(builder, stages, stage, dpsProfiles);
        }

        WriteUtf8Csv(WAVE_CSV_PATH, builder.ToString());
        AddMessage("웨이브 CSV 익스포트 완료: " + WAVE_CSV_PATH);
        FlushMessagesToConsole(true);
    }

    // CSV 한 줄에 웨이브 스테이지 값을 추가한다
    private static void AppendWaveStageCsvLine(StringBuilder builder, SerializedProperty stages, SerializedProperty stage, List<ZombieWaveDpsMeasurementProfileSO> dpsProfiles)
    {
        builder.Append(GetRelativeInt(stage, "minWave"));
        builder.Append(',');
        builder.Append(GetRelativeInt(stage, "maxWave"));
        builder.Append(',');
        builder.Append(GetRelativeFloat(stage, "spawnInterval"));
        builder.Append(',');
        builder.Append(GetRelativeInt(stage, "spawnCount"));
        builder.Append(',');
        builder.Append(GetRelativeBool(stage, "spawnBossAsLastEnemy"));
        builder.Append(',');
        builder.Append(GetRelativeFloat(stage, "hpMultiplier"));
        builder.Append(',');
        builder.Append(GetRelativeFloat(stage, "attackDamageMultiplier"));
        builder.Append(',');
        builder.Append(GetRelativeFloat(stage, "moveAttackSpeedMultiplier"));
        builder.Append(',');
        builder.Append(GetRelativeFloat(stage, "rewardMultiplier"));
        builder.Append(',');
        builder.Append(EscapeCsvField(FormatNormalZombieEntries(stage.FindPropertyRelative("normalZombieEntries"))));
        builder.Append(',');
        builder.Append(EscapeCsvField(FormatBossZombieEntries(stage.FindPropertyRelative("bossZombieEntries"))));
        builder.Append(',');
        builder.Append(ResolveNormalDpsCsvValue(stages, stage, dpsProfiles));
        builder.Append(',');
        builder.Append(ResolveBossDpsCsvValue(stages, stage, dpsProfiles, BossZombieType.Boomer));
        builder.Append(',');
        builder.Append(ResolveBossDpsCsvValue(stages, stage, dpsProfiles, BossZombieType.Screamer));
        builder.Append(',');
        builder.Append(ResolveBossDpsCsvValue(stages, stage, dpsProfiles, BossZombieType.Tank));
        builder.AppendLine();
    }

    // 프로젝트 내 좀비 DPS 측정 프로필을 모두 로드한다
    private static List<ZombieWaveDpsMeasurementProfileSO> LoadZombieWaveDpsMeasurementProfiles()
    {
        string[] guids = AssetDatabase.FindAssets("t:ZombieWaveDpsMeasurementProfileSO");
        List<ZombieWaveDpsMeasurementProfileSO> profiles = new List<ZombieWaveDpsMeasurementProfileSO>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ZombieWaveDpsMeasurementProfileSO profile = AssetDatabase.LoadAssetAtPath<ZombieWaveDpsMeasurementProfileSO>(path);
            if (profile != null)
            {
                profiles.Add(profile);
            }
        }

        return profiles;
    }

    // 일반 좀비 DPS CSV 값을 실측 또는 정규화 값으로 계산한다
    private static string ResolveNormalDpsCsvValue(SerializedProperty stages, SerializedProperty targetStage, List<ZombieWaveDpsMeasurementProfileSO> dpsProfiles)
    {
        int targetWave = Mathf.Max(1, GetRelativeInt(targetStage, "minWave"));
        if (TryGetExactMeasuredNormalDps(dpsProfiles, targetWave, out float exactDps))
        {
            return FormatDpsCsvValue(exactDps);
        }

        float targetAttackMultiplier = SanitizeDpsMultiplier(GetRelativeFloat(targetStage, "attackDamageMultiplier"));
        float targetSpeedMultiplier = SanitizeDpsMultiplier(GetRelativeFloat(targetStage, "moveAttackSpeedMultiplier"));
        if (TryGetNormalizedMeasuredNormalDps(stages, dpsProfiles, targetAttackMultiplier, targetSpeedMultiplier, out float normalizedDps))
        {
            return FormatDpsCsvValue(normalizedDps);
        }

        return string.Empty;
    }

    // 보스 좀비 DPS CSV 값을 실측 또는 정규화 값으로 계산한다
    private static string ResolveBossDpsCsvValue(SerializedProperty stages, SerializedProperty targetStage, List<ZombieWaveDpsMeasurementProfileSO> dpsProfiles, BossZombieType bossType)
    {
        int targetWave = Mathf.Max(1, GetRelativeInt(targetStage, "minWave"));
        if (TryGetExactMeasuredBossDps(dpsProfiles, targetWave, bossType, out float exactDps))
        {
            return FormatDpsCsvValue(exactDps);
        }

        float targetAttackMultiplier = SanitizeDpsMultiplier(GetRelativeFloat(targetStage, "attackDamageMultiplier"));
        float targetSpeedMultiplier = SanitizeDpsMultiplier(GetRelativeFloat(targetStage, "moveAttackSpeedMultiplier"));
        if (TryGetNormalizedMeasuredBossDps(stages, dpsProfiles, bossType, targetAttackMultiplier, targetSpeedMultiplier, out float normalizedDps))
        {
            return FormatDpsCsvValue(normalizedDps);
        }

        return string.Empty;
    }

    // 같은 웨이브의 일반 좀비 실측 DPS를 찾는다
    private static bool TryGetExactMeasuredNormalDps(List<ZombieWaveDpsMeasurementProfileSO> dpsProfiles, int wave, out float dps)
    {
        dps = 0.0f;
        if (dpsProfiles == null)
        {
            return false;
        }

        for (int i = 0; i < dpsProfiles.Count; i++)
        {
            ZombieWaveDpsMeasurementProfileSO profile = dpsProfiles[i];
            if (profile != null && profile.TryGetDps(wave, ZombieRewardTypeFilter.NormalOnly, out dps))
            {
                return true;
            }
        }

        return false;
    }

    // 같은 웨이브의 보스 타입별 실측 DPS를 찾는다
    private static bool TryGetExactMeasuredBossDps(List<ZombieWaveDpsMeasurementProfileSO> dpsProfiles, int wave, BossZombieType bossType, out float dps)
    {
        dps = 0.0f;
        if (dpsProfiles == null)
        {
            return false;
        }

        for (int i = 0; i < dpsProfiles.Count; i++)
        {
            ZombieWaveDpsMeasurementProfileSO profile = dpsProfiles[i];
            if (profile != null && profile.TryGetBossDps(wave, bossType, out dps))
            {
                return true;
            }
        }

        return false;
    }

    // 다른 웨이브의 일반 좀비 실측값을 대상 웨이브 배율로 정규화한다
    private static bool TryGetNormalizedMeasuredNormalDps(SerializedProperty stages, List<ZombieWaveDpsMeasurementProfileSO> dpsProfiles, float targetAttackMultiplier, float targetSpeedMultiplier, out float dps)
    {
        dps = 0.0f;
        if (dpsProfiles == null)
        {
            return false;
        }

        for (int i = 0; i < dpsProfiles.Count; i++)
        {
            ZombieWaveDpsMeasurementProfileSO profile = dpsProfiles[i];
            if (profile == null)
            {
                continue;
            }

            IReadOnlyList<WaveZombieDpsSample> samples = profile.Samples;
            for (int j = 0; j < samples.Count; j++)
            {
                WaveZombieDpsSample sample = samples[j];
                if (sample == null || !sample.TryGetDps(ZombieRewardTypeFilter.NormalOnly, out float measuredDps))
                {
                    continue;
                }

                if (TryNormalizeMeasuredDps(stages, sample.Wave, measuredDps, targetAttackMultiplier, targetSpeedMultiplier, out dps))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // 다른 웨이브의 보스 타입별 실측값을 대상 웨이브 배율로 정규화한다
    private static bool TryGetNormalizedMeasuredBossDps(SerializedProperty stages, List<ZombieWaveDpsMeasurementProfileSO> dpsProfiles, BossZombieType bossType, float targetAttackMultiplier, float targetSpeedMultiplier, out float dps)
    {
        dps = 0.0f;
        if (dpsProfiles == null)
        {
            return false;
        }

        for (int i = 0; i < dpsProfiles.Count; i++)
        {
            ZombieWaveDpsMeasurementProfileSO profile = dpsProfiles[i];
            if (profile == null)
            {
                continue;
            }

            IReadOnlyList<WaveZombieDpsSample> samples = profile.Samples;
            for (int j = 0; j < samples.Count; j++)
            {
                WaveZombieDpsSample sample = samples[j];
                if (sample == null || !sample.TryGetBossDps(bossType, out float measuredDps))
                {
                    continue;
                }

                if (TryNormalizeMeasuredDps(stages, sample.Wave, measuredDps, targetAttackMultiplier, targetSpeedMultiplier, out dps))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // 실측 DPS를 측정 웨이브 배율 기준값으로 되돌린 뒤 대상 배율을 적용한다
    private static bool TryNormalizeMeasuredDps(SerializedProperty stages, int measuredWave, float measuredDps, float targetAttackMultiplier, float targetSpeedMultiplier, out float dps)
    {
        dps = 0.0f;
        if (measuredDps <= 0.0f || !TryGetDpsMultipliersForWave(stages, measuredWave, out float measuredAttackMultiplier, out float measuredSpeedMultiplier))
        {
            return false;
        }

        float measuredFactor = SanitizeDpsMultiplier(measuredAttackMultiplier) * SanitizeDpsMultiplier(measuredSpeedMultiplier);
        float targetFactor = SanitizeDpsMultiplier(targetAttackMultiplier) * SanitizeDpsMultiplier(targetSpeedMultiplier);
        dps = measuredDps / measuredFactor * targetFactor;
        return dps > 0.0f;
    }

    // 지정 웨이브가 속한 스테이지의 DPS 관련 배율을 찾는다
    private static bool TryGetDpsMultipliersForWave(SerializedProperty stages, int wave, out float attackMultiplier, out float speedMultiplier)
    {
        attackMultiplier = 1.0f;
        speedMultiplier = 1.0f;
        if (stages == null)
        {
            return false;
        }

        int safeWave = Mathf.Max(1, wave);
        for (int i = 0; i < stages.arraySize; i++)
        {
            SerializedProperty stage = stages.GetArrayElementAtIndex(i);
            int minWave = GetRelativeInt(stage, "minWave");
            int maxWave = GetRelativeInt(stage, "maxWave");
            if (safeWave < minWave || (maxWave > 0 && safeWave > maxWave))
            {
                continue;
            }

            attackMultiplier = GetRelativeFloat(stage, "attackDamageMultiplier");
            speedMultiplier = GetRelativeFloat(stage, "moveAttackSpeedMultiplier");
            return true;
        }

        return false;
    }

    // DPS 배율에 사용할 수 없는 값을 안전한 기본값으로 바꾼다
    private static float SanitizeDpsMultiplier(float multiplier)
    {
        return multiplier > 0.0f ? multiplier : 1.0f;
    }

    // DPS 값을 CSV 표시 문자열로 변환한다
    private static string FormatDpsCsvValue(float dps)
    {
        return dps > 0.0f ? dps.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;
    }

    // 웨이브 스폰 CSV를 프로필에 반영한다
    private void ImportWaveSpawnProfile()
    {
        ClearRunState();
        ZombieWaveSpawnProfileSO profile = AssetDatabase.LoadAssetAtPath<ZombieWaveSpawnProfileSO>(WAVE_PROFILE_PATH);
        if (profile == null)
        {
            AddMessage("웨이브 스폰 프로필을 찾을 수 없습니다: " + WAVE_PROFILE_PATH);
            FlushMessagesToConsole(false);
            return;
        }

        if (!TryReadCsv(WAVE_CSV_PATH, out List<List<string>> table))
        {
            FlushMessagesToConsole(false);
            return;
        }

        if (!TryBuildHeaderMap(table[0], GetWaveRequiredColumns(), out Dictionary<string, int> headerMap))
        {
            FlushMessagesToConsole(false);
            return;
        }

        SerializedObject serializedObject = new SerializedObject(profile);
        SerializedProperty stages = serializedObject.FindProperty("stages");
        stages.arraySize = CountDataRows(table);
        int stageIndex = 0;
        for (int i = 1; i < table.Count; i++)
        {
            List<string> row = table[i];
            if (IsEmptyCsvRow(row))
            {
                continue;
            }

            SerializedProperty stage = stages.GetArrayElementAtIndex(stageIndex);
            SetWaveStageFromRow(stage, row, headerMap, i + 1);
            stageIndex++;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AddMessage("웨이브 CSV 임포트 완료: " + WAVE_PROFILE_PATH);
        FlushMessagesToConsole(true);
    }

    // CSV 행 값을 웨이브 스테이지 직렬화 프로퍼티에 반영한다
    private void SetWaveStageFromRow(SerializedProperty stage, List<string> row, Dictionary<string, int> headerMap, int lineNumber)
    {
        stage.FindPropertyRelative("minWave").intValue = ReadInt(row, headerMap, "MinWave", lineNumber, 1);
        stage.FindPropertyRelative("maxWave").intValue = ReadInt(row, headerMap, "MaxWave", lineNumber, 0);
        stage.FindPropertyRelative("spawnInterval").floatValue = ReadFloat(row, headerMap, "SpawnInterval", lineNumber, 1.0f);
        stage.FindPropertyRelative("spawnCount").intValue = ReadInt(row, headerMap, "SpawnCount", lineNumber, 0);
        stage.FindPropertyRelative("spawnBossAsLastEnemy").boolValue = ReadBool(row, headerMap, "SpawnBossAsLastEnemy", lineNumber, false);
        stage.FindPropertyRelative("hpMultiplier").floatValue = ReadFloat(row, headerMap, "HpMultiplier", lineNumber, 1.0f);
        stage.FindPropertyRelative("attackDamageMultiplier").floatValue = ReadFloat(row, headerMap, "AttackDamageMultiplier", lineNumber, 1.0f);
        stage.FindPropertyRelative("moveAttackSpeedMultiplier").floatValue = ReadFloat(row, headerMap, "MoveAttackSpeedMultiplier", lineNumber, 1.0f);
        stage.FindPropertyRelative("rewardMultiplier").floatValue = ReadFloat(row, headerMap, "RewardMultiplier", lineNumber, 1.0f);
        SetNormalZombieEntries(stage.FindPropertyRelative("normalZombieEntries"), ReadString(row, headerMap, "NormalZombieEntries"), lineNumber);
        SetBossZombieEntries(stage.FindPropertyRelative("bossZombieEntries"), ReadString(row, headerMap, "BossZombieEntries"), lineNumber);
    }

    // 좀비 보상 프로필들을 CSV로 내보낸다
    private void ExportRewardProfiles()
    {
        ClearRunState();
        ZombieWaveSpawnProfileSO waveProfile = AssetDatabase.LoadAssetAtPath<ZombieWaveSpawnProfileSO>(WAVE_PROFILE_PATH);
        if (waveProfile == null)
        {
            AddMessage("웨이브 스폰 프로필을 찾을 수 없습니다: " + WAVE_PROFILE_PATH);
            FlushMessagesToConsole(false);
            return;
        }

        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine("ZombieType(좀비 타입),CurrencyType(재화 타입),Amount(기본 수량),DropChance(드롭 확률),MinAmountMultiplier(최소 수량 배율),MaxAmountMultiplier(최대 수량 배율)");

        int exportedCount = 0;
        exportedCount += AppendPrefabMapRewardCsvLines(builder, waveProfile, true);
        exportedCount += AppendPrefabMapRewardCsvLines(builder, waveProfile, false);

        WriteUtf8Csv(REWARD_CSV_PATH, builder.ToString());
        AddMessage($"보상 CSV 익스포트 완료: {exportedCount}개 프로필");
        FlushMessagesToConsole(true);
    }

    // 프리팹 맵을 순회하며 각 좀비 프리팹의 rewardProfileOverride를 읽어 CSV 줄로 추가한다
    private int AppendPrefabMapRewardCsvLines(StringBuilder builder, ZombieWaveSpawnProfileSO waveProfile, bool isNormal)
    {
        int count = 0;
        if (isNormal)
        {
            foreach (NormalZombieType type in Enum.GetValues(typeof(NormalZombieType)))
            {
                if (!waveProfile.TryGetNormalPrefabForType(type, out PoolObject poolObj))
                {
                    continue;
                }

                ZombieRewardProfileSO rewardProfile = GetRewardProfileFromPrefab(poolObj, true);
                if (rewardProfile == null)
                {
                    AddMessage($"보상 프로필 없음 (rewardProfileOverride 미할당): {type}");
                    continue;
                }

                AppendRewardProfileCsvLines(builder, type.ToString(), rewardProfile);
                count++;
            }
        }
        else
        {
            foreach (BossZombieType type in Enum.GetValues(typeof(BossZombieType)))
            {
                if (!waveProfile.TryGetBossPrefabForType(type, out PoolObject poolObj))
                {
                    continue;
                }

                ZombieRewardProfileSO rewardProfile = GetRewardProfileFromPrefab(poolObj, false);
                if (rewardProfile == null)
                {
                    AddMessage($"보상 프로필 없음 (rewardProfileOverride 미할당): {type}");
                    continue;
                }

                AppendRewardProfileCsvLines(builder, type.ToString(), rewardProfile);
                count++;
            }
        }

        return count;
    }

    // PoolObject 프리팹 컴포넌트의 rewardProfileOverride 필드 값을 반환한다
    private static ZombieRewardProfileSO GetRewardProfileFromPrefab(PoolObject poolObj, bool isNormal)
    {
        Component zombie = isNormal
            ? (Component)poolObj.GetComponent<NormalZombie>()
            : (Component)poolObj.GetComponent<BossZombie>();
        if (zombie == null)
        {
            return null;
        }

        SerializedObject serializedZombie = new SerializedObject(zombie);
        return serializedZombie.FindProperty("rewardProfileOverride")?.objectReferenceValue as ZombieRewardProfileSO;
    }

    // 보상 프로필 하나의 기본 보상 목록을 CSV 줄로 추가한다
    private static void AppendRewardProfileCsvLines(StringBuilder builder, string zombieTypeName, ZombieRewardProfileSO profile)
    {
        SerializedObject serializedObject = new SerializedObject(profile);
        SerializedProperty rewards = serializedObject.FindProperty("rewards");
        for (int i = 0; i < rewards.arraySize; i++)
        {
            SerializedProperty reward = rewards.GetArrayElementAtIndex(i);
            builder.Append(EscapeCsvField(zombieTypeName));
            builder.Append(',');
            builder.Append(EscapeCsvField(GetRewardCurrencyName(reward.FindPropertyRelative("currencyType").enumValueIndex)));
            builder.Append(',');
            builder.Append(reward.FindPropertyRelative("amount").intValue);
            builder.Append(',');
            builder.Append(reward.FindPropertyRelative("dropChance").floatValue);
            builder.Append(',');
            builder.Append(reward.FindPropertyRelative("minAmountMultiplier").floatValue);
            builder.Append(',');
            builder.Append(reward.FindPropertyRelative("maxAmountMultiplier").floatValue);
            builder.AppendLine();
        }
    }

    // CSV의 보상 값을 기존 보상 프로필들에 반영한다
    private void ImportRewardProfiles()
    {
        ClearRunState();
        ZombieWaveSpawnProfileSO waveProfile = AssetDatabase.LoadAssetAtPath<ZombieWaveSpawnProfileSO>(WAVE_PROFILE_PATH);
        if (waveProfile == null)
        {
            AddMessage("웨이브 스폰 프로필을 찾을 수 없습니다: " + WAVE_PROFILE_PATH);
            FlushMessagesToConsole(false);
            return;
        }

        if (!TryReadCsv(REWARD_CSV_PATH, out List<List<string>> table))
        {
            FlushMessagesToConsole(false);
            return;
        }

        if (!TryBuildHeaderMap(table[0], GetRewardRequiredColumns(), out Dictionary<string, int> headerMap))
        {
            FlushMessagesToConsole(false);
            return;
        }

        Dictionary<string, List<RewardCsvRow>> rowsByZombieType = new Dictionary<string, List<RewardCsvRow>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < table.Count; i++)
        {
            List<string> row = table[i];
            if (IsEmptyCsvRow(row))
            {
                continue;
            }

            RewardCsvRow rewardRow = ParseRewardCsvRow(row, headerMap, i + 1);
            if (!rowsByZombieType.TryGetValue(rewardRow.ZombieTypeName, out List<RewardCsvRow> rewards))
            {
                rewards = new List<RewardCsvRow>();
                rowsByZombieType.Add(rewardRow.ZombieTypeName, rewards);
            }

            rewards.Add(rewardRow);
        }

        int updatedCount = 0;
        foreach (KeyValuePair<string, List<RewardCsvRow>> pair in rowsByZombieType)
        {
            ZombieRewardProfileSO profile = ResolveRewardProfile(waveProfile, pair.Key);
            if (profile == null)
            {
                AddMessage($"보상 프로필을 찾을 수 없어 건너뜁니다: {pair.Key}");
                continue;
            }

            ApplyRewardRows(profile, pair.Value);
            updatedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AddMessage($"보상 CSV 임포트 완료: {updatedCount}개 프로필 갱신");
        FlushMessagesToConsole(true);
    }

    // 좀비 타입 이름으로 보상 프로필 SO를 찾는다 (프리팹의 rewardProfileOverride 경유)
    private static ZombieRewardProfileSO ResolveRewardProfile(ZombieWaveSpawnProfileSO waveProfile, string zombieTypeName)
    {
        if (Enum.TryParse(zombieTypeName, out NormalZombieType normalType))
        {
            if (waveProfile.TryGetNormalPrefabForType(normalType, out PoolObject poolObj))
            {
                return GetRewardProfileFromPrefab(poolObj, true);
            }
        }

        if (Enum.TryParse(zombieTypeName, out BossZombieType bossType))
        {
            if (waveProfile.TryGetBossPrefabForType(bossType, out PoolObject poolObj))
            {
                return GetRewardProfileFromPrefab(poolObj, false);
            }
        }

        return null;
    }

    // 보상 CSV 행을 데이터 구조로 변환한다
    private RewardCsvRow ParseRewardCsvRow(List<string> row, Dictionary<string, int> headerMap, int lineNumber)
    {
        RewardCsvRow result = new RewardCsvRow
        {
            ZombieTypeName = ReadString(row, headerMap, "ZombieType"),
            CurrencyType = ReadRewardCurrencyType(row, headerMap, "CurrencyType", lineNumber),
            Amount = ReadInt(row, headerMap, "Amount", lineNumber, 0),
            DropChance = ReadFloat(row, headerMap, "DropChance", lineNumber, 1.0f),
            MinAmountMultiplier = ReadFloat(row, headerMap, "MinAmountMultiplier", lineNumber, 1.0f),
            MaxAmountMultiplier = ReadFloat(row, headerMap, "MaxAmountMultiplier", lineNumber, 1.0f)
        };
        return result;
    }

    // 보상 CSV 행 목록을 보상 프로필에 반영한다
    private static void ApplyRewardRows(ZombieRewardProfileSO profile, List<RewardCsvRow> rewardRows)
    {
        SerializedObject serializedObject = new SerializedObject(profile);
        SerializedProperty rewards = serializedObject.FindProperty("rewards");
        rewards.arraySize = rewardRows.Count;
        for (int i = 0; i < rewardRows.Count; i++)
        {
            RewardCsvRow row = rewardRows[i];
            SerializedProperty reward = rewards.GetArrayElementAtIndex(i);
            reward.FindPropertyRelative("currencyType").enumValueIndex = GetRewardCurrencyIndex(row.CurrencyType);
            reward.FindPropertyRelative("amount").intValue = Mathf.Max(0, row.Amount);
            reward.FindPropertyRelative("dropChance").floatValue = Mathf.Clamp01(row.DropChance);
            reward.FindPropertyRelative("minAmountMultiplier").floatValue = Mathf.Max(0.0f, row.MinAmountMultiplier);
            reward.FindPropertyRelative("maxAmountMultiplier").floatValue = Mathf.Max(row.MinAmountMultiplier, row.MaxAmountMultiplier);
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(profile);
    }

    // 생존자 구출 스폰 프로필을 CSV로 내보낸다
    private void ExportSurvivorRescueProfile()
    {
        ClearRunState();
        SurvivorRescueSpawnProfileSO profile = AssetDatabase.LoadAssetAtPath<SurvivorRescueSpawnProfileSO>(SURVIVOR_RESCUE_PROFILE_PATH);
        if (profile == null)
        {
            AddMessage("생존자 구출 스폰 프로필을 찾을 수 없습니다: " + SURVIVOR_RESCUE_PROFILE_PATH);
            FlushMessagesToConsole(false);
            return;
        }

        SerializedObject serializedObject = new SerializedObject(profile);
        SerializedProperty stages = serializedObject.FindProperty("stages");
        StringBuilder builder = new StringBuilder(512);
        builder.AppendLine("Wave(웨이브),SpawnChance(구출 생존자 스폰 확률)");
        for (int i = 0; i < stages.arraySize; i++)
        {
            SerializedProperty stage = stages.GetArrayElementAtIndex(i);
            builder.Append(GetRelativeInt(stage, "wave"));
            builder.Append(',');
            builder.Append(GetRelativeFloat(stage, "spawnChance"));
            builder.AppendLine();
        }

        WriteUtf8Csv(SURVIVOR_RESCUE_CSV_PATH, builder.ToString());
        AddMessage("생존자 구출 CSV 익스포트 완료: " + SURVIVOR_RESCUE_CSV_PATH);
        FlushMessagesToConsole(true);
    }

    // 웨이브 CSV 파일을 기본 앱으로 연다
    private void OpenWaveCsv()
    {
        ClearRunState();
        bool isSuccess = TryOpenCsvFile(WAVE_CSV_PATH);
        FlushMessagesToConsole(isSuccess);
    }

    // 보상 CSV 파일을 기본 앱으로 연다
    private void OpenRewardCsv()
    {
        ClearRunState();
        bool isSuccess = TryOpenCsvFile(REWARD_CSV_PATH);
        FlushMessagesToConsole(isSuccess);
    }

    // 생존자 구출 CSV 파일을 기본 앱으로 연다
    private void OpenSurvivorRescueCsv()
    {
        ClearRunState();
        bool isSuccess = TryOpenCsvFile(SURVIVOR_RESCUE_CSV_PATH);
        FlushMessagesToConsole(isSuccess);
    }

    // CSV의 생존자 구출 스폰 값을 프로필에 반영한다
    private void ImportSurvivorRescueProfile()
    {
        ClearRunState();
        SurvivorRescueSpawnProfileSO profile = AssetDatabase.LoadAssetAtPath<SurvivorRescueSpawnProfileSO>(SURVIVOR_RESCUE_PROFILE_PATH);
        if (profile == null)
        {
            AddMessage("생존자 구출 스폰 프로필을 찾을 수 없습니다: " + SURVIVOR_RESCUE_PROFILE_PATH);
            FlushMessagesToConsole(false);
            return;
        }

        if (!TryReadCsv(SURVIVOR_RESCUE_CSV_PATH, out List<List<string>> table))
        {
            FlushMessagesToConsole(false);
            return;
        }

        if (!TryBuildHeaderMap(table[0], GetSurvivorRequiredColumns(), out Dictionary<string, int> headerMap))
        {
            FlushMessagesToConsole(false);
            return;
        }

        SerializedObject serializedObject = new SerializedObject(profile);
        SerializedProperty stages = serializedObject.FindProperty("stages");
        stages.arraySize = CountDataRows(table);
        int stageIndex = 0;
        for (int i = 1; i < table.Count; i++)
        {
            List<string> row = table[i];
            if (IsEmptyCsvRow(row))
            {
                continue;
            }

            SerializedProperty stage = stages.GetArrayElementAtIndex(stageIndex);
            stage.FindPropertyRelative("wave").intValue = ReadInt(row, headerMap, "Wave", i + 1, 1);
            stage.FindPropertyRelative("spawnChance").floatValue = ReadFloat(row, headerMap, "SpawnChance", i + 1, 0.0f);
            stageIndex++;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AddMessage("생존자 구출 CSV 임포트 완료: " + SURVIVOR_RESCUE_PROFILE_PATH);
        FlushMessagesToConsole(true);
    }

    // 일반 좀비 엔트리 배열을 CSV 필드 문자열로 변환한다
    private static string FormatNormalZombieEntries(SerializedProperty entries)
    {
        if (entries == null || !entries.isArray || entries.arraySize == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(256);
        for (int i = 0; i < entries.arraySize; i++)
        {
            if (i > 0)
            {
                builder.Append(';');
            }

            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            NormalZombieType zombieType = (NormalZombieType)entry.FindPropertyRelative("zombieType").enumValueIndex;
            builder.Append(zombieType.ToString());
            builder.Append('|');
            builder.Append(GetRelativeInt(entry, "weight"));
            builder.Append('|');
            builder.Append(GetRelativeInt(entry, "minWave"));
            builder.Append('|');
            builder.Append(GetRelativeInt(entry, "maxWave"));
        }

        return builder.ToString();
    }

    // 보스 좀비 엔트리 배열을 CSV 필드 문자열로 변환한다
    private static string FormatBossZombieEntries(SerializedProperty entries)
    {
        if (entries == null || !entries.isArray || entries.arraySize == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(128);
        for (int i = 0; i < entries.arraySize; i++)
        {
            if (i > 0)
            {
                builder.Append(';');
            }

            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            BossZombieType bossType = (BossZombieType)entry.FindPropertyRelative("bossType").enumValueIndex;
            builder.Append(bossType.ToString());
            builder.Append('|');
            builder.Append(GetRelativeInt(entry, "weight"));
            builder.Append('|');
            builder.Append(GetRelativeInt(entry, "minWave"));
            builder.Append('|');
            builder.Append(GetRelativeInt(entry, "maxWave"));
        }

        return builder.ToString();
    }

    // CSV 필드 문자열을 일반 좀비 엔트리 배열에 반영한다
    private void SetNormalZombieEntries(SerializedProperty entries, string entryText, int lineNumber)
    {
        if (string.IsNullOrWhiteSpace(entryText))
        {
            entries.arraySize = 0;
            return;
        }

        string[] tokens = entryText.Split(';');
        entries.arraySize = tokens.Length;
        for (int i = 0; i < tokens.Length; i++)
        {
            string[] parts = tokens[i].Split('|');
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (parts.Length != 4)
            {
                AddMessage($"{lineNumber}행: 일반 좀비 엔트리 형식이 잘못되었습니다. 값: {tokens[i]}");
                continue;
            }

            string typeName = parts[0].Trim();
            if (!System.Enum.TryParse(typeName, out NormalZombieType zombieType))
            {
                AddMessage($"{lineNumber}행: 알 수 없는 NormalZombieType: {typeName}");
            }

            entry.FindPropertyRelative("zombieType").enumValueIndex = (int)zombieType;
            entry.FindPropertyRelative("weight").intValue = ParseInt(parts[1], 0);
            entry.FindPropertyRelative("minWave").intValue = ParseInt(parts[2], 1);
            entry.FindPropertyRelative("maxWave").intValue = ParseInt(parts[3], 0);
        }
    }

    // CSV 필드 문자열을 보스 좀비 엔트리 배열에 반영한다
    private void SetBossZombieEntries(SerializedProperty entries, string entryText, int lineNumber)
    {
        if (string.IsNullOrWhiteSpace(entryText))
        {
            entries.arraySize = 0;
            return;
        }

        string[] tokens = entryText.Split(';');
        entries.arraySize = tokens.Length;
        for (int i = 0; i < tokens.Length; i++)
        {
            string[] parts = tokens[i].Split('|');
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (parts.Length != 4)
            {
                AddMessage($"{lineNumber}행: 보스 좀비 엔트리 형식이 잘못되었습니다. 값: {tokens[i]}");
                continue;
            }

            string typeName = parts[0].Trim();
            if (!System.Enum.TryParse(typeName, out BossZombieType bossType))
            {
                AddMessage($"{lineNumber}행: 알 수 없는 BossZombieType: {typeName}");
            }

            entry.FindPropertyRelative("bossType").enumValueIndex = (int)bossType;
            entry.FindPropertyRelative("weight").intValue = ParseInt(parts[1], 0);
            entry.FindPropertyRelative("minWave").intValue = ParseInt(parts[2], 1);
            entry.FindPropertyRelative("maxWave").intValue = ParseInt(parts[3], 0);
        }
    }

    // CSV를 읽고 테이블로 파싱한다
    private bool TryReadCsv(string path, out List<List<string>> table)
    {
        table = null;
        if (!File.Exists(path))
        {
            AddMessage("CSV 파일을 찾을 수 없습니다: " + path);
            return false;
        }

        if (!TryReadCsvText(path, out string csvText))
        {
            return false;
        }

        table = ParseCsv(csvText);
        if (table.Count <= 0)
        {
            AddMessage("CSV가 비어 있습니다: " + path);
            return false;
        }

        return true;
    }

    // CSV 파일을 UTF-8, UTF-16, CP949 순서로 읽는다
    private bool TryReadCsvText(string path, out string csvText)
    {
        csvText = string.Empty;
        byte[] bytes;
        try
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bytes = new byte[stream.Length];
                int totalRead = 0;
                while (totalRead < bytes.Length)
                {
                    int read = stream.Read(bytes, totalRead, bytes.Length - totalRead);
                    if (read <= 0)
                    {
                        break;
                    }

                    totalRead += read;
                }
            }
        }
        catch (IOException exception)
        {
            AddMessage("CSV 파일을 읽을 수 없습니다. Excel에서 저장 중이거나 파일을 독점 잠금 중일 수 있습니다.");
            AddMessage(exception.Message);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            AddMessage("CSV 파일 접근 권한이 없습니다: " + path);
            AddMessage(exception.Message);
            return false;
        }

        if (HasUtf8Bom(bytes))
        {
            csvText = new UTF8Encoding(true, false).GetString(bytes, 3, bytes.Length - 3);
            return true;
        }

        if (HasUtf16LittleEndianBom(bytes))
        {
            csvText = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            return true;
        }

        if (HasUtf16BigEndianBom(bytes))
        {
            csvText = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            return true;
        }

        if (TryDecodeStrictUtf8(bytes, out csvText))
        {
            return true;
        }

        if (TryDecodeCp949(bytes, out csvText))
        {
            AddMessage("CSV가 UTF-8이 아니어서 CP949(한국어 Windows CSV)로 읽었습니다: " + path);
            return true;
        }

        csvText = Encoding.Default.GetString(bytes);
        AddMessage("CSV가 UTF-8/UTF-16/CP949로 명확히 읽히지 않아 OS 기본 인코딩으로 읽었습니다: " + path);
        return true;
    }

    // CSV 헤더를 컬럼 인덱스 맵으로 변환한다
    private bool TryBuildHeaderMap(List<string> headers, string[] requiredColumns, out Dictionary<string, int> headerMap)
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

        for (int i = 0; i < requiredColumns.Length; i++)
        {
            if (!headerMap.ContainsKey(requiredColumns[i]))
            {
                AddMessage("필수 CSV 컬럼이 없습니다: " + requiredColumns[i]);
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

    // CSV 텍스트를 행과 필드 목록으로 파싱한다
    private static List<List<string>> ParseCsv(string text)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> row = new List<string>();
        StringBuilder field = new StringBuilder();
        bool inQuotes = false;
        char delimiter = DetectDelimiter(text);

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

    // CSV 텍스트의 구분자를 쉼표 또는 탭 중에서 감지한다
    private static char DetectDelimiter(string text)
    {
        int commaCount = 0;
        int tabCount = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r' || c == '\n')
            {
                break;
            }

            if (c == ',')
            {
                commaCount++;
            }
            else if (c == '\t')
            {
                tabCount++;
            }
        }

        return tabCount > commaCount ? '\t' : ',';
    }

    // CSV 필드로 안전하게 출력할 문자열로 변환한다
    private static string EscapeCsvField(string value)
    {
        string safeValue = value ?? string.Empty;
        bool needsQuote = safeValue.IndexOfAny(new[] { ',', '"', '\n', '\r', '\t' }) >= 0;
        if (!needsQuote)
        {
            return safeValue;
        }

        return "\"" + safeValue.Replace("\"", "\"\"") + "\"";
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

    // 지정한 CSV 파일을 기본 앱으로 연다
    private bool TryOpenCsvFile(string path)
    {
        if (!File.Exists(path))
        {
            AddMessage("CSV 파일을 찾을 수 없습니다: " + path);
            return false;
        }

        EditorUtility.OpenWithDefaultApp(path);
        AddMessage("CSV 파일 열기 요청: " + path);
        return true;
    }

    // CSV 행이 비어 있는지 확인한다
    private static bool IsEmptyCsvRow(List<string> fields)
    {
        for (int i = 0; i < fields.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(fields[i]))
            {
                return false;
            }
        }

        return true;
    }

    // 헤더를 제외한 실제 데이터 행 수를 센다
    private static int CountDataRows(List<List<string>> table)
    {
        int count = 0;
        for (int i = 1; i < table.Count; i++)
        {
            if (!IsEmptyCsvRow(table[i]))
            {
                count++;
            }
        }

        return count;
    }

    // CSV 행에서 문자열 값을 읽는다
    private static string ReadString(List<string> row, Dictionary<string, int> headerMap, string columnName)
    {
        int index = headerMap[columnName];
        return index >= 0 && index < row.Count ? row[index] ?? string.Empty : string.Empty;
    }

    // CSV 행에서 정수 값을 읽는다
    private int ReadInt(List<string> row, Dictionary<string, int> headerMap, string columnName, int lineNumber, int fallback)
    {
        string value = ReadString(row, headerMap, columnName);
        if (int.TryParse(value, out int result))
        {
            return result;
        }

        AddMessage($"{lineNumber}행: {columnName} 정수 값이 유효하지 않아 {fallback}을 사용합니다. 값: {value}");
        return fallback;
    }

    // CSV 행에서 실수 값을 읽는다
    private float ReadFloat(List<string> row, Dictionary<string, int> headerMap, string columnName, int lineNumber, float fallback)
    {
        string value = ReadString(row, headerMap, columnName);
        if (float.TryParse(value, out float result))
        {
            return result;
        }

        AddMessage($"{lineNumber}행: {columnName} 실수 값이 유효하지 않아 {fallback}을 사용합니다. 값: {value}");
        return fallback;
    }

    // CSV 행에서 bool 값을 읽는다
    private bool ReadBool(List<string> row, Dictionary<string, int> headerMap, string columnName, int lineNumber, bool fallback)
    {
        string value = ReadString(row, headerMap, columnName);
        if (bool.TryParse(value, out bool result))
        {
            return result;
        }

        if (int.TryParse(value, out int intValue))
        {
            return intValue != 0;
        }

        AddMessage($"{lineNumber}행: {columnName} bool 값이 유효하지 않아 {fallback}을 사용합니다. 값: {value}");
        return fallback;
    }

    // CSV 행에서 재화 enum 값을 읽는다
    private RewardCurrencyType ReadRewardCurrencyType(List<string> row, Dictionary<string, int> headerMap, string columnName, int lineNumber)
    {
        string value = ReadString(row, headerMap, columnName).Trim();
        if (Enum.TryParse(value, out RewardCurrencyType result))
        {
            return result;
        }

        AddMessage($"{lineNumber}행: RewardCurrencyType 값이 유효하지 않아 Coin을 사용합니다. 값: {value}");
        return RewardCurrencyType.Coin;
    }

    // 문자열을 정수로 변환한다
    private static int ParseInt(string value, int fallback)
    {
        return int.TryParse(value, out int result) ? result : fallback;
    }

    // 직렬화 프로퍼티의 하위 정수 값을 반환한다
    private static int GetRelativeInt(SerializedProperty property, string relativePath)
    {
        return property.FindPropertyRelative(relativePath).intValue;
    }

    // 직렬화 프로퍼티의 하위 실수 값을 반환한다
    private static float GetRelativeFloat(SerializedProperty property, string relativePath)
    {
        return property.FindPropertyRelative(relativePath).floatValue;
    }

    // 직렬화 프로퍼티의 하위 bool 값을 반환한다
    private static bool GetRelativeBool(SerializedProperty property, string relativePath)
    {
        return property.FindPropertyRelative(relativePath).boolValue;
    }

    // 재화 enum 인덱스를 이름으로 변환한다
    private static string GetRewardCurrencyName(int enumValueIndex)
    {
        string[] names = Enum.GetNames(typeof(RewardCurrencyType));
        return enumValueIndex >= 0 && enumValueIndex < names.Length ? names[enumValueIndex] : RewardCurrencyType.Coin.ToString();
    }

    // 재화 enum 값을 직렬화 enum 인덱스로 변환한다
    private static int GetRewardCurrencyIndex(RewardCurrencyType currencyType)
    {
        string[] names = Enum.GetNames(typeof(RewardCurrencyType));
        string currencyName = currencyType.ToString();
        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == currencyName)
            {
                return i;
            }
        }

        return 0;
    }

    // UTF-8 BOM이 있는지 확인한다
    private static bool HasUtf8Bom(byte[] bytes)
    {
        return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    }

    // UTF-16 LE BOM이 있는지 확인한다
    private static bool HasUtf16LittleEndianBom(byte[] bytes)
    {
        return bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE;
    }

    // UTF-16 BE BOM이 있는지 확인한다
    private static bool HasUtf16BigEndianBom(byte[] bytes)
    {
        return bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF;
    }

    // 엄격한 UTF-8 디코딩을 시도한다
    private static bool TryDecodeStrictUtf8(byte[] bytes, out string text)
    {
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }

    // CP949 디코딩을 시도한다
    private static bool TryDecodeCp949(byte[] bytes, out string text)
    {
        text = string.Empty;
        try
        {
            Encoding encoding = Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            text = encoding.GetString(bytes);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // 웨이브 CSV 필수 컬럼 목록을 반환한다
    private static string[] GetWaveRequiredColumns()
    {
        return new[]
        {
            "MinWave",
            "MaxWave",
            "SpawnInterval",
            "SpawnCount",
            "SpawnBossAsLastEnemy",
            "HpMultiplier",
            "AttackDamageMultiplier",
            "MoveAttackSpeedMultiplier",
            "RewardMultiplier",
            "NormalZombieEntries",
            "BossZombieEntries"
        };
    }

    // 보상 CSV 필수 컬럼 목록을 반환한다
    private static string[] GetRewardRequiredColumns()
    {
        return new[]
        {
            "ZombieType",
            "CurrencyType",
            "Amount",
            "DropChance",
            "MinAmountMultiplier",
            "MaxAmountMultiplier"
        };
    }

    // 생존자 구출 CSV 필수 컬럼 목록을 반환한다
    private static string[] GetSurvivorRequiredColumns()
    {
        return new[]
        {
            "Wave",
            "SpawnChance"
        };
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
            Debug.Log("[ZombieBalanceCsvEditorTool]\n" + joinedMessage);
        }
        else
        {
            Debug.LogWarning("[ZombieBalanceCsvEditorTool]\n" + joinedMessage);
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

    private struct RewardCsvRow
    {
        public string ZombieTypeName;
        public RewardCurrencyType CurrencyType;
        public int Amount;
        public float DropChance;
        public float MinAmountMultiplier;
        public float MaxAmountMultiplier;
    }
}
#endif
