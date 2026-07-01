using System.Collections.Generic;
using UnityEngine;

// 터렛 웨이브 밸런스 리포트의 입력 스냅샷 데이터.
internal sealed class TurretBalanceInputSnapshot
{
    public int InitialWalletCoin;
    public int WaveClearCoinBonusPercentage;
    public readonly List<TurretShopEntrySO> ShopEntries = new List<TurretShopEntrySO>();
    public readonly List<WaveProfileInput> WaveProfiles = new List<WaveProfileInput>();
    public readonly List<ReportWarning> Warnings = new List<ReportWarning>();
}

// 웨이브 스폰 프로필 입력 데이터.
internal sealed class WaveProfileInput
{
    public string Path = string.Empty;
    public readonly List<WaveStageInput> Stages = new List<WaveStageInput>();
}

// 웨이브 스폰 스테이지 입력 데이터.
internal sealed class WaveStageInput
{
    public int MinWave;
    public int MaxWave;
    public float SpawnInterval;
    public int SpawnCount;
    public bool SpawnBossAsLastEnemy;
    public float HpMultiplier;
    public float AttackDamageMultiplier;
    public float MoveAttackSpeedMultiplier;
    public float RewardMultiplier;
    public readonly List<SpawnEntryInput> NormalEntries = new List<SpawnEntryInput>();
    public readonly List<SpawnEntryInput> BossEntries = new List<SpawnEntryInput>();
}

// 웨이브 스폰 후보 입력 데이터.
internal struct SpawnEntryInput
{
    public UnityEngine.Object PrefabReference;
    public int Weight;
    public int MinWave;
    public int MaxWave;
    public GameObject Prefab;
    public NormalZombie NormalZombie;
    public BossZombie BossZombie;
    public ScriptableObject SourceSpec;
    public ZombieRewardProfileSO RewardProfileOverride;
}

// 계산 완료된 터렛 웨이브 밸런스 리포트 결과.
internal sealed class TurretBalanceReportResult
{
    public int InitialWalletCoin;
    public readonly List<WaveSummaryRow> WaveRows = new List<WaveSummaryRow>();
    public readonly List<WaveClearSimulationRow> WaveClearRows = new List<WaveClearSimulationRow>();
    public readonly List<int> ScenarioReferenceLevels = new List<int>();
    public readonly List<TurretSpeciesDetailRow> SpeciesDetailRows = new List<TurretSpeciesDetailRow>();
    public readonly List<ReportWarning> Warnings = new List<ReportWarning>();
}

// 리포트 DPS 계산에서 다수 대상 기대값과 관통 효율을 공유하기 위한 설정값.
internal struct TurretBalanceDpsSettings
{
    public float FrostExpectedTargetCount;
    public float PoisonExpectedTargetCount;
    public float ElectroExpectedTargetCount;
    public float IgnitionExpectedTargetCount;
    public float ElectroOverloadTriggerExpectation;
    public float PierceDpsMultiplierPerCount;
}

// 원천 데이터 경고의 심각도. 계산 결과 신뢰도에 영향을 주면 Warning, 의도된 대체/생략 안내면 Info.
internal enum ReportWarningSeverity
{
    Info,
    Warning
}

// 원천 데이터 경고 또는 계산 비고.
internal struct ReportWarning
{
    public ReportWarningSeverity Severity;
    public string SourceType;
    public string AssetPath;
    public string Note;

    // 경고 목록에 원천 데이터 경고를 추가한다
    public static void Add(List<ReportWarning> warnings, ReportWarningSeverity severity, string sourceType, string assetPath, string note)
    {
        warnings.Add(new ReportWarning
        {
            Severity = severity,
            SourceType = sourceType,
            AssetPath = assetPath,
            Note = note
        });
    }
}

// 화면과 CSV에 공유되는 문자열 표 모델.
internal sealed class ReportTableModel
{
    public string FileName = string.Empty;
    public string InfoText = string.Empty;
    public string[] Headers = System.Array.Empty<string>();
    public float[] ColumnWidths = System.Array.Empty<float>();
    public float HeaderHeight = 20.0f;
    public readonly List<string[]> Rows = new List<string[]>(512);
    public readonly List<float> RowHeights = new List<float>(512);

