using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 웨이브별 좀비 타입 DPS 측정 결과를 저장하는 디버그용 프로필이다.
/// </summary>
[CreateAssetMenu(fileName = "ZombieWaveDpsMeasurementProfile", menuName = "Project Z Defense/Zombie Wave DPS Measurement Profile")]
public class ZombieWaveDpsMeasurementProfileSO : ScriptableObject
{
    [Header("웨이브별 DPS 샘플")]
    [SerializeField] private List<WaveZombieDpsSample> samples = new List<WaveZombieDpsSample>();

    public IReadOnlyList<WaveZombieDpsSample> Samples => samples;

    // 지정 웨이브와 좀비 타입의 측정 DPS를 찾는다
    public bool TryGetDps(int wave, ZombieRewardTypeFilter zombieType, out float dps)
    {
        int safeWave = Mathf.Max(1, wave);
        for (int i = 0; i < samples.Count; i++)
        {
            WaveZombieDpsSample sample = samples[i];
            if (sample.Wave != safeWave)
            {
                continue;
            }

            return sample.TryGetDps(zombieType, out dps);
        }

        dps = 0.0f;
        return false;
    }

    // 지정 웨이브의 타입별 DPS 목록을 갱신한다
    public void SetWaveDps(int wave, List<ZombieDpsEntry> entries)
    {
        int safeWave = Mathf.Max(1, wave);
        WaveZombieDpsSample targetSample = null;
        for (int i = 0; i < samples.Count; i++)
        {
            if (samples[i].Wave == safeWave)
            {
                targetSample = samples[i];
                break;
            }
        }

        if (targetSample == null)
        {
            targetSample = new WaveZombieDpsSample(safeWave);
            samples.Add(targetSample);
        }

        targetSample.SetEntries(entries);
        SortSamples();
        MarkDirty();
    }

    // 웨이브 순서대로 샘플을 정렬한다
    private void SortSamples()
    {
        samples.Sort(CompareSamples);
    }

    // 샘플 웨이브 번호를 비교한다
    private static int CompareSamples(WaveZombieDpsSample left, WaveZombieDpsSample right)
    {
        if (left == null && right == null)
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        return left.Wave.CompareTo(right.Wave);
    }

    // 에디터에서 측정 프로필 변경 사항을 저장 대상으로 표시한다
    private void MarkDirty()
    {
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
#endif
    }
}

[Serializable]
public sealed class WaveZombieDpsSample
{
    [SerializeField] private int wave = 1;
    [SerializeField] private List<ZombieDpsEntry> zombieDpsEntries = new List<ZombieDpsEntry>();

    public int Wave => wave;
    public IReadOnlyList<ZombieDpsEntry> ZombieDpsEntries => zombieDpsEntries;

    // 직렬화용 기본 샘플을 생성한다
    public WaveZombieDpsSample()
    {
    }

    // 지정 웨이브 샘플을 생성한다
    public WaveZombieDpsSample(int wave)
    {
        this.wave = Mathf.Max(1, wave);
    }

    // 지정 좀비 타입의 DPS를 찾는다
    public bool TryGetDps(ZombieRewardTypeFilter zombieType, out float dps)
    {
        for (int i = 0; i < zombieDpsEntries.Count; i++)
        {
            ZombieDpsEntry entry = zombieDpsEntries[i];
            if (entry.ZombieType == zombieType)
            {
                dps = entry.Dps;
                return dps > 0.0f;
            }
        }

        dps = 0.0f;
        return false;
    }

    // 타입별 DPS 목록을 교체한다
    public void SetEntries(List<ZombieDpsEntry> entries)
    {
        zombieDpsEntries.Clear();
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            ZombieDpsEntry entry = entries[i];
            if (entry.ZombieType == ZombieRewardTypeFilter.Any || entry.Dps <= 0.0f)
            {
                continue;
            }

            zombieDpsEntries.Add(entry);
        }
    }
}

[Serializable]
public struct ZombieDpsEntry
{
    [SerializeField] private ZombieRewardTypeFilter zombieType;
    [SerializeField] private float dps;

    public ZombieRewardTypeFilter ZombieType => zombieType;
    public float Dps => dps;

    // 타입별 DPS 엔트리를 생성한다
    public ZombieDpsEntry(ZombieRewardTypeFilter zombieType, float dps)
    {
        this.zombieType = zombieType;
        this.dps = Mathf.Max(0.0f, dps);
    }
}
