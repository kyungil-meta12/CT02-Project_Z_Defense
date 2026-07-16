using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
[CustomEditor(typeof(GameManager))] 
public class WaveController : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GameManager script = (GameManager)target;
        GUILayout.Space(10);
        if (GUILayout.Button("웨이브 증가"))
        {
            script.InvokeIncreaseWave();
        }
    }
}
#endif

/// <summary>
/// 웨이브 진행, 게임 배속, 방어선 상태, 생존자/장애물 등록을 관리한다.
/// </summary>
public class GameManager : MonoBehaviour, ISaveable
{

    private const int DEFAULT_DEFENSE_LINE_COUNT = 4;
    private const float DEFAULT_FIXED_DELTA_TIME = 0.02f;
    private const float MIN_TIME_SCALE = 0.01f;
    private const float MIN_FIXED_DELTA_TIME = 0.001f;

    [Serializable]
    // 방어선 슬롯과 대피/복귀 지점 설정을 묶어 관리한다
    private class DefenseLineEntry
    {
        [Header("방어선 설정")]
        public string lineName;
        public List<ObstacleBuildSlot> obstacleSlots = new List<ObstacleBuildSlot>(3);
        [Header("터렛 베이스")]
        public List<TurretBaseSlot> turretBaseSlots = new List<TurretBaseSlot>(3);
        [Tooltip("도망 포인트")]
        public Transform retreatPoint;
        [Tooltip("복귀 포인트")]
        public Transform restoredPoint;

        [NonSerialized] public bool isBreached;
    }

    public static GameManager Inst;

    // SaveManager 저장 파일 안에서 웨이브 데이터를 구분하는 키
    public string SaveKey => "Wave";

    [Header("방어선")]
    [SerializeField] private List<DefenseLineEntry> defenseLines = new List<DefenseLineEntry>(DEFAULT_DEFENSE_LINE_COUNT);

    [Header("게임오버")]
    [SerializeField] private GameOverPanelUI gameOverPanelUI;
    [SerializeField, Min(0.0f)] private float gameOverFadeInDuration = 10.0f;
    [SerializeField, Min(0.0f)] private float gameOverFadeOutDuration = 10.0f;

    [Header("게임 종료")]
    [SerializeField, Min(0.0f)] private float quitFadeDuration = 2.0f;
    [SerializeField] private string quitTitleMessage = "게임 종료";
    [SerializeField] private string quitStatusMessage = "";

    public event Action<int> OnWaveIncrease; // 웨이브 증가 이벤트
    public event Action<int> OnWaveDecrease; // 웨이브 감소 이벤트
    public event Action<int> OnFirstWaveReached; // 웨이브 최초 도달 이벤트
    public event Action OnObstaclePlacementsRestored; // 저장 장애물 복원 후 비용 UI 갱신 이벤트
    private readonly List<Survivor> survivors = new List<Survivor>(16);
    private readonly List<SurvivorSaveEntry> pendingSurvivorRestoreEntries = new List<SurvivorSaveEntry>(16);
    private readonly List<ObstaclePlacementSaveEntry> pendingObstacleRestoreEntries = new List<ObstaclePlacementSaveEntry>(16);
    private readonly List<TurretPlacementSaveEntry> pendingTurretRestoreEntries = new List<TurretPlacementSaveEntry>(16);
    private readonly List<TurretPlacementCountSaveEntry> pendingTurretPlacementCountEntries = new List<TurretPlacementCountSaveEntry>(8);
    private readonly Dictionary<string, ObstacleBuildEntrySO> obstacleBuildEntriesBySaveId = new Dictionary<string, ObstacleBuildEntrySO>(4);
    private readonly HashSet<string> invalidObstacleBuildEntrySaveIds = new HashSet<string>();
    private readonly Dictionary<string, TurretDefinitionSO> turretDefinitionsById = new Dictionary<string, TurretDefinitionSO>(32);
    private readonly HashSet<string> invalidTurretDefinitionIds = new HashSet<string>();
    private readonly HashSet<TurretDefinitionSO> registeredTurretDefinitions = new HashSet<TurretDefinitionSO>();
    private readonly Dictionary<string, TurretShopEntrySO> turretShopEntriesBySaveId = new Dictionary<string, TurretShopEntrySO>(8);
    private readonly HashSet<string> invalidTurretShopEntrySaveIds = new HashSet<string>();
    private readonly List<ZombieSpawner> zombieSpawners = new List<ZombieSpawner>(2);
    private TurretPlacementController turretPlacementController;
    private Coroutine gameOverCoroutine;
    private Coroutine quitGameCoroutine;
    private bool isWaveProgressionPaused;
    private bool suppressDefenseLineRestore;
    private bool isRestoringSurvivors;
    private bool isRestoringObstacles;
    private bool isRestoringTurrets;
    private bool hasStartedTurretRestorePhase;

    public int Wave{ get; private set; } = 1;
    public int HighestReachedWave { get; private set; }
    public int KillCount{ get; private set; }= 0; // 현재 킬 카운트
    public int DestKillCount{ get; private set; } = 0; // 목표 킬 카운트

    [Header("시작 웨이브")] [Min(1)] public int startWave = 1;
    [Header("웨이브 클리어 보너스")] [Min(0)] public int waveClearCoinBonusPercentage = 20; // 웨이브 클리어 시 웨이브 동안 모은 코인의 이 퍼센트만큼을 보너스로 지급
    [Header("게임 배속")]
    [SerializeField, Min(MIN_TIME_SCALE)] private float startTimeScale = 1f;
    [SerializeField, HideInInspector] private float baseFixedDeltaTime = DEFAULT_FIXED_DELTA_TIME;
    [Header("좀비 DPS 디버그")]
    [SerializeField] private bool enableZombieDpsMeasurement;
    [SerializeField] private ZombieWaveDpsMeasurementProfileSO zombieDpsMeasurementProfile;

    public float StartTimeScale => startTimeScale;
    public float CurrentTimeScale => Time.timeScale;
    public bool IsWaveProgressionPaused => isWaveProgressionPaused;

    public InputAction backAction;

    // 배속 기능 사용 여부
    public bool isTimeFastMode{ get; private set; } = false;

    // 인스펙터 값이 유효 범위를 벗어나지 않도록 보정한다
    private void OnValidate()
    {
        startWave = Mathf.Max(1, startWave);
        waveClearCoinBonusPercentage = Mathf.Max(0, waveClearCoinBonusPercentage);
        startTimeScale = Mathf.Max(MIN_TIME_SCALE, startTimeScale);
        baseFixedDeltaTime = Mathf.Max(MIN_FIXED_DELTA_TIME, baseFixedDeltaTime);
        EnsureDefaultDefenseLineEntries();
    }

    // 싱글톤을 초기화하고 시작 웨이브와 게임 배속을 적용한다
    private void Awake()
    {
        if(Inst && Inst != this)
        {
            Destroy(gameObject);
            return;
        }

        Inst = this;

        Wave = startWave;
        HighestReachedWave = Mathf.Max(0, Wave - 1);
        SaveManager.Inst.Register(this);
        SetGameTimeScale(startTimeScale);
        BeginZombieDpsMeasurementWave();

        DontDestroyOnLoad(gameObject);
        EnsureDefaultDefenseLineEntries();
    }

    // 시작 시 프레임 설정과 방어선 초기 상태 계산을 준비한다
    private void Start()
    {
        // 수직동기화 해제
        QualitySettings.vSyncCount = 0;

        // 기기의 현재 화면 주사율을 받아와 타겟 프레임으로 설정
        Application.targetFrameRate = (int)Screen.currentResolution.refreshRateRatio.value;

        print($"디바이스 주사율: {Application.targetFrameRate}hz | 주사율 적용됨");

        StartCoroutine(InitializeDefenseLineStatesAfterSlotRegistration());
    }

    // 싱글톤 인스턴스가 제거될 때 정적 참조를 정리한다
    private void OnDestroy()
    {
        if (Inst == this)
        {
            if (SaveManager.Inst != null)
            {
                SaveManager.Inst.Unregister(this);
            }

            Inst = null;
        }
    }

    // 목표 킬 수 달성 여부를 확인하고 다음 웨이브로 진행한다
    private void Update()
    {
        if (isWaveProgressionPaused)
        {
            return;
        }

        // 킬 카운트가 목표 킬 카운트에 도달할 시 웨이브를 증가시킨다
        if(DestKillCount > 0 && KillCount == DestKillCount)
        {
            InvokeIncreaseWave();
        }
    }

