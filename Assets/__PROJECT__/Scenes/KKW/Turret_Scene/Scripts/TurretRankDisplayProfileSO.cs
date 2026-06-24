using System;
using UnityEngine;

/// <summary>
/// 터렛 Definition과 티어 레벨을 3D Rank 프리팹의 랭크 인덱스로 변환하는 표시 규칙 에셋.
/// </summary>
[CreateAssetMenu(fileName = "TurretRankDisplayProfile", menuName = "Project Z Defense/Turret Rank Display Profile")]
public class TurretRankDisplayProfileSO : ScriptableObject
{
    [Header("랭크 구간")]
    [SerializeField] private TurretRankBandRange[] rankBands =
    {
        new TurretRankBandRange(TurretRankBand.Recruit, 0, 1),
        new TurretRankBandRange(TurretRankBand.Bronze, 1, 10),
        new TurretRankBandRange(TurretRankBand.Silver, 11, 10),
        new TurretRankBandRange(TurretRankBand.Gold, 21, 10),
        new TurretRankBandRange(TurretRankBand.Platinum, 31, 10),
        new TurretRankBandRange(TurretRankBand.Diamond, 41, 10),
        new TurretRankBandRange(TurretRankBand.Crimson, 51, 10),
    };

    [Header("기본 규칙")]
    [SerializeField, Min(1)] private int defaultLevelsPerRank = 10;
    [SerializeField] private TurretRankBand defaultRankBand = TurretRankBand.Recruit;
    [SerializeField] private bool useDefaultRuleWhenRuleMissing;

    [Header("터렛별 규칙")]
    [SerializeField] private TurretRankRule[] turretRules;

    public int DefaultLevelsPerRank
    {
        get
        {
            return Mathf.Max(1, defaultLevelsPerRank);
        }
    }

    // 터렛 Definition과 티어 레벨에 해당하는 3D Rank 인덱스를 계산한다
    public bool TryGetRankIndex(TurretDefinitionSO turretDefinition, int tierLevel, out int rankIndex)
    {
        rankIndex = 0;
        TurretRankRule rule = FindRule(turretDefinition);
        if (rule == null)
        {
            if (!useDefaultRuleWhenRuleMissing)
            {
                return false;
            }

            return TryCalculateRankIndex(defaultRankBand, tierLevel, DefaultLevelsPerRank, 0, out rankIndex);
        }

        return TryCalculateRankIndex(rule.RankBand, tierLevel, rule.LevelsPerRank, rule.FirstRankOffset, out rankIndex);
    }

    // 터렛 Definition에 명시적으로 연결된 랭크 규칙을 찾는다
    public TurretRankRule FindRule(TurretDefinitionSO turretDefinition)
    {
        if (turretDefinition == null || turretRules == null)
        {
            return null;
        }

        for (int i = 0; i < turretRules.Length; i++)
        {
            TurretRankRule rule = turretRules[i];
            if (rule != null && rule.TurretDefinition == turretDefinition)
            {
                return rule;
            }
        }

        return null;
    }

    // 랭크 구간과 레벨 단위로 실제 3D Rank 인덱스를 계산한다
    private bool TryCalculateRankIndex(TurretRankBand rankBand, int tierLevel, int levelsPerRank, int firstRankOffset, out int rankIndex)
    {
        rankIndex = 0;
        TurretRankBandRange bandRange = FindBandRange(rankBand);
        if (bandRange == null || bandRange.RankCount <= 0)
        {
            return false;
        }

        int safeLevelsPerRank = Mathf.Max(1, levelsPerRank);
        int safeTierLevel = Mathf.Max(1, tierLevel);
        int rankStep = Mathf.Max(0, firstRankOffset) + ((safeTierLevel - 1) / safeLevelsPerRank);
        int clampedStep = Mathf.Clamp(rankStep, 0, bandRange.RankCount - 1);
        rankIndex = bandRange.StartIndex + clampedStep;
        return true;
    }

    // 지정한 랭크 구간의 시작 인덱스와 개수를 찾는다
    private TurretRankBandRange FindBandRange(TurretRankBand rankBand)
    {
        if (rankBands == null)
        {
            return null;
        }

        for (int i = 0; i < rankBands.Length; i++)
        {
            TurretRankBandRange bandRange = rankBands[i];
            if (bandRange != null && bandRange.Band == rankBand)
            {
                return bandRange;
            }
        }

        return null;
    }

    // 인스펙터 입력값을 유효한 랭크 표시 범위로 보정한다
    private void OnValidate()
    {
        defaultLevelsPerRank = Mathf.Max(1, defaultLevelsPerRank);

        if (rankBands != null)
        {
            for (int i = 0; i < rankBands.Length; i++)
            {
                rankBands[i]?.Normalize();
            }
        }

        if (turretRules != null)
        {
            for (int i = 0; i < turretRules.Length; i++)
            {
                turretRules[i]?.Normalize(defaultLevelsPerRank);
            }
        }
    }
}

/// <summary>
/// 3D Rank 프리팹의 랭크 구간별 시작 인덱스와 개수를 정의한다.
/// </summary>
[Serializable]
public class TurretRankBandRange
{
    [SerializeField] private TurretRankBand band;
    [SerializeField, Min(0)] private int startIndex;
    [SerializeField, Min(1)] private int rankCount = 10;

    public TurretRankBand Band
    {
        get
        {
            return band;
        }
    }

    public int StartIndex
    {
        get
        {
            return Mathf.Max(0, startIndex);
        }
    }

    public int RankCount
    {
        get
        {
            return Mathf.Max(1, rankCount);
        }
    }

    // 새 랭크 구간 값을 초기화한다
    public TurretRankBandRange(TurretRankBand band, int startIndex, int rankCount)
    {
        this.band = band;
        this.startIndex = Mathf.Max(0, startIndex);
        this.rankCount = Mathf.Max(1, rankCount);
    }

    // 인스펙터 입력값을 유효한 인덱스 범위로 보정한다
    public void Normalize()
    {
        startIndex = Mathf.Max(0, startIndex);
        rankCount = Mathf.Max(1, rankCount);
    }
}

/// <summary>
/// 특정 터렛 Definition이 어떤 랭크 구간과 레벨 단위를 사용할지 정의한다.
/// </summary>
[Serializable]
public class TurretRankRule
{
    [SerializeField] private TurretDefinitionSO turretDefinition;
    [SerializeField] private TurretRankBand rankBand = TurretRankBand.Bronze;
    [SerializeField, Min(1)] private int levelsPerRank = 10;
    [SerializeField, Min(0)] private int firstRankOffset;

    public TurretDefinitionSO TurretDefinition
    {
        get
        {
            return turretDefinition;
        }
    }

    public TurretRankBand RankBand
    {
        get
        {
            return rankBand;
        }
    }

    public int LevelsPerRank
    {
        get
        {
            return Mathf.Max(1, levelsPerRank);
        }
    }

    public int FirstRankOffset
    {
        get
        {
            return Mathf.Max(0, firstRankOffset);
        }
    }

    // 인스펙터 입력값을 유효한 터렛 랭크 규칙 범위로 보정한다
    public void Normalize(int fallbackLevelsPerRank)
    {
        levelsPerRank = Mathf.Max(1, levelsPerRank <= 0 ? fallbackLevelsPerRank : levelsPerRank);
        firstRankOffset = Mathf.Max(0, firstRankOffset);
    }
}
