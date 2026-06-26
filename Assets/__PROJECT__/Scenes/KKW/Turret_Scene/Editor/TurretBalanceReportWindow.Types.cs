using System.Collections.Generic;
using UnityEngine;

// 터렛 웨이브 밸런스 리포트의 입력 스냅샷 데이터.
internal sealed class TurretBalanceInputSnapshot
{
    public int InitialWalletCoin;
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
    public int SpawnCount;
    public bool SpawnBossAsLastEnemy;
    public float HpMultiplier;
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
    public readonly List<TurretScenarioDetailRow> ScenarioDetailRows = new List<TurretScenarioDetailRow>();
    public readonly List<ReportWarning> Warnings = new List<ReportWarning>();
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
    public readonly List<string[]> Rows = new List<string[]>(512);

    // 표 모델을 새 헤더와 설명으로 초기화한다
    public void Reset(string fileName, string infoText, params string[] headers)
    {
        FileName = fileName ?? string.Empty;
        InfoText = infoText ?? string.Empty;
        Headers = headers ?? System.Array.Empty<string>();
        Rows.Clear();

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

// 웨이브별 계산용 요약 행 데이터.
internal struct WaveSummaryRow
{
    public string ProfilePath;
    public string WaveLabel;
    public int MinWave;
    public int MaxWave;
    public int SpawnCount;
    public int NormalSpawnCount;
    public int BossSpawnCount;
    public float HpMultiplier;
    public float RewardMultiplier;
    public int CandidateCount;
    public float AverageZombieHp;
    public float TotalWaveHp;
    public int InitialWalletCoin;
    public float AverageCoinPerWave;
    public float CumulativeWaveRewardCoin;
    public float AvailableBudgetCoin;
}

// 웨이브 클리어 시뮬레이션 표의 행 데이터.
internal struct WaveClearSimulationRow
{
    public string WaveLabel;
    public int SpawnCount;
    public int NormalSpawnCount;
    public int BossSpawnCount;
    public float AverageZombieHp;
    public float TotalWaveHp;
    public int InitialWalletCoin;
    public float AverageCoinPerWave;
    public float CumulativeWaveRewardCoin;
    public float AvailableBudgetCoin;
    public string BestTurretName;
    public int BestInstallCount;
    public string BestLevelText;
    public float BestTotalDps;
    public float BestClearSeconds;
    public string Note;
}

// 터렛 시나리오 상세 표의 행 데이터. 시나리오(1대 집중/최대 설치/최적)별 전투력과 경제 사용 내역을 한 행에 담는다.
internal struct TurretScenarioDetailRow
{
    public string WaveLabel;
    public string TurretName;
    public string ScenarioName;
    public int InstallCount;
    public string LevelSummary;
    public int TotalLevel;
    public float TotalDps;
    public float ClearSeconds;
    public int BudgetCoin;
    public int PlacementCost;
    public int UpgradeCost;
    public int RemainingCoin;
    public int NextUpgradeShortage;
    public string Note;
}

// 시뮬레이션 내부 계산 결과 데이터.
internal struct SimulationResult
{
    public string TurretName;
    public string ScenarioName;
    public int InstallCount;
    public int TotalLevel;
    public float AverageLevel;
    public string LevelSummary;
    public int MaxLevel;
    public int PlacementCost;
    public int UpgradeCost;
    public int RemainingCoin;
    public int NextUpgradeShortage;
    public float TotalDps;
    public float ClearSeconds;
    public string Note;
}