    // 표 모델을 새 헤더와 설명으로 초기화한다
    public void Reset(string fileName, string infoText, params string[] headers)
    {
        FileName = fileName ?? string.Empty;
        InfoText = infoText ?? string.Empty;
        Headers = headers ?? System.Array.Empty<string>();
        HeaderHeight = 20.0f;
        Rows.Clear();
        RowHeights.Clear();

        if (ColumnWidths.Length != Headers.Length)
        {
            ColumnWidths = new float[Headers.Length];
        }
    }

    // 표 모델에 문자열 행을 추가한다
    public void AddRow(params string[] columns)
    {
        Rows.Add(columns ?? System.Array.Empty<string>());
    }
}

// 웨이브별 계산용 요약 행 데이터. 재화별 보상은 Dictionary로 보관하고, Coin 예산만 터렛 시뮬레이션이 바로 쓸 수 있도록 별도 필드로 둔다.
internal struct WaveSummaryRow
{
    public string ProfilePath;
    public string WaveLabel;
    public int MinWave;
    public int MaxWave;
    public float SpawnInterval;
    public int SpawnCount;
    public int NormalSpawnCount;
    public int BossSpawnCount;
    public float HpMultiplier;
    public float RewardMultiplier;
    public int CandidateCount;
    public float AverageZombieHp;
    public float AverageNormalZombieHp;
    public float AverageBossZombieHp;
    public float TotalWaveHp;
    public int InitialWalletCoin;
    public Dictionary<RewardCurrencyType, float> AverageRewardPerWave;
    public Dictionary<RewardCurrencyType, float> CumulativeReward;
    public float AvailableBudgetCoin;
    public float AverageNormalZombieDps;
}

// 웨이브 클리어 시뮬레이션 표 한 순위 항목의 터렛/설치 수/레벨/총 DPS.
internal struct WaveClearRankEntry
{
    public string TurretName;
    public int InstallCount;
    public int Level;
    public float TotalDps;
}

// 웨이브 클리어 시뮬레이션 표의 행 데이터. 재화별 보상은 Dictionary로 보관해 Coin 외 재화가 추가돼도 그대로 표시된다.
internal struct WaveClearSimulationRow
{
    public string WaveLabel;
    public int NormalSpawnCount;
    public int BossSpawnCount;
    public float TotalWaveHp;
    public Dictionary<RewardCurrencyType, float> AverageRewardPerWave;
    public Dictionary<RewardCurrencyType, float> CumulativeReward;
    public List<WaveClearRankEntry> TopRanks;
    public List<WaveClearRankEntry> SpeciesEntries;
    public float BestClearSeconds;
    public string Note;
}

// 진화 그래프에서 도달 가능한 터렛 종류 하나의 단계/누적 비용 정보. 누적 비용은 재화 종류별로 구분해 보관한다.
internal sealed class TurretEvolutionNode
{
    public TurretDefinitionSO Definition;
    public TurretShopEntrySO RootShopEntry;
    public int Tier;
    public bool IsTerminal;
    public int RequiredEvolutionLevel;
    public Dictionary<RewardCurrencyType, int> CumulativeReachCost;
    public Dictionary<RewardCurrencyType, int> UpgradeCostToRequiredLevel;
    // 루트 첫 대 설치비를 제외한, 진화/조상 업그레이드 비용(설치 대수가 늘어도 동일하게 적용되는 부분).
    public int NonRootCoinCost;
}

// 기준 레벨 하나에서 단일 설치 기준으로 그 레벨까지 올리는 데 드는 재화별 누적 비용/DPS와, Coin 기준 도달 웨이브 샘플.
internal struct TurretLevelCostSample
{
    public int Level;
    public bool LevelAvailable;
    public Dictionary<RewardCurrencyType, int> CumulativeCost;
    public float Dps;
    public bool WaveReached;
    public int Wave;
}

// 터렛 시나리오 상세 표의 행 데이터. 터렛 종류(진화 그래프 노드) 하나가 다음 단계로 진화하는 시점/비용과 기준 레벨별 누적 비용·DPS·도달 웨이브를 담는다.
internal struct TurretSpeciesDetailRow
{
    public string TurretName;
    public int Tier;
    public bool HasNextEvolution;
    public bool NextEvolutionReached;
    public int NextEvolutionWave;
    public Dictionary<RewardCurrencyType, int> NextEvolutionCumulativeCost;
    public List<TurretLevelCostSample> LevelSamples;
}