    // 웨이브 성공 보상과 장애물 전체 복구를 적용한 뒤 다음 웨이브를 시작한다
    public void InvokeIncreaseWave()
    {
        CompleteZombieDpsMeasurementWave();
        RebuildAllDefenseLines(true);
        KillCount = 0;
        Wave++;
        bool isFirstWaveReached = TryMarkWaveAsReached(Wave);
        SaveManager.Inst?.MarkDirty();
        InventorySystem.Inst.AddCoinBouns(waveClearCoinBonusPercentage);
        OnWaveIncrease?.Invoke(Wave);
        OnWaveIncrease?.Invoke(Wave);
        if (isFirstWaveReached)
        {
            OnFirstWaveReached?.Invoke(Wave);
        }
        BeginZombieDpsMeasurementWave();
    }

    // 지정 웨이브가 최초 도달인지 확인하고 최고 도달 웨이브를 갱신한다
    public bool TryMarkWaveAsReached(int wave)
    {
        int safeWave = Mathf.Max(1, wave);
        if (safeWave <= HighestReachedWave)
        {
            return false;
        }

        HighestReachedWave = safeWave;
        SaveManager.Inst?.MarkDirty();
        return true;
    }

    /// <summary>
    /// 시간 배속 기능 사용을 설정한다.
    /// </summary>
    /// <param name="flag"></param>
    public void SetTimeSpeedMode(bool flag)
    {
        isTimeFastMode = flag;
        Time.timeScale = flag ? 2f : 1f;
    }

    /// <summary>
    /// 목표 킬 카운트를 입력한다.
    /// ZombieSpawner에서 호출
    /// </summary>
    /// <param name="val"></param>
    // 목표 킬 카운트를 현재 웨이브 목표로 저장한다
    public void InputDestKillCount(int val)
    {
        DestKillCount = val;
    }

    /// <summary>
    /// 현재 킬 카운트를 1 증가시킨다.
    /// </summary>
    // 현재 웨이브의 처치 수를 증가시킨다
    public void IncreaseKillCount()
    {
        KillCount++;
    }

    // 게임 배속과 FixedUpdate 기준 간격을 함께 적용한다
    public void SetGameTimeScale(float timeScale)
    {
        startTimeScale = Mathf.Max(MIN_TIME_SCALE, timeScale);
        baseFixedDeltaTime = Mathf.Max(MIN_FIXED_DELTA_TIME, baseFixedDeltaTime);
        Time.timeScale = startTimeScale;
        Time.fixedDeltaTime = baseFixedDeltaTime * startTimeScale;
    }

    // 생존자를 방어선 이벤트 수신 대상으로 등록한다
    public void RegisterSurvivor(Survivor survivor)
    {
        if (survivor == null || survivors.Contains(survivor))
        {
            return;
        }

        survivors.Add(survivor);
        MarkSurvivorStateDirty();
    }

    // 생존자를 방어선 이벤트 수신 대상에서 해제한다
    public void UnregisterSurvivor(Survivor survivor)
    {
        if (survivor == null)
        {
            return;
        }

        survivors.Remove(survivor);
    }

    // 생존자 명단이나 역할 변경을 저장 대상으로 표시한다
    public void MarkSurvivorStateDirty()
    {
        if (!isRestoringSurvivors)
        {
            SaveManager.Inst?.MarkDirty();
        }
    }

    // 복원 대기 중인 생존자 저장 항목 수를 반환한다
    public int GetPendingSurvivorRestoreCount()
    {
        return pendingSurvivorRestoreEntries.Count;
    }

    // 지정 인덱스의 생존자 복원 항목을 반환한다
    public bool TryGetPendingSurvivorRestoreEntry(int index, out SurvivorSaveEntry saveEntry)
    {
        if (index < 0 || index >= pendingSurvivorRestoreEntries.Count)
        {
            saveEntry = null;
            return false;
        }

        saveEntry = pendingSurvivorRestoreEntries[index];
        return saveEntry != null;
    }

    // 생존자 생성 중 등록 이벤트가 저장을 다시 더럽히지 않도록 복원 상태를 시작한다
    public void BeginSurvivorRestore()
    {
        isRestoringSurvivors = true;
    }

    // 생존자 복원을 완료하고 캐시된 저장 명단을 비운다
    public void CompleteSurvivorRestore()
    {
        pendingSurvivorRestoreEntries.Clear();
        isRestoringSurvivors = false;
    }

    // 좀비 스포너를 게임오버 리셋 대상으로 등록한다
    public void RegisterZombieSpawner(ZombieSpawner zombieSpawner)
    {
        if (zombieSpawner == null || zombieSpawners.Contains(zombieSpawner))
        {
            return;
        }

        zombieSpawners.Add(zombieSpawner);
    }

    // 좀비 스포너를 게임오버 리셋 대상에서 해제한다
    public void UnregisterZombieSpawner(ZombieSpawner zombieSpawner)
    {
        if (zombieSpawner == null)
        {
            return;
        }

        zombieSpawners.Remove(zombieSpawner);
    }

    // 장애물이 속한 설치 슬롯을 슬롯 기준 방어선 목록과 동기화한다
    public void RegisterObstacle(Obstacle obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        ObstacleBuildSlot slot = obstacle.GetComponentInParent<ObstacleBuildSlot>();
        if (slot == null)
        {
            return;
        }

        slot.SetCurrentObstacle(obstacle);
        RegisterDefenseLineSlot(slot);
    }

    // 장애물이 제거될 때 설치 슬롯 점유 상태를 해제한다
    public void UnregisterObstacle(Obstacle obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        ObstacleBuildSlot slot = FindDefenseLineSlot(obstacle);
        if (slot == null)
        {
            slot = obstacle.GetComponentInParent<ObstacleBuildSlot>();
        }

        if (slot != null)
        {
            slot.ClearCurrentObstacle(obstacle);
        }
    }

    // 방어선 설치 슬롯을 등록한다
    public void RegisterDefenseLineSlot(ObstacleBuildSlot slot)
    {
        if (slot == null)
        {
            return;
        }

        EnsureDefaultDefenseLineEntries();

        int defenseLineIndex = slot.DefenseLineIndex;
        EnsureDefenseLineEntryCount(defenseLineIndex + 1);
        if (defenseLineIndex < 0 || defenseLineIndex >= defenseLines.Count)
        {
            Debug.LogWarning("[GameManager] 방어선 슬롯 인덱스가 유효하지 않아 등록하지 않았습니다.", slot);
            return;
        }

        DefenseLineEntry defenseLine = defenseLines[defenseLineIndex];
        if (defenseLine == null)
        {
            Debug.LogWarning("[GameManager] 방어선 설정이 비어 있어 슬롯을 등록하지 않았습니다.", this);
            return;
        }

        if (defenseLine.obstacleSlots == null)
        {
            defenseLine.obstacleSlots = new List<ObstacleBuildSlot>(3);
        }

        if (!defenseLine.obstacleSlots.Contains(slot))
        {
            defenseLine.obstacleSlots.Add(slot);
            defenseLine.obstacleSlots.Sort(CompareObstacleBuildSlotIndex);
        }

        slot.RefreshCurrentObstacleReference();
        RegisterObstacleBuildEntry(slot.StoredBuildEntry);
        TryRestorePendingObstaclePlacements();
    }

    // 장애물 빌드 항목을 저장 식별자로 등록한다
    public void RegisterObstacleBuildEntry(ObstacleBuildEntrySO buildEntry)
    {
        if (buildEntry == null)
        {
            return;
        }

        string saveId = buildEntry.SaveId;
        if (string.IsNullOrWhiteSpace(saveId))
        {
            Debug.LogWarning($"[GameManager] 장애물 빌드 항목 '{buildEntry.name}'의 SaveId가 비어 있어 저장 복원에 사용할 수 없습니다.", buildEntry);
            return;
        }

        if (invalidObstacleBuildEntrySaveIds.Contains(saveId))
        {
            return;
        }

        if (obstacleBuildEntriesBySaveId.TryGetValue(saveId, out ObstacleBuildEntrySO registeredEntry) && registeredEntry != buildEntry)
        {
            Debug.LogWarning($"[GameManager] 장애물 빌드 항목 SaveId가 중복되었습니다: {saveId}", buildEntry);
            obstacleBuildEntriesBySaveId.Remove(saveId);
            invalidObstacleBuildEntrySaveIds.Add(saveId);
            return;
        }

        obstacleBuildEntriesBySaveId[saveId] = buildEntry;
    }

    // 장애물 배치나 레벨 변경을 저장 대상으로 표시한다
    public void MarkObstacleStateDirty()
    {
        if (!isRestoringObstacles)
        {
            SaveManager.Inst?.MarkDirty();
        }
    }

