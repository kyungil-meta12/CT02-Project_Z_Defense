using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 웨이브 진행 중 실제 적용된 좀비 공격 피해를 타입별 DPS로 누적하는 디버그 기록기이다.
/// </summary>
public static class ZombieWaveDpsRuntimeRecorder
{
    private struct RuntimeDpsBucket
    {
        public float TotalDamage;
    }

    private static readonly Dictionary<BossZombieType, RuntimeDpsBucket> BossBuckets = new Dictionary<BossZombieType, RuntimeDpsBucket>(3);
    private static readonly List<ZombieDpsEntry> FlushEntries = new List<ZombieDpsEntry>(1);
    private static readonly List<BossZombieDpsEntry> BossFlushEntries = new List<BossZombieDpsEntry>(3);
    private static bool isEnabled;
    private static int currentWave = 1;
    private static float waveStartTime;
    private static RuntimeDpsBucket normalBucket;
    private static bool hasNormalDamage;
    private static ZombieWaveDpsMeasurementProfileSO currentProfile;

    // 새 웨이브 측정 세션을 시작한다
    public static void BeginWave(int wave, bool enableMeasurement, ZombieWaveDpsMeasurementProfileSO profile)
    {
        isEnabled = enableMeasurement && profile != null;
        currentProfile = profile;
        currentWave = Mathf.Max(1, wave);
        waveStartTime = Time.time;
        normalBucket = new RuntimeDpsBucket();
        hasNormalDamage = false;
        BossBuckets.Clear();
    }

    // 실제 적용된 일반 좀비 공격 피해량을 기록한다
    public static void RecordNormalDamage(float damage)
    {
        if (!isEnabled || damage <= 0.0f)
        {
            return;
        }

        normalBucket.TotalDamage += damage;
        hasNormalDamage = true;
    }

    // 실제 적용된 보스 좀비 공격 피해량을 타입별로 기록한다
    public static void RecordBossDamage(BossZombieType bossType, float damage)
    {
        if (!isEnabled || damage <= 0.0f)
        {
            return;
        }

        if (!BossBuckets.TryGetValue(bossType, out RuntimeDpsBucket bucket))
        {
            bucket = new RuntimeDpsBucket();
        }

        bucket.TotalDamage += damage;
        BossBuckets[bossType] = bucket;
    }

    // 기존 호출 호환성을 위해 일반/보스 평균 타입 피해량을 기록한다
    public static void RecordDamage(ZombieRewardTypeFilter zombieType, float damage)
    {
        if (zombieType == ZombieRewardTypeFilter.NormalOnly)
        {
            RecordNormalDamage(damage);
            return;
        }

        // BossOnly 평균 기록은 더 이상 사용하지 않는다.
    }

    // 현재 웨이브 측정 결과를 프로필에 저장하고 세션을 종료한다
    public static void CompleteWave(int wave)
    {
        if (!isEnabled || currentProfile == null || (!hasNormalDamage && BossBuckets.Count <= 0))
        {
            return;
        }

        int safeWave = Mathf.Max(1, wave);
        if (safeWave != currentWave)
        {
            safeWave = currentWave;
        }

        float elapsedSeconds = Mathf.Max(0.01f, Time.time - waveStartTime);
        FlushEntries.Clear();
        BossFlushEntries.Clear();
        AddNormalFlushEntry(elapsedSeconds);
        AddBossFlushEntry(BossZombieType.Boomer, elapsedSeconds);
        AddBossFlushEntry(BossZombieType.Screamer, elapsedSeconds);
        AddBossFlushEntry(BossZombieType.Tank, elapsedSeconds);

        if (FlushEntries.Count > 0 || BossFlushEntries.Count > 0)
        {
            currentProfile.SetWaveDps(safeWave, FlushEntries, BossFlushEntries);
        }
    }

    // 일반 좀비 누적 피해를 DPS 엔트리로 변환한다
    private static void AddNormalFlushEntry(float elapsedSeconds)
    {
        if (!hasNormalDamage || normalBucket.TotalDamage <= 0.0f)
        {
            return;
        }

        FlushEntries.Add(new ZombieDpsEntry(ZombieRewardTypeFilter.NormalOnly, normalBucket.TotalDamage / elapsedSeconds));
    }

    // 보스 좀비 누적 피해를 DPS 엔트리로 변환한다
    private static void AddBossFlushEntry(BossZombieType bossType, float elapsedSeconds)
    {
        if (!BossBuckets.TryGetValue(bossType, out RuntimeDpsBucket bucket) || bucket.TotalDamage <= 0.0f)
        {
            return;
        }

        BossFlushEntries.Add(new BossZombieDpsEntry(bossType, bucket.TotalDamage / elapsedSeconds));
    }
}
