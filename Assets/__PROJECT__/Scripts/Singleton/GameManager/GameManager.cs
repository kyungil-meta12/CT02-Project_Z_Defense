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
    private readonly List<Survivor> survivors = new List<Survivor>(16);
    private readonly List<ZombieSpawner> zombieSpawners = new List<ZombieSpawner>(2);
    private Coroutine gameOverCoroutine;
    private Coroutine quitGameCoroutine;
    private bool isWaveProgressionPaused;
    private bool suppressDefenseLineRestore;

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
        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null || !HasRegisteredDefenseLineSlots(defenseLine))
            {
                continue;
            }

            defenseLine.isBreached = !IsDefenseLineFullyBuilt(i);
            ApplyDefenseLineTurretBaseState(i, !defenseLine.isBreached);
            if (defenseLine.isBreached)
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
        ReassignAllEngineers();
        ResetConstructionWorkersForWaveRestart();
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

    // 웨이브 재시작 시 건축노동자의 후퇴 방어선 상태를 복구한다
    private void ResetConstructionWorkersForWaveRestart()
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
        KillCount = 0;
        DestKillCount = 0;
    }

    [Serializable]
    private class WaveSaveData
    {
        public int Wave;
        public int HighestReachedWave;
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

        int retreatedCount = 0;
        for (int i = survivors.Count - 1; i >= 0; i--)
        {
            Survivor survivor = survivors[i];
            if (survivor == null)
            {
                survivors.RemoveAt(i);
                continue;
            }

            //Debug.Log($"[GameManager] {survivor.name}에게 방어선 {defenseLineIndex} 대피 명령 전달");
            survivor.StartDefenseLineRetreat(defenseLineIndex, retreatPoint, isGateBreached);
            retreatedCount++;
        }

        //Debug.Log($"[GameManager] {retreatedCount}명의 생존자에게 방어선 {defenseLineIndex} 대피 명령 전달 완료");
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