    // 터렛 상점 항목과 연결된 진화 Definition을 저장 식별자로 등록한다
    public void RegisterTurretShopEntry(TurretShopEntrySO shopEntry, TurretPlacementController placementController)
    {
        if (shopEntry == null)
        {
            return;
        }

        if (placementController != null)
        {
            turretPlacementController = placementController;
        }

        string saveId = shopEntry.SaveId;
        if (string.IsNullOrWhiteSpace(saveId))
        {
            Debug.LogWarning($"[GameManager] 터렛 상점 항목 '{shopEntry.name}'의 SaveId가 비어 있어 배치 횟수를 복원할 수 없습니다.", shopEntry);
        }
        else if (!invalidTurretShopEntrySaveIds.Contains(saveId))
        {
            if (turretShopEntriesBySaveId.TryGetValue(saveId, out TurretShopEntrySO registeredEntry) && registeredEntry != shopEntry)
            {
                Debug.LogWarning($"[GameManager] 터렛 상점 항목 SaveId가 중복되었습니다: {saveId}", shopEntry);
                turretShopEntriesBySaveId.Remove(saveId);
                invalidTurretShopEntrySaveIds.Add(saveId);
            }
            else
            {
                turretShopEntriesBySaveId[saveId] = shopEntry;
            }
        }

        RegisterTurretDefinitionTree(shopEntry.TurretDefinition);
        TryRestorePendingTurretPlacementCounts();
        if (hasStartedTurretRestorePhase)
        {
            TryRestorePendingTurretPlacements();
        }
    }

    // 터렛 배치나 강화 상태 변경을 저장 대상으로 표시한다
    public void MarkTurretStateDirty()
    {
        if (!isRestoringTurrets)
        {
            SaveManager.Inst?.MarkDirty();
        }
    }

    // 터렛 Definition과 모든 진화 대상을 저장 ID 조회 목록에 재귀 등록한다
    private void RegisterTurretDefinitionTree(TurretDefinitionSO definition)
    {
        if (definition == null || !registeredTurretDefinitions.Add(definition))
        {
            return;
        }

        string turretId = definition.turretId;
        if (string.IsNullOrWhiteSpace(turretId))
        {
            Debug.LogWarning($"[GameManager] 터렛 Definition '{definition.name}'의 turretId가 비어 있어 저장 복원에 사용할 수 없습니다.", definition);
        }
        else if (!invalidTurretDefinitionIds.Contains(turretId))
        {
            if (turretDefinitionsById.TryGetValue(turretId, out TurretDefinitionSO registeredDefinition) && registeredDefinition != definition)
            {
                Debug.LogWarning($"[GameManager] 터렛 Definition의 turretId가 중복되었습니다: {turretId}", definition);
                turretDefinitionsById.Remove(turretId);
                invalidTurretDefinitionIds.Add(turretId);
            }
            else
            {
                turretDefinitionsById[turretId] = definition;
            }
        }

        TurretEvolutionProgressionSO progression = definition.evolutionProgressionProfile;
        if (progression == null || progression.evolutionEntries == null)
        {
            return;
        }

        for (int i = 0; i < progression.evolutionEntries.Length; i++)
        {
            TurretEvolutionEntry evolutionEntry = progression.evolutionEntries[i];
            if (evolutionEntry != null)
            {
                RegisterTurretDefinitionTree(evolutionEntry.targetDefinition);
            }
        }
    }

    // 지정 배치 항목으로 최초 점유된 슬롯 수를 반환한다
    public int GetFirstPlacementCount(ObstacleBuildEntrySO entry)
    {
        if (entry == null || defenseLines == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null || defenseLine.obstacleSlots == null)
            {
                continue;
            }

            for (int j = 0; j < defenseLine.obstacleSlots.Count; j++)
            {
                ObstacleBuildSlot slot = defenseLine.obstacleSlots[j];
                if (slot != null && slot.HasStoredProgressForEntry(entry))
                {
                    count++;
                }
            }
        }

