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

    private static readonly Dictionary<ZombieRewardTypeFilter, RuntimeDpsBucket> Buckets = new Dictionary<ZombieRewardTypeFilter, RuntimeDpsBucket>(2);
    private static readonly List<ZombieDpsEntry> FlushEntries = new List<ZombieDpsEntry>(2);
    private static bool isEnabled;
    private static int currentWave = 1;
    private static float waveStartTime;
    private static ZombieWaveDpsMeasurementProfileSO currentProfile;

    // 새 웨이브 측정 세션을 시작한다
    public static void BeginWave(int wave, bool enableMeasurement, ZombieWaveDpsMeasurementProfileSO profile)
    {
        isEnabled = enableMeasurement && profile != null;
        currentProfile = profile;
        currentWave = Mathf.Max(1, wave);
        waveStartTime = Time.time;
        Buckets.Clear();
    }

    // 실제 적용된 좀비 공격 피해량을 기록한다
    public static void RecordDamage(ZombieRewardTypeFilter zombieType, float damage)
    {
        if (!isEnabled || zombieType == ZombieRewardTypeFilter.Any || damage <= 0.0f)
        {
            return;
        }

        if (!Buckets.TryGetValue(zombieType, out RuntimeDpsBucket bucket))
        {
            bucket = new RuntimeDpsBucket();
        }

        bucket.TotalDamage += damage;
        Buckets[zombieType] = bucket;
    }

    // 현재 웨이브 측정 결과를 프로필에 저장하고 세션을 종료한다
    public static void CompleteWave(int wave)
    {
        if (!isEnabled || currentProfile == null || Buckets.Count <= 0)
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
        AddFlushEntry(ZombieRewardTypeFilter.NormalOnly, elapsedSeconds);
        AddFlushEntry(ZombieRewardTypeFilter.BossOnly, elapsedSeconds);

        if (FlushEntries.Count > 0)
        {
            currentProfile.SetWaveDps(safeWave, FlushEntries);
        }
    }

    // 지정 타입의 누적 피해를 DPS 엔트리로 변환한다
    private static void AddFlushEntry(ZombieRewardTypeFilter zombieType, float elapsedSeconds)
    {
        if (!Buckets.TryGetValue(zombieType, out RuntimeDpsBucket bucket) || bucket.TotalDamage <= 0.0f)
        {
            return;
        }

        FlushEntries.Add(new ZombieDpsEntry(zombieType, bucket.TotalDamage / elapsedSeconds));
    }
}
