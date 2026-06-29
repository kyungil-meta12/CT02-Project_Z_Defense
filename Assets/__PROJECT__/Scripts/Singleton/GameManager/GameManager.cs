using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 웨이브 진행, 게임 배속, 방어선 상태, 생존자/장애물 등록을 관리한다.
/// </summary>
public class GameManager : MonoBehaviour
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

    [Header("방어선")]
    [SerializeField] private List<DefenseLineEntry> defenseLines = new List<DefenseLineEntry>(DEFAULT_DEFENSE_LINE_COUNT);

    [Header("게임오버")]
    [SerializeField] private GameOverPanelUI gameOverPanelUI;
    [SerializeField, Min(0.0f)] private float gameOverFadeInDuration = 10.0f;
    [SerializeField, Min(0.0f)] private float gameOverFadeOutDuration = 10.0f;

    public event Action<int> OnWaveIncrease; // 웨이브 증가 이벤트
    public event Action<int> OnWaveDecrease; // 웨이브 감소 이벤트
    private readonly List<Survivor> survivors = new List<Survivor>(16);
    private readonly List<ZombieSpawner> zombieSpawners = new List<ZombieSpawner>(2);
    private Coroutine gameOverCoroutine;
    private bool isWaveProgressionPaused;
    private bool suppressDefenseLineRestore;

    public int Wave{ get; private set; } = 1;
    public int KillCount{ get; private set; }= 0; // 현재 킬 카운트
    public int DestKillCount{ get; private set; } = 0; // 목표 킬 카운트

    [Header("시작 웨이브")] [Min(1)] public int startWave = 1;
    [Header("웨이브 클리어 보너스")] [Min(0)] public int waveClearCoinBonusPercentage = 20; // 웨이브 클리어 시 웨이브 동안 모은 코인의 이 퍼센트만큼을 보너스로 지급
    [Header("게임 배속")]
    [SerializeField, Min(MIN_TIME_SCALE)] private float startTimeScale = 1f;
    [SerializeField, HideInInspector] private float baseFixedDeltaTime = DEFAULT_FIXED_DELTA_TIME;

    public float StartTimeScale => startTimeScale;
    public float CurrentTimeScale => Time.timeScale;
    public bool IsWaveProgressionPaused => isWaveProgressionPaused;

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
        SetGameTimeScale(startTimeScale);

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
            KillCount = 0;
            Wave++;
            InventorySystem.Inst.AddCoinBouns(waveClearCoinBonusPercentage);
            OnWaveIncrease?.Invoke(Wave);
        }
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
        ApplyDefenseLineTurretBaseState(defenseLineIndex, false);
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

        gameOverCoroutine = StartCoroutine(GameOverSequence());
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

    // 저장된 슬롯 진행도를 기준으로 모든 방어선을 다시 배치한다
    private void RebuildAllDefenseLines()
    {
        suppressDefenseLineRestore = true;

        for (int i = 0; i < defenseLines.Count; i++)
        {
            DefenseLineEntry defenseLine = defenseLines[i];
            if (defenseLine == null)
            {
                continue;
            }

            defenseLine.isBreached = false;
            ApplyDefenseLineTurretBaseState(i, true);

            if (defenseLine.obstacleSlots == null)
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

                slot.TryRebuildStoredObstacleWithoutCost(out _);
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

    // 현재 웨이브의 이전 웨이브를 처음부터 다시 시작할 수 있도록 준비한다
    private void PreparePreviousWaveRestart()
    {
        Wave = Mathf.Max(1, Wave - 1);
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