        return count;
    }

    // 방어선 설치 슬롯 등록을 해제한다
    public void UnregisterDefenseLineSlot(ObstacleBuildSlot slot)
    {
        if (slot == null || defenseLines == null)
        {
            return;
        }

        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null || defenseLine.obstacleSlots == null)
            {
                continue;
            }

            defenseLine.obstacleSlots.Remove(slot);
        }
    }

    // 슬롯에 장애물이 설치되었을 때 방어선 복구 가능 여부를 확인한다
    public void NotifyObstaclePlaced(ObstacleBuildSlot slot, Obstacle obstacle)
    {
        if (slot == null || obstacle == null)
        {
            return;
        }

        RegisterDefenseLineSlot(slot);
        slot.SetCurrentObstacle(obstacle);

        int defenseLineIndex = slot.DefenseLineIndex;
        if (defenseLineIndex < 0 || defenseLineIndex >= defenseLines.Count)
        {
            return;
        }

        bool isBreached = defenseLines[defenseLineIndex].isBreached;
        bool isFullyBuilt = IsDefenseLineFullyBuilt(defenseLineIndex);
        //Debug.Log($"[GameManager] 장애물 배치됨 - 방어선 {defenseLineIndex}, 붕괴 상태: {isBreached}, 완전 건설: {isFullyBuilt}");

        if (suppressDefenseLineRestore)
        {
            return;
        }

        if (isBreached && isFullyBuilt)
        {
            NotifyDefenseLineRestored(defenseLineIndex);
        }
    }

    // 파괴된 장애물이 속한 방어선을 붕괴 상태로 전환한다
    public void NotifyObstacleFractured(Obstacle obstacle)
    {
        if (obstacle == null)
        {
            Debug.LogWarning("[GameManager] NotifyObstacleFractured: obstacle이 null입니다.");
            return;
        }

        //Debug.Log($"[GameManager] {obstacle.name} 파괴 알림 받음");

        ObstacleBuildSlot slot = FindDefenseLineSlot(obstacle);
        int defenseLineIndex = slot != null ? slot.DefenseLineIndex : FindDefenseLineIndex(obstacle);

        //Debug.Log($"[GameManager] 파괴된 장애물의 방어선 인덱스: {defenseLineIndex}, 슬롯 발견: {slot != null}");

        if (defenseLineIndex < 0)
        {
            Debug.LogWarning($"[GameManager] {obstacle.name}의 방어선을 찾을 수 없습니다!");
            return;
        }

        if (slot != null)
        {
            slot.ClearCurrentObstacle(obstacle);
        }

        DefenseLineEntry defenseLine = defenseLines[defenseLineIndex];
        if (defenseLine.isBreached)
        {
            //Debug.Log($"[GameManager] 방어선 {defenseLineIndex}는 이미 붕괴 상태입니다.");
            return;
        }

        bool isGateBreached = slot != null && slot.SlotType == ObstacleBuildSlotType.Gate;

        //Debug.Log($"[GameManager] 방어선 {defenseLineIndex} 붕괴! 생존자 대피 명령 전달");
        defenseLine.isBreached = true;
        CommandSurvivorsToRetreat(defenseLineIndex, defenseLine.retreatPoint, isGateBreached);

        if (isGateBreached)
        {
            StartGameOverSequence();
        }
    }

    // 게이트 파괴 이후 게임오버 복구 시퀀스를 시작한다
    public void StartGameOverSequence()
    {
        if (gameOverCoroutine != null)
        {
            return;
        }

        CompleteZombieDpsMeasurementWave();
        gameOverCoroutine = StartCoroutine(GameOverSequence());
    }

    // 페이드 인 후 게임을 종료한다(에디터: 플레이 모드 중지, 빌드: 애플리케이션 종료)
    public void QuitGame()
    {
        //if (quitGameCoroutine != null)
        //{
        //    return;
        //}

        //quitGameCoroutine = StartCoroutine(QuitGameSequence());

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 페이드 패널을 불투명하게 만든 뒤 실행 환경에 맞춰 게임을 종료한다
    private IEnumerator QuitGameSequence()
    {
        if (gameOverPanelUI != null)
        {
            gameOverPanelUI.SetMessage(quitTitleMessage, quitStatusMessage);
            yield return gameOverPanelUI.FadeIn(quitFadeDuration);
        }
        else
        {
            yield return new WaitForSecondsRealtime(quitFadeDuration);
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 외부 재건축 시스템이 방어선 복구 완료 시 생존자 복귀를 요청한다
    public void NotifyDefenseLineRestored(int defenseLineIndex)
    {
        if (defenseLineIndex < 0 || defenseLineIndex >= defenseLines.Count)
        {
            Debug.LogWarning("[GameManager] 복구 처리할 방어선 인덱스가 유효하지 않습니다.", this);
            return;
        }

        DefenseLineEntry defenseLine = defenseLines[defenseLineIndex];
        if (defenseLine == null)
        {
            Debug.LogWarning("[GameManager] 복구 처리할 방어선 설정이 비어 있습니다.", this);
            return;
        }

        if (!defenseLine.isBreached)
        {
            //Debug.Log($"[GameManager] 방어선 {defenseLineIndex}는 이미 복구 상태입니다.");
            return;
        }

        if (!IsDefenseLineFullyBuilt(defenseLineIndex))
        {
            //Debug.Log($"[GameManager] 방어선 {defenseLineIndex}가 완전히 건설되지 않아 복구할 수 없습니다.");
            return;
        }

        //Debug.Log($"[GameManager] 방어선 {defenseLineIndex} 복구됨! 생존자 복귀 명령 전달");
        defenseLine.isBreached = false;
        ApplyDefenseLineTurretBaseState(defenseLineIndex, true);
        CommandSurvivorsToRestoredPoint(defenseLineIndex, defenseLine.restoredPoint);
    }

    // 수리가 필요한 가장 가까운 슬롯 점유 장애물을 예약한다
    public bool TryGetRepairTarget(Vector3 requesterPosition, Survivor survivor, out Obstacle obstacle)
    {
        obstacle = null;
        if (survivor == null || !survivor.CanRepairObstacles || defenseLines == null)
        {
            return false;
        }

        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null || defenseLine.obstacleSlots == null)
            {
                continue;
            }

            if (defenseLine.isBreached)
            {
                continue;
            }

            for (int j = defenseLine.obstacleSlots.Count - 1; j >= 0; j--)
            {
                ObstacleBuildSlot slot = defenseLine.obstacleSlots[j];
                if (slot == null)
                {
                    defenseLine.obstacleSlots.RemoveAt(j);
                    continue;
                }

                Obstacle current = slot.CurrentObstacle;
                if (current == null || !current.CanBeRepairedBy(survivor))
                {
                    continue;
                }

                if (IsRepairBlockedByRetreatedLine(survivor, current))
                {
                    continue;
                }

                float sqrDistance = (current.transform.position - requesterPosition).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    obstacle = current;
                }
            }
        }

        return obstacle != null && obstacle.TryReserveRepair(survivor);
    }

    // 장애물이 속한 방어선 인덱스를 외부 시스템에 제공한다
    public bool TryGetDefenseLineIndex(Obstacle obstacle, out int defenseLineIndex)
    {
        defenseLineIndex = FindDefenseLineIndex(obstacle);
        return defenseLineIndex >= 0;
    }

    // 현재 복구된 방어선 바로 앞 방어선에만 장애물을 설치할 수 있는지 확인한다
    public bool CanPlaceObstacleAtDefenseLine(int defenseLineIndex)
    {
        int restoredDefenseLineIndex = GetFrontMostContiguousRestoredDefenseLineIndex();
        if (restoredDefenseLineIndex <= 0)
        {
            return false;
        }

        return defenseLineIndex == restoredDefenseLineIndex - 1;
    }

    // 뒤쪽에서 이어진 복구 구간의 가장 앞쪽 방어선 인덱스를 찾는다
    private int GetFrontMostContiguousRestoredDefenseLineIndex()
    {
        if (defenseLines == null)
        {
            return -1;
        }

        int restoredDefenseLineIndex = -1;
        for (int i = defenseLines.Count - 1; i >= 0; i--)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null || defenseLine.isBreached || !IsDefenseLineFullyBuilt(i))
            {
                break;
            }

            restoredDefenseLineIndex = i;
        }

        return restoredDefenseLineIndex;
    }

    // 지정 방어선의 등록된 슬롯이 모두 점유되었는지 확인한다
    public bool IsDefenseLineFullyBuilt(int defenseLineIndex)
    {
        if (defenseLineIndex < 0 || defenseLineIndex >= defenseLines.Count)
        {
            return false;
        }

        DefenseLineEntry defenseLine = defenseLines[defenseLineIndex];
        if (defenseLine == null || defenseLine.obstacleSlots == null)
        {
            return false;
        }

        int registeredSlotCount = 0;
        int occupiedSlotCount = 0;

        for (int i = defenseLine.obstacleSlots.Count - 1; i >= 0; i--)
        {
            ObstacleBuildSlot slot = defenseLine.obstacleSlots[i];
            if (slot == null)
            {
                defenseLine.obstacleSlots.RemoveAt(i);
                continue;
            }

            registeredSlotCount++;
            Obstacle currentObs = slot.CurrentObstacle;
            if (currentObs != null)
            {
                occupiedSlotCount++;
                //Debug.Log($"[GameManager] 방어선 {defenseLineIndex} 슬롯 {slot.SlotIndex}: {currentObs.name} (Enabled: {currentObs.enabled}, Alive: {currentObs.IsAlive})");
            }
            else
            {
                //Debug.Log($"[GameManager] 방어선 {defenseLineIndex} 슬롯 {slot.SlotIndex}: 비어있음");
            }
        }

        //Debug.Log($"[GameManager] 방어선 {defenseLineIndex} 완성 체크 - 점유: {occupiedSlotCount}/{registeredSlotCount}");
        return registeredSlotCount > 0 && occupiedSlotCount == registeredSlotCount;
    }

    // 모든 방어선의 등록된 슬롯이 점유되었는지 확인한다
    public bool AreAllDefenseLineSlotsBuilt()
    {
        if (defenseLines == null)
        {
            return false;
        }

        for (int i = 0; i < defenseLines.Count; i++)
        {
            if (!IsDefenseLineFullyBuilt(i))
            {
                return false;
            }
        }

        return true;
    }

    // 지정 방어선의 빈 슬롯 수를 계산한다
    public int GetEmptyDefenseLineSlotCount(int defenseLineIndex)
    {
        if (defenseLineIndex < 0 || defenseLineIndex >= defenseLines.Count)
        {
            return 0;
        }

        DefenseLineEntry defenseLine = defenseLines[defenseLineIndex];
        if (defenseLine == null || defenseLine.obstacleSlots == null)
        {
            return 0;
        }

        int registeredSlotCount = 0;
        int occupiedSlotCount = 0;
        for (int i = defenseLine.obstacleSlots.Count - 1; i >= 0; i--)
        {
            ObstacleBuildSlot slot = defenseLine.obstacleSlots[i];
            if (slot == null)
            {
                defenseLine.obstacleSlots.RemoveAt(i);
                continue;
            }

            registeredSlotCount++;
            if (slot.CurrentObstacle != null)
            {
                occupiedSlotCount++;
            }
        }

        return Mathf.Max(0, registeredSlotCount - occupiedSlotCount);
    }

    // 슬롯 등록이 끝난 뒤 시작 시점의 방어선 붕괴 상태를 계산한다
    private IEnumerator InitializeDefenseLineStatesAfterSlotRegistration()
    {
        yield return null;

        EnsureDefaultDefenseLineEntries();
        TryRestorePendingObstaclePlacements();
        TryRestorePendingTurretPlacementCounts();

        // 터렛 베이스 슬롯의 활성 상태를 먼저 확정해야 TryRestorePendingTurretPlacements()에서
        // TurretBaseSlot.CanPlace(isActiveAndEnabled)가 올바르게 평가된다.
        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null || !HasRegisteredDefenseLineSlots(defenseLine))
            {
                continue;
            }

            defenseLine.isBreached = !IsDefenseLineFullyBuilt(i);
            ApplyDefenseLineTurretBaseState(i, !defenseLine.isBreached);
        }

        hasStartedTurretRestorePhase = true;
        TryRestorePendingTurretPlacements();
        LogPendingObstacleRestoreFailures();
        LogPendingTurretRestoreFailures();

        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine != null && defenseLine.isBreached && HasRegisteredDefenseLineSlots(defenseLine))
            {
                CommandSurvivorsToRetreat(i, defenseLine.retreatPoint, IsGateDefenseLine(defenseLine));
            }
        }
    }

    // 방어선 상태에 맞춰 연결된 터렛 베이스 루트를 활성화하거나 비활성화한다
    private void ApplyDefenseLineTurretBaseState(int defenseLineIndex, bool isAvailable)
    {
        if (defenseLines == null)
        {
            return;
        }

        if (defenseLineIndex < 0 || defenseLineIndex >= defenseLines.Count)
        {
            return;
        }

        DefenseLineEntry defenseLine = defenseLines[defenseLineIndex];
        if (defenseLine == null || defenseLine.turretBaseSlots == null)
        {
            return;
        }

        for (int i = defenseLine.turretBaseSlots.Count - 1; i >= 0; i--)
        {
            TurretBaseSlot turretBaseSlot = defenseLine.turretBaseSlots[i];
            if (turretBaseSlot == null)
            {
                defenseLine.turretBaseSlots.RemoveAt(i);
                continue;
            }

            turretBaseSlot.SetDefenseLineAvailable(isAvailable);
        }
    }

    // 방어선에 등록된 슬롯이 하나 이상 있는지 확인한다
    private bool HasRegisteredDefenseLineSlots(DefenseLineEntry defenseLine)
    {
        if (defenseLine == null || defenseLine.obstacleSlots == null)
        {
            return false;
        }

        for (int i = defenseLine.obstacleSlots.Count - 1; i >= 0; i--)
        {
            if (defenseLine.obstacleSlots[i] == null)
            {
                defenseLine.obstacleSlots.RemoveAt(i);
                continue;
            }

            return true;
        }

        return false;
    }

    // 방어선에 게이트 슬롯이 포함되어 있는지 확인한다
    private bool IsGateDefenseLine(DefenseLineEntry defenseLine)
    {
        if (defenseLine == null || defenseLine.obstacleSlots == null)
        {
            return false;
        }

        for (int i = defenseLine.obstacleSlots.Count - 1; i >= 0; i--)
        {
            ObstacleBuildSlot slot = defenseLine.obstacleSlots[i];
            if (slot == null)
            {
                defenseLine.obstacleSlots.RemoveAt(i);
                continue;
            }

            if (slot.SlotType == ObstacleBuildSlotType.Gate)
            {
                return true;
            }
        }

        return false;
    }

    // 후퇴한 생존자가 이전 방어선을 수리 대상으로 잡지 않도록 확인한다
    private bool IsRepairBlockedByRetreatedLine(Survivor survivor, Obstacle obstacle)
    {
        if (survivor == null || obstacle == null || survivor.ActiveDefenseLineIndex < 0)
        {
            return false;
        }

        int obstacleDefenseLineIndex = FindDefenseLineIndex(obstacle);
        return obstacleDefenseLineIndex >= 0 && obstacleDefenseLineIndex <= survivor.ActiveDefenseLineIndex;
    }

    // 기본 방어선 항목이 부족하면 1차부터 4차까지 채운다
    private void EnsureDefaultDefenseLineEntries()
    {
        if (defenseLines == null)
        {
            defenseLines = new List<DefenseLineEntry>(DEFAULT_DEFENSE_LINE_COUNT);
        }

        EnsureDefenseLineEntryCount(DEFAULT_DEFENSE_LINE_COUNT);

        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null)
            {
                defenseLine = new DefenseLineEntry();
                defenseLines[i] = defenseLine;
            }

            if (string.IsNullOrWhiteSpace(defenseLine.lineName))
            {
                defenseLine.lineName = $"{i + 1}차 방어선";
            }

            if (defenseLine.obstacleSlots == null)
            {
                defenseLine.obstacleSlots = new List<ObstacleBuildSlot>(3);
            }

            if (defenseLine.turretBaseSlots == null)
            {
                defenseLine.turretBaseSlots = new List<TurretBaseSlot>(3);
            }
        }
    }

    // 필요한 개수만큼 방어선 항목을 생성한다
    private void EnsureDefenseLineEntryCount(int requiredCount)
    {
        if (defenseLines == null)
        {
            defenseLines = new List<DefenseLineEntry>(Mathf.Max(DEFAULT_DEFENSE_LINE_COUNT, requiredCount));
        }

        int targetCount = Mathf.Max(DEFAULT_DEFENSE_LINE_COUNT, requiredCount);
        while (defenseLines.Count < targetCount)
        {
            int lineNumber = defenseLines.Count + 1;
            defenseLines.Add(new DefenseLineEntry
            {
                lineName = $"{lineNumber}차 방어선"
            });
        }

    }

    // 장애물이 속한 방어선 인덱스를 찾는다
    private int FindDefenseLineIndex(Obstacle obstacle)
    {
        ObstacleBuildSlot slot = FindDefenseLineSlot(obstacle);
        return slot == null ? -1 : slot.DefenseLineIndex;
    }

    // 장애물이 점유한 방어선 슬롯을 찾는다
    private ObstacleBuildSlot FindDefenseLineSlot(Obstacle obstacle)
    {
        if (obstacle == null || defenseLines == null)
        {
            return null;
        }

        Transform obstacleTransform = obstacle.transform;

        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null || defenseLine.obstacleSlots == null)
            {
                continue;
            }

            for (int j = defenseLine.obstacleSlots.Count - 1; j >= 0; j--)
            {
                ObstacleBuildSlot slot = defenseLine.obstacleSlots[j];
                if (slot == null)
                {
                    defenseLine.obstacleSlots.RemoveAt(j);
                    continue;
                }

                // CurrentObstacle 프로퍼티는 파괴된 장애물을 null로 반환할 수 있으므로
                // BuildPoint 하위에 있는지 Transform으로 직접 확인한다
                if (slot.BuildPoint != null && obstacleTransform.IsChildOf(slot.BuildPoint))
                {
                    //Debug.Log($"[GameManager] {obstacle.name}의 슬롯을 Transform 계층으로 찾음 - 방어선 {i}, 슬롯 {slot.SlotIndex}");
                    return slot;
                }
            }
        }

        return null;
    }

    // 방어선 슬롯 정렬을 위해 슬롯 인덱스를 비교한다
    private static int CompareObstacleBuildSlotIndex(ObstacleBuildSlot left, ObstacleBuildSlot right)
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

        return left.SlotIndex.CompareTo(right.SlotIndex);
    }

    // 게임오버 페이드, 리셋, 이전 웨이브 재시작을 순서대로 처리한다
    private IEnumerator GameOverSequence()
    {
        isWaveProgressionPaused = true;
        PauseAllZombieSpawners();

        yield return FadeGameOverPanelIn();

        DespawnAllZombies();
        RebuildAllDefenseLines();
        ResetSurvivorsForWaveRestart();
        ReassignAllEngineers();
        PreparePreviousWaveRestart();
        
        yield return fadeWait;
        yield return FadeGameOverPanelOut();

        ResumeAllZombieSpawners();
        isWaveProgressionPaused = false;
        gameOverCoroutine = null;
    }

    private WaitForSeconds fadeWait = new WaitForSeconds(3f);
    // 게임오버 패널을 불투명하게 페이드한다
    private IEnumerator FadeGameOverPanelIn()
    {
        if (gameOverPanelUI == null)
        {
            Debug.LogWarning("[GameManager] 게임오버 패널 UI가 없어 페이드 인을 건너뜁니다.", this);
            yield break;
        }

        gameOverPanelUI.ResetMessageToDefault();
        yield return gameOverPanelUI.FadeIn(gameOverFadeInDuration);
    }

    // 게임오버 패널을 투명하게 페이드한다
    private IEnumerator FadeGameOverPanelOut()
    {
        if (gameOverPanelUI == null)
        {
            yield break;
        }

        yield return gameOverPanelUI.FadeOut(gameOverFadeOutDuration);
    }

    // 모든 좀비 스포너의 추가 스폰을 멈춘다
    private void PauseAllZombieSpawners()
    {
        for (int i = zombieSpawners.Count - 1; i >= 0; i--)
        {
            ZombieSpawner zombieSpawner = zombieSpawners[i];
            if (zombieSpawner == null)
            {
                zombieSpawners.RemoveAt(i);
                continue;
            }

            zombieSpawner.PauseSpawning();
        }
    }

    // 모든 좀비 스포너의 활성 좀비를 풀로 반환한다
    private void DespawnAllZombies()
    {
        for (int i = zombieSpawners.Count - 1; i >= 0; i--)
        {
            ZombieSpawner zombieSpawner = zombieSpawners[i];
            if (zombieSpawner == null)
            {
                zombieSpawners.RemoveAt(i);
                continue;
            }

            zombieSpawner.DespawnAllSpawnedZombies();
        }
    }

    // 저장된 슬롯 진행도를 기준으로 모든 방어선을 다시 배치하고 필요하면 생존자 복귀를 알린다
    private void RebuildAllDefenseLines(bool notifyRestoredDefenseLines = false)
    {
        suppressDefenseLineRestore = true;

        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null)
            {
                continue;
            }

            bool wasBreached = defenseLine.isBreached;

            if (defenseLine.obstacleSlots == null)
            {
                defenseLine.isBreached = true;
                ApplyDefenseLineTurretBaseState(i, false);
                continue;
            }

            for (int j = defenseLine.obstacleSlots.Count - 1; j >= 0; j--)
            {
                ObstacleBuildSlot slot = defenseLine.obstacleSlots[j];
                if (slot == null)
                {
                    defenseLine.obstacleSlots.RemoveAt(j);
                    continue;
                }

                slot.TryRebuildStoredObstacleWithoutCost(out _);
            }

            bool isFullyBuilt = IsDefenseLineFullyBuilt(i);
            if (notifyRestoredDefenseLines && wasBreached && isFullyBuilt)
            {
                defenseLine.isBreached = true;
                NotifyDefenseLineRestored(i);
            }
            else
            {
                defenseLine.isBreached = !isFullyBuilt;
                ApplyDefenseLineTurretBaseState(i, isFullyBuilt);
            }
        }

        suppressDefenseLineRestore = false;
    }

    // 엔지니어 생존자를 마지막 터렛 슬롯에 다시 배치한다
    private void ReassignAllEngineers()
    {
        for (int i = survivors.Count - 1; i >= 0; i--)
        {
            Survivor survivor = survivors[i];
            if (survivor == null)
            {
                survivors.RemoveAt(i);
                continue;
            }

            survivor.TryReassignEngineerToStoredTurret();
        }
    }

    // 웨이브 재시작 시 건축노동자의 후퇴 방어선 상태를 복구하고, 게이트 붕괴로 도피했던 나머지 생존자를 집결지로 복귀시킨다
    private void ResetSurvivorsForWaveRestart()
    {
        Transform restoredPoint = GetRearDefenseLineRestoredPoint();
        if (restoredPoint == null)
        {
            Debug.LogWarning("[GameManager] 웨이브 재시작 복귀 포인트가 없어 건축노동자 방어선 상태만 초기화합니다.", this);
        }

        for (int i = survivors.Count - 1; i >= 0; i--)
        {
            Survivor survivor = survivors[i];
            if (survivor == null)
            {
                survivors.RemoveAt(i);
                continue;
            }

            survivor.ResetDefenseLineStateForWaveRestart(restoredPoint);
            survivor.ResetGateRetreatStateForWaveRestart();
        }
    }

    // 등록된 방어선 중 가장 뒤쪽 방어선의 복귀 지점을 반환한다
    private Transform GetRearDefenseLineRestoredPoint()
    {
        if (defenseLines == null)
        {
            return null;
        }

        for (int i = defenseLines.Count - 1; i >= 0; i--)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null || !HasRegisteredDefenseLineSlots(defenseLine))
            {
                continue;
            }

            return defenseLine.restoredPoint;
        }

        return null;
    }

    // 마지막 보스 웨이브 다음 체크포인트부터 다시 시작할 수 있도록 준비한다
    private void PreparePreviousWaveRestart()
    {
        Wave = ResolveBossCheckpointRestartWave(Wave);
        SaveManager.Inst?.MarkDirty();
        KillCount = 0;
        DestKillCount = 0;
        // 웨이브 실패 패널티: 그동안 모은 코인량을 초기화해 클리어 보너스에 누적되지 않도록 한다.
        InventorySystem.Inst.ResetWaveCollectCoinCount();

        int totalSpawnCount = 0;
        for (int i = zombieSpawners.Count - 1; i >= 0; i--)
        {
            ZombieSpawner zombieSpawner = zombieSpawners[i];
            if (zombieSpawner == null)
            {
                zombieSpawners.RemoveAt(i);
                continue;
            }

            totalSpawnCount += zombieSpawner.PrepareWaveForRestart(Wave);
        }

        InputDestKillCount(totalSpawnCount);
        OnWaveDecrease?.Invoke(Wave);
        BeginZombieDpsMeasurementWave();
    }

    // 현재 웨이브 번호를 SaveManager가 저장할 JSON으로 직렬화한다
    public string CaptureSaveData()
    {
        WaveSaveData saveData = new WaveSaveData
        {
            Wave = Mathf.Max(1, Wave),
            HighestReachedWave = Mathf.Max(Mathf.Max(1, Wave), HighestReachedWave)
        };

        for (int i = 0; i < survivors.Count; i++)
        {
            Survivor survivor = survivors[i];
            if (survivor != null)
            {
                saveData.Survivors.Add(survivor.CaptureSaveEntry());
            }
        }

        CaptureObstaclePlacements(saveData.Obstacles);
        CaptureTurretPlacements(saveData.Turrets);
        CaptureTurretPlacementCounts(saveData.TurretPlacementCounts);

        return JsonUtility.ToJson(saveData);
    }

    // 저장된 웨이브 번호를 복원하고 현재 웨이브를 처음부터 시작할 수 있도록 초기화한다
    public void RestoreSaveData(string json)
    {
        WaveSaveData saveData = JsonUtility.FromJson<WaveSaveData>(json);
        if (saveData == null)
        {
            return;
        }

        Wave = Mathf.Max(1, saveData.Wave);
        HighestReachedWave = Mathf.Max(Wave, saveData.HighestReachedWave);
        pendingSurvivorRestoreEntries.Clear();
        if (saveData.Survivors != null)
        {
            for (int i = 0; i < saveData.Survivors.Count; i++)
            {
                SurvivorSaveEntry saveEntry = saveData.Survivors[i];
                if (saveEntry != null)
                {
                    pendingSurvivorRestoreEntries.Add(saveEntry);
                }
            }
        }
        pendingObstacleRestoreEntries.Clear();
        if (saveData.Obstacles != null)
        {
            for (int i = 0; i < saveData.Obstacles.Count; i++)
            {
                ObstaclePlacementSaveEntry saveEntry = saveData.Obstacles[i];
                if (saveEntry != null)
                {
                    pendingObstacleRestoreEntries.Add(saveEntry);
                }
            }
        }
        pendingTurretRestoreEntries.Clear();
        if (saveData.Turrets != null)
        {
            for (int i = 0; i < saveData.Turrets.Count; i++)
            {
                TurretPlacementSaveEntry saveEntry = saveData.Turrets[i];
                if (saveEntry != null)
                {
                    pendingTurretRestoreEntries.Add(saveEntry);
                }
            }
        }
        pendingTurretPlacementCountEntries.Clear();
        if (saveData.TurretPlacementCounts != null)
        {
            for (int i = 0; i < saveData.TurretPlacementCounts.Count; i++)
            {
                TurretPlacementCountSaveEntry saveEntry = saveData.TurretPlacementCounts[i];
                if (saveEntry != null)
                {
                    pendingTurretPlacementCountEntries.Add(saveEntry);
                }
            }
        }
        KillCount = 0;
        DestKillCount = 0;
    }

    [Serializable]
    private class WaveSaveData
    {
        public int Wave;
        public int HighestReachedWave;
        public List<SurvivorSaveEntry> Survivors = new List<SurvivorSaveEntry>();
        public List<ObstaclePlacementSaveEntry> Obstacles = new List<ObstaclePlacementSaveEntry>();
        public List<TurretPlacementSaveEntry> Turrets = new List<TurretPlacementSaveEntry>();
        public List<TurretPlacementCountSaveEntry> TurretPlacementCounts = new List<TurretPlacementCountSaveEntry>();
    }

    // 현재 슬롯 진행도와 아직 해결하지 못한 복원 항목을 저장 목록에 모은다
    private void CaptureObstaclePlacements(List<ObstaclePlacementSaveEntry> destination)
    {
        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null || defenseLine.obstacleSlots == null)
            {
                continue;
            }

            for (int j = 0; j < defenseLine.obstacleSlots.Count; j++)
            {
                ObstacleBuildSlot slot = defenseLine.obstacleSlots[j];
                if (slot != null && slot.TryCaptureSaveEntry(out ObstaclePlacementSaveEntry saveEntry))
                {
                    destination.Add(saveEntry);
                }
            }
        }

        for (int i = 0; i < pendingObstacleRestoreEntries.Count; i++)
        {
            ObstaclePlacementSaveEntry pendingEntry = pendingObstacleRestoreEntries[i];
            if (pendingEntry != null && !ContainsObstaclePlacement(destination, pendingEntry.DefenseLineIndex, pendingEntry.SlotIndex))
            {
                destination.Add(pendingEntry);
            }
        }
    }

    // 등록된 슬롯과 빌드 항목을 사용해 가능한 저장 장애물을 모두 복원한다
    private void TryRestorePendingObstaclePlacements()
    {
        if (isRestoringObstacles || pendingObstacleRestoreEntries.Count == 0)
        {
            return;
        }

        bool restoredAny = false;
        isRestoringObstacles = true;
        try
        {
            for (int i = pendingObstacleRestoreEntries.Count - 1; i >= 0; i--)
            {
                ObstaclePlacementSaveEntry saveEntry = pendingObstacleRestoreEntries[i];
                ObstacleBuildSlot slot = FindDefenseLineSlot(saveEntry.DefenseLineIndex, saveEntry.SlotIndex);
                if (slot == null || string.IsNullOrWhiteSpace(saveEntry.BuildEntrySaveId) ||
                    !obstacleBuildEntriesBySaveId.TryGetValue(saveEntry.BuildEntrySaveId, out ObstacleBuildEntrySO buildEntry))
                {
                    continue;
                }

                if (slot.TryRestoreSaveEntry(saveEntry, buildEntry, out _))
                {
                    pendingObstacleRestoreEntries.RemoveAt(i);
                    restoredAny = true;
                }
                else
                {
                    Debug.LogWarning($"[GameManager] 저장 장애물 복원에 실패했습니다. 방어선: {saveEntry.DefenseLineIndex}, 슬롯: {saveEntry.SlotIndex}, SaveId: {saveEntry.BuildEntrySaveId}", slot);
                }
            }
        }
        finally
        {
            isRestoringObstacles = false;
        }

        if (restoredAny)
        {
            OnObstaclePlacementsRestored?.Invoke();
        }
    }

    // 초기 등록 후에도 해결하지 못한 장애물 복원 항목의 원인을 출력한다
    private void LogPendingObstacleRestoreFailures()
    {
        for (int i = 0; i < pendingObstacleRestoreEntries.Count; i++)
        {
            ObstaclePlacementSaveEntry saveEntry = pendingObstacleRestoreEntries[i];
            if (saveEntry == null)
            {
                continue;
            }

            if (FindDefenseLineSlot(saveEntry.DefenseLineIndex, saveEntry.SlotIndex) == null)
            {
                Debug.LogWarning($"[GameManager] 저장 장애물의 슬롯을 찾지 못했습니다. 방어선: {saveEntry.DefenseLineIndex}, 슬롯: {saveEntry.SlotIndex}", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(saveEntry.BuildEntrySaveId) ||
                !obstacleBuildEntriesBySaveId.ContainsKey(saveEntry.BuildEntrySaveId))
            {
                Debug.LogWarning($"[GameManager] 저장 장애물의 빌드 항목을 찾지 못했습니다. SaveId: {saveEntry.BuildEntrySaveId}", this);
            }
        }
    }

    // 방어선과 슬롯 인덱스가 일치하는 장애물 슬롯을 반환한다
    private ObstacleBuildSlot FindDefenseLineSlot(int defenseLineIndex, int slotIndex)
    {
        if (defenseLineIndex < 0 || defenseLineIndex >= defenseLines.Count)
        {
            return null;
        }

        DefenseLineEntry defenseLine = defenseLines[defenseLineIndex];
        if (defenseLine == null || defenseLine.obstacleSlots == null)
        {
            return null;
        }

        for (int i = 0; i < defenseLine.obstacleSlots.Count; i++)
        {
            ObstacleBuildSlot slot = defenseLine.obstacleSlots[i];
            if (slot != null && slot.SlotIndex == slotIndex)
            {
                return slot;
            }
        }

        return null;
    }

    // 저장 목록에 같은 방어선과 슬롯 식별자가 이미 있는지 확인한다
    private static bool ContainsObstaclePlacement(List<ObstaclePlacementSaveEntry> entries, int defenseLineIndex, int slotIndex)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            ObstaclePlacementSaveEntry entry = entries[i];
            if (entry != null && entry.DefenseLineIndex == defenseLineIndex && entry.SlotIndex == slotIndex)
            {
                return true;
            }
        }

        return false;
    }

    // 현재 터렛 슬롯 상태와 아직 해결하지 못한 복원 항목을 저장 목록에 모은다
    private void CaptureTurretPlacements(List<TurretPlacementSaveEntry> destination)
    {
        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null || defenseLine.turretBaseSlots == null)
            {
                continue;
            }

            for (int j = 0; j < defenseLine.turretBaseSlots.Count; j++)
            {
                TurretBaseSlot slot = defenseLine.turretBaseSlots[j];
                if (slot != null && slot.TryCaptureSaveEntry(i, j, out TurretPlacementSaveEntry saveEntry))
                {
                    destination.Add(saveEntry);
                }
            }
        }

        for (int i = 0; i < pendingTurretRestoreEntries.Count; i++)
        {
            TurretPlacementSaveEntry pendingEntry = pendingTurretRestoreEntries[i];
            if (pendingEntry != null && !ContainsTurretPlacement(destination, pendingEntry.DefenseLineIndex, pendingEntry.SlotIndex))
            {
                destination.Add(pendingEntry);
            }
        }
    }

    // 현재 누적 배치 횟수와 아직 해결하지 못한 상점 항목을 저장 목록에 모은다
    private void CaptureTurretPlacementCounts(List<TurretPlacementCountSaveEntry> destination)
    {
        if (turretPlacementController != null)
        {
            foreach (KeyValuePair<string, TurretShopEntrySO> pair in turretShopEntriesBySaveId)
            {
                if (pair.Value == null || invalidTurretShopEntrySaveIds.Contains(pair.Key))
                {
                    continue;
                }

                destination.Add(new TurretPlacementCountSaveEntry
                {
                    ShopEntrySaveId = pair.Key,
                    PlacedCount = Mathf.Max(0, turretPlacementController.GetPlacedCount(pair.Value))
                });
            }
        }

        for (int i = 0; i < pendingTurretPlacementCountEntries.Count; i++)
        {
            TurretPlacementCountSaveEntry pendingEntry = pendingTurretPlacementCountEntries[i];
            if (pendingEntry != null && !ContainsTurretPlacementCount(destination, pendingEntry.ShopEntrySaveId))
            {
                destination.Add(pendingEntry);
            }
        }
    }

    // 등록된 슬롯과 Definition을 사용해 가능한 저장 터렛을 모두 복원한다
    private void TryRestorePendingTurretPlacements()
    {
        if (isRestoringTurrets || pendingTurretRestoreEntries.Count == 0)
        {
            return;
        }

        isRestoringTurrets = true;
        try
        {
            for (int i = pendingTurretRestoreEntries.Count - 1; i >= 0; i--)
            {
                TurretPlacementSaveEntry saveEntry = pendingTurretRestoreEntries[i];
                TurretBaseSlot slot = FindTurretBaseSlot(saveEntry.DefenseLineIndex, saveEntry.SlotIndex);
                if (slot == null || string.IsNullOrWhiteSpace(saveEntry.TurretDefinitionId) ||
                    !turretDefinitionsById.TryGetValue(saveEntry.TurretDefinitionId, out TurretDefinitionSO definition))
                {
                    continue;
                }

                if (slot.TryRestoreSaveEntry(saveEntry, definition, out _))
                {
                    pendingTurretRestoreEntries.RemoveAt(i);
                }
                else
                {
                    Debug.LogWarning($"[GameManager] 저장 터렛 복원에 실패했습니다. 방어선: {saveEntry.DefenseLineIndex}, 슬롯: {saveEntry.SlotIndex}, turretId: {saveEntry.TurretDefinitionId}", slot);
                }
            }
        }
        finally
        {
            isRestoringTurrets = false;
        }
    }

    // 등록된 상점 항목에 저장된 누적 배치 횟수를 적용한다
    private void TryRestorePendingTurretPlacementCounts()
    {
        if (isRestoringTurrets || turretPlacementController == null || pendingTurretPlacementCountEntries.Count == 0)
        {
            return;
        }

        isRestoringTurrets = true;
        try
        {
            for (int i = pendingTurretPlacementCountEntries.Count - 1; i >= 0; i--)
            {
                TurretPlacementCountSaveEntry saveEntry = pendingTurretPlacementCountEntries[i];
                if (saveEntry == null || string.IsNullOrWhiteSpace(saveEntry.ShopEntrySaveId) ||
                    !turretShopEntriesBySaveId.TryGetValue(saveEntry.ShopEntrySaveId, out TurretShopEntrySO shopEntry))
                {
                    continue;
                }

                turretPlacementController.RestorePlacedCount(shopEntry, saveEntry.PlacedCount);
                pendingTurretPlacementCountEntries.RemoveAt(i);
            }
        }
        finally
        {
            isRestoringTurrets = false;
        }
    }

    // 초기 등록 후에도 해결하지 못한 터렛과 배치 횟수 복원 원인을 출력한다
    private void LogPendingTurretRestoreFailures()
    {
        for (int i = 0; i < pendingTurretRestoreEntries.Count; i++)
        {
            TurretPlacementSaveEntry saveEntry = pendingTurretRestoreEntries[i];
            if (saveEntry == null)
            {
                continue;
            }

            if (FindTurretBaseSlot(saveEntry.DefenseLineIndex, saveEntry.SlotIndex) == null)
            {
                Debug.LogWarning($"[GameManager] 저장 터렛의 슬롯을 찾지 못했습니다. 방어선: {saveEntry.DefenseLineIndex}, 슬롯: {saveEntry.SlotIndex}", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(saveEntry.TurretDefinitionId) || !turretDefinitionsById.ContainsKey(saveEntry.TurretDefinitionId))
            {
                Debug.LogWarning($"[GameManager] 저장 터렛의 Definition을 찾지 못했습니다. turretId: {saveEntry.TurretDefinitionId}", this);
            }
        }

        for (int i = 0; i < pendingTurretPlacementCountEntries.Count; i++)
        {
            TurretPlacementCountSaveEntry saveEntry = pendingTurretPlacementCountEntries[i];
            if (saveEntry != null)
            {
                Debug.LogWarning($"[GameManager] 저장된 배치 횟수의 상점 항목을 찾지 못했습니다. SaveId: {saveEntry.ShopEntrySaveId}", this);
            }
        }
    }

    // 방어선과 목록 인덱스가 일치하는 터렛 베이스 슬롯을 반환한다
    private TurretBaseSlot FindTurretBaseSlot(int defenseLineIndex, int slotIndex)
    {
        if (defenseLineIndex < 0 || defenseLineIndex >= defenseLines.Count)
        {
            return null;
        }

        DefenseLineEntry defenseLine = defenseLines[defenseLineIndex];
        if (defenseLine == null || defenseLine.turretBaseSlots == null || slotIndex < 0 || slotIndex >= defenseLine.turretBaseSlots.Count)
        {
            return null;
        }

        return defenseLine.turretBaseSlots[slotIndex];
    }

    // 저장 목록에 같은 방어선과 터렛 슬롯 식별자가 이미 있는지 확인한다
    private static bool ContainsTurretPlacement(List<TurretPlacementSaveEntry> entries, int defenseLineIndex, int slotIndex)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            TurretPlacementSaveEntry entry = entries[i];
            if (entry != null && entry.DefenseLineIndex == defenseLineIndex && entry.SlotIndex == slotIndex)
            {
                return true;
            }
        }

        return false;
    }

    // 저장 목록에 같은 상점 항목의 배치 횟수가 이미 있는지 확인한다
    private static bool ContainsTurretPlacementCount(List<TurretPlacementCountSaveEntry> entries, string saveId)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            TurretPlacementCountSaveEntry entry = entries[i];
            if (entry != null && string.Equals(entry.ShopEntrySaveId, saveId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // 실패 웨이브 이전의 마지막 보스 웨이브 다음 체크포인트를 계산한다
    private int ResolveBossCheckpointRestartWave(int failedWave)
    {
        int safeFailedWave = Mathf.Max(1, failedWave);
        for (int candidateWave = safeFailedWave - 1; candidateWave >= 1; candidateWave--)
        {
            if (IsBossWave(candidateWave))
            {
                return candidateWave + 1;
            }
        }

        return 1;
    }

    // 등록된 스포너 중 하나라도 지정 웨이브에 보스를 출현시키는지 확인한다
    private bool IsBossWave(int wave)
    {
        for (int i = zombieSpawners.Count - 1; i >= 0; i--)
        {
            ZombieSpawner zombieSpawner = zombieSpawners[i];
            if (zombieSpawner != null && zombieSpawner.IsBossWave(wave))
            {
                return true;
            }
        }

        return false;
    }

    // 현재 웨이브의 좀비 DPS 측정을 시작한다
    private void BeginZombieDpsMeasurementWave()
    {
        ZombieWaveDpsRuntimeRecorder.BeginWave(Wave, enableZombieDpsMeasurement, zombieDpsMeasurementProfile);
    }

    // 현재 웨이브의 좀비 DPS 측정 결과를 저장한다
    private void CompleteZombieDpsMeasurementWave()
    {
        ZombieWaveDpsRuntimeRecorder.CompleteWave(Wave);
    }

    // 준비된 좀비 스포너를 다시 실행한다
    private void ResumeAllZombieSpawners()
    {
        for (int i = zombieSpawners.Count - 1; i >= 0; i--)
        {
            ZombieSpawner zombieSpawner = zombieSpawners[i];
            if (zombieSpawner == null)
            {
                zombieSpawners.RemoveAt(i);
                continue;
            }

            zombieSpawner.ResumeSpawning();
        }
    }

    // 등록된 생존자에게 대피 명령을 전달한다
    private void CommandSurvivorsToRetreat(int defenseLineIndex, Transform retreatPoint, bool isGateBreached)
    {
        if (retreatPoint == null)
        {
            Debug.LogWarning("[GameManager] 방어선 대피 포인트가 할당되지 않았습니다.", this);
            return;
        }

        //Debug.Log($"[GameManager] 등록된 생존자 수: {survivors.Count}");

        if (isGateBreached)
        {
            PrepareAllEngineersForWaveFailureRetreat();
        }

        int retreatedCount = 0;
        for (int i = survivors.Count - 1; i >= 0; i--)
        {
            Survivor survivor = survivors[i];
            if (survivor == null)
            {
                survivors.RemoveAt(i);
                continue;
            }

            // 게이트 붕괴 시에는 생존자마다 좀비 스포너 목적지 중 하나를 무작위로 배정해 흩어져 도망가는 것처럼 연출한다
            Transform destination = retreatPoint;
            if (isGateBreached && TryGetRandomFleeDestination(out Transform fleeDestination))
            {
                destination = fleeDestination;
            }

            //Debug.Log($"[GameManager] {survivor.name}에게 방어선 {defenseLineIndex} 대피 명령 전달");
            survivor.StartDefenseLineRetreat(defenseLineIndex, destination, isGateBreached);
            retreatedCount++;
        }

        //Debug.Log($"[GameManager] {retreatedCount}명의 생존자에게 방어선 {defenseLineIndex} 대피 명령 전달 완료");
    }

    // 게이트 붕괴 후 전체 생존자 후퇴 전에 탑승 엔지니어를 먼저 하차시킨다
    private void PrepareAllEngineersForWaveFailureRetreat()
    {
        for (int i = survivors.Count - 1; i >= 0; i--)
        {
            Survivor survivor = survivors[i];
            if (survivor == null)
            {
                survivors.RemoveAt(i);
                continue;
            }

            survivor.PrepareEngineerForWaveFailureRetreat();
        }
    }

    // 등록된 좀비 스포너의 destinations 중 하나를 무작위로 반환한다(생존자 도피 연출용)
    private bool TryGetRandomFleeDestination(out Transform destination)
    {
        destination = null;
        if (zombieSpawners.Count == 0)
        {
            return false;
        }

        int startIndex = UnityEngine.Random.Range(0, zombieSpawners.Count);
        for (int offset = 0; offset < zombieSpawners.Count; offset++)
        {
            ZombieSpawner spawner = zombieSpawners[(startIndex + offset) % zombieSpawners.Count];
            if (spawner == null || spawner.destinations == null || spawner.destinations.Length == 0)
            {
                continue;
            }

            int destinationStartIndex = UnityEngine.Random.Range(0, spawner.destinations.Length);
            for (int i = 0; i < spawner.destinations.Length; i++)
            {
                Transform candidate = spawner.destinations[(destinationStartIndex + i) % spawner.destinations.Length];
                if (candidate != null)
                {
                    destination = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    // 등록된 생존자에게 방어선 복귀 명령을 전달한다
    private void CommandSurvivorsToRestoredPoint(int defenseLineIndex, Transform restoredPoint)
    {
        if (restoredPoint == null)
        {
            Debug.LogWarning("[GameManager] 방어선 복귀 포인트가 할당되지 않았습니다.", this);
            return;
        }

        int survivorCount = 0;
        for (int i = survivors.Count - 1; i >= 0; i--)
        {
            Survivor survivor = survivors[i];
            if (survivor == null)
            {
                survivors.RemoveAt(i);
                continue;
            }

            survivor.StartDefenseLineReturn(defenseLineIndex, restoredPoint);
            survivorCount++;
        }

        //Debug.Log($"[GameManager] {survivorCount}명의 생존자에게 방어선 {defenseLineIndex} 복귀 명령 전달");
    }
}
