using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 생존자의 이동, 치료, 역할 전환, 수리, 엔지니어 배치를 관리한다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class Survivor : MonoBehaviour
{
    // 역할 외형 상태별 Mesh와 Material 세트를 보관한다
    [System.Serializable]
    private class VisualSet
    {
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material[] materials = System.Array.Empty<Material>();

        public Mesh Mesh => mesh;
        public Material[] Materials => materials;
    }

    // 역할별 렌더러 교체에 사용할 Inspector 표시 데이터를 보관한다
    [System.Serializable]
    private class RoleVisualEntry
    {
        [SerializeField] private SurvivorRole role;
        [SerializeField] private VisualSet normal;
        [SerializeField] private VisualSet wounded;

        // 현재 외형 상태에 맞는 외형 세트를 반환한다
        public VisualSet GetVisualSet(SurvivorVisualCondition condition)
        {
            if (condition == SurvivorVisualCondition.Wounded && wounded != null && wounded.Mesh != null)
            {
                return wounded;
            }

            return normal != null && normal.Mesh != null ? normal : null;
        }
    }

    private enum SurvivorState
    {
        Idle,
        MoveToTarget,
        Repairing,
        Retreating,
        ReturningToDefensePoint,
        RescueEntering,
        TreatmentReady,
        MovingToHospital,
        InTreatment,
        ReturningFromHospital,
        RoleSelectionReady,
        EngineerReady,
        MovingToEngineerStandby,
        EngineerAssigned,
        ReturningToEngineerGathering,
        Vaulting
    }

    private enum SurvivorVisualCondition
    {
        Normal,
        Wounded
    }

    private const int NO_DEFENSE_LINE = -1;
    private const int VAULT_HIT_BUFFER_SIZE = 4;

    [Header("생존자 스펙")]
    public SurvivorSpec spec;

    [Header("애니메이션")]
    [SerializeField] private string moveSpeedParameter = "MoveSpeed";
    [SerializeField] private string repairParameter = "IsRepairing";
    [SerializeField] private string vaultTriggerParameter = "Vault";
    [SerializeField] private float baseRootMotionMoveSpeed = 3.5f;

    [Header("탐색 최적화")]
    [SerializeField] private float minTargetSearchInterval = 0.25f;
    [SerializeField] private float destinationRefreshInterval = 0.5f;
    [SerializeField] private float moveTimeout = 8f;
    [SerializeField, Min(0.1f)] private float defenseMoveWarningInterval = 5f;

    [Header("방어선 이동")]
    [SerializeField] private float defensePointStoppingDistance = 0.4f;
    [SerializeField] private float defensePointMoveTimeout = 12f;
    [SerializeField, Min(0.05f)] private float defensePointRetryInterval = 0.5f;
    [SerializeField, Min(0.1f)] private float navMeshRecoverySampleDistance = 1.5f;

    [Header("엔지니어 이동")]
    [SerializeField, Min(0.1f)] private float engineerStandbyArriveDistance = 2.0f;

    [Header("구출 및 치료")]
    [SerializeField] private SurvivorRole role = SurvivorRole.constructionWorker;
    [SerializeField] private GameObject visibleRoot;
    [SerializeField] private Collider interactionCollider;
    [SerializeField, Min(0f)] private float defaultTreatmentDuration = 8f;
    [SerializeField] private Transform hospitalPoint;
    [SerializeField] private Transform finalRearPoint;
    [SerializeField] private bool registerWithGameManagerOnEnable = true;

    [Header("역할 외형")]
    [SerializeField] private SurvivorVisualCondition visualCondition = SurvivorVisualCondition.Normal;
    [SerializeField] private List<RoleVisualEntry> roleVisualEntries = new List<RoleVisualEntry>();
    private Mesh defaultRoleMesh;
    private Material[] defaultRoleMaterials = System.Array.Empty<Material>();

    [Header("장애물 넘기")]
    [SerializeField] private LayerMask vaultObstacleLayerMask;
    [SerializeField] private float vaultDetectionInterval = 0.15f;
    [SerializeField] private float vaultDetectionDistance = 1.2f;
    [SerializeField] private float vaultDetectionRadius = 0.35f;
    [SerializeField] private float vaultDuration = 0.8f;
    [SerializeField] private float vaultForwardOffset = 1.4f;
    [SerializeField] private float vaultVerticalOffset = 0.05f;
    [SerializeField] private float vaultCooldown = 0.6f;

    private readonly RaycastHit[] vaultHits = new RaycastHit[VAULT_HIT_BUFFER_SIZE];
    private Animator anim;
    private NavMeshAgent agent;
    private Obstacle repairTarget;
    private Transform defenseMoveTarget;
    private SurvivorState state;
    private SurvivorState stateBeforeVault;
    private Vector3 vaultStartPosition;
    private Vector3 vaultEndPosition;
    private float searchTimer;
    private float destinationRefreshTimer;
    private float moveTimer;
    private float vaultTimer;
    private float vaultDetectionTimer;
    private float nextVaultTime;
    private float defenseMoveRetryTimer;
    private float runtimeRepairRateMultiplier = 1.0f;
    private int moveSpeedHash;
    private int repairHash;
    private int vaultHash;
    private int activeDefenseLineIndex = NO_DEFENSE_LINE;
    private int repairTargetDefenseLineIndex = NO_DEFENSE_LINE;
    private bool hasMoveSpeedParameter;
    private bool hasRepairParameter;
    private bool hasVaultParameter;
    private bool loggedMissingVaultParameter;
    private float treatmentTimer;
    private float nextDefenseMoveWarningTime;
    private bool isInitializedAsRescueSurvivor;
    private TurretEngineerBuffReceiver assignedEngineerBuffReceiver;
    private TurretBaseSlot assignedTurretSlot;
    private SkinnedMeshRenderer roleSkinnedMeshRenderer;

    public int ActiveDefenseLineIndex => activeDefenseLineIndex;
    public SurvivorRole Role => role;
    public bool CanRepairObstacles => role == SurvivorRole.constructionWorker;
    public bool CanRequestTreatment => role == SurvivorRole.survivor && state == SurvivorState.TreatmentReady;
    public bool CanAssignRole => role == SurvivorRole.survivor && state == SurvivorState.RoleSelectionReady;
    public bool CanBeginEngineerAssignment => role == SurvivorRole.engineer && (state == SurvivorState.EngineerReady || state == SurvivorState.EngineerAssigned);
    public bool IsMovingForInteraction => IsAgentMoveState() && agent != null && agent.enabled && agent.isOnNavMesh && agent.desiredVelocity.sqrMagnitude > 0.01f;
    public bool IsWaitingForInteraction => role == SurvivorRole.survivor && !CanRequestTreatment && !CanAssignRole && !IsMovingForInteraction;

    // 현재 역할과 치료 진행 상태를 저장 항목으로 반환한다
    public SurvivorSaveEntry CaptureSaveEntry()
    {
        return new SurvivorSaveEntry
        {
            Role = role,
            RestoreStage = GetRestoreStage()
        };
    }

    // 저장된 역할과 치료 진행 상태를 집결지 대기 상태로 복원한다
    public void RestoreSaveEntry(SurvivorSaveEntry saveEntry, Transform hospitalPoint_, Transform finalRearPoint_, float treatmentDuration)
    {
        if (saveEntry == null)
        {
            return;
        }

        ConfigureRescueFlow(hospitalPoint_, finalRearPoint_, treatmentDuration);
        ClearRepairTarget();
        ClearEngineerAssignment();
        assignedTurretSlot = null;
        defenseMoveTarget = null;
        activeDefenseLineIndex = NO_DEFENSE_LINE;
        isInitializedAsRescueSurvivor = false;
        role = saveEntry.Role;
        SetInteractionVisible(true);

        if (role == SurvivorRole.survivor && saveEntry.RestoreStage == SurvivorRestoreStage.TreatmentPending)
        {
            visualCondition = SurvivorVisualCondition.Wounded;
            ApplyRoleVisual();
            ChangeState(SurvivorState.TreatmentReady);
            return;
        }

        visualCondition = SurvivorVisualCondition.Normal;
        ApplyRoleVisual();
        ChangeState(role == SurvivorRole.survivor ? SurvivorState.RoleSelectionReady : GetIdleStateForRole());
    }

    // 필요한 컴포넌트와 애니메이터 파라미터를 초기화한다
    private void Awake()
    {
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        moveSpeedHash = Animator.StringToHash(moveSpeedParameter);
        repairHash = Animator.StringToHash(repairParameter);
        vaultHash = Animator.StringToHash(vaultTriggerParameter);
        hasMoveSpeedParameter = HasAnimatorParameter(moveSpeedHash);
        hasRepairParameter = HasAnimatorParameter(repairHash);
        hasVaultParameter = HasAnimatorParameter(vaultHash);
        AutoBindInteractionReferences();
        AutoBindRoleVisualReferences();
        CacheDefaultRoleVisual();
        ConfigureDefaultVaultLayerMask();
        ConfigureAgent(spec != null ? spec.repairRange : 0f);
        ApplyRoleVisual();
    }

    // 활성화될 때 기본 대기 상태로 초기화한다
    private void OnEnable()
    {
        if (registerWithGameManagerOnEnable)
        {
            RegisterWithGameManager();
        }

        ChangeState(isInitializedAsRescueSurvivor ? SurvivorState.RescueEntering : GetIdleStateForRole());
        defenseMoveTarget = null;
        activeDefenseLineIndex = NO_DEFENSE_LINE;
        searchTimer = 0f;
        destinationRefreshTimer = 0f;
        moveTimer = 0f;
        vaultTimer = 0f;
        vaultDetectionTimer = 0f;
        defenseMoveRetryTimer = 0f;
        nextDefenseMoveWarningTime = 0f;
    }

    // 시작 시 게임 매니저 등록 누락을 보완한다
    private void Start()
    {
        if (registerWithGameManagerOnEnable)
        {
            RegisterWithGameManager();
        }
    }

    // 비활성화될 때 수리 예약과 게임 매니저 등록을 해제한다
    private void OnDisable()
    {
        ClearRepairTarget();
        ClearEngineerAssignment();
        defenseMoveTarget = null;

        if (GameManager.Inst != null)
        {
            GameManager.Inst.UnregisterSurvivor(this);
        }
    }

    // 매 프레임 현재 상태를 갱신한다
    private void Update()
    {
        if (spec == null)
        {
            Debug.LogError("[Survivor] 생존자 스펙이 할당되지 않았습니다.", this);
            enabled = false;
            return;
        }

        UpdateState();
        UpdateAnimation();
    }

    // 방어선 붕괴 시 생존자를 지정한 대피 포인트로 이동시킨다
    public void StartDefenseLineRetreat(int defenseLineIndex, Transform retreatPoint, bool isGateBreached)
    {
        if (retreatPoint == null)
        {
            Debug.LogWarning("[Survivor] 대피 포인트가 없어 방어선 대피 명령을 무시합니다.", this);
            return;
        }

        if (!isGateBreached && role != SurvivorRole.constructionWorker)
        {
            return;
        }

        if (activeDefenseLineIndex > defenseLineIndex && state != SurvivorState.ReturningToDefensePoint)
        {
            return;
        }

        if (isGateBreached)
        {
            ClearEngineerAssignment();
        }

        activeDefenseLineIndex = defenseLineIndex;
        defenseMoveTarget = retreatPoint;
        ClearRepairTarget();
        ChangeState(SurvivorState.Retreating);
    }

    // 방어선 복구 시 생존자를 지정한 복귀 포인트로 이동시킨다
    public void StartDefenseLineReturn(int defenseLineIndex, Transform restoredPoint)
    {
        if (restoredPoint == null)
        {
            Debug.LogWarning("[Survivor] 복귀 포인트가 없어 방어선 복귀 명령을 무시합니다.", this);
            return;
        }

        if (role != SurvivorRole.constructionWorker)
        {
            return;
        }

        if (activeDefenseLineIndex > defenseLineIndex)
        {
            return;
        }

        activeDefenseLineIndex = defenseLineIndex;
        defenseMoveTarget = restoredPoint;
        ClearRepairTarget();
        ChangeState(SurvivorState.ReturningToDefensePoint);
    }

    // 웨이브 재시작 시 후퇴 상태를 정리하고 방어선 복귀를 준비한다
    public void ResetDefenseLineStateForWaveRestart(Transform restoredPoint)
    {
        if (role != SurvivorRole.constructionWorker)
        {
            return;
        }

        ClearRepairTarget();

        if (restoredPoint == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            ResetDefenseLineStateImmediatelyForWaveRestart();
            return;
        }

        defenseMoveTarget = restoredPoint;
        ChangeState(SurvivorState.ReturningToDefensePoint);
    }

    // 구출 생존자의 이동 기준 지점과 치료 시간을 설정한다
    public void ConfigureRescueFlow(Transform hospitalPoint_, Transform finalRearPoint_, float treatmentDuration)
    {
        hospitalPoint = hospitalPoint_;
        finalRearPoint = finalRearPoint_;
        defaultTreatmentDuration = Mathf.Max(0f, treatmentDuration);
    }

    // 지정 위치로 생존자를 즉시 이동시킨다
    public void SetPosition(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.Warp(target.position);
        }
        else
        {
            transform.position = target.position;
        }

        transform.rotation = target.rotation;
    }

    // 스폰 지점에서 최후방 지점으로 뛰어오도록 시작한다
    public void StartRescueRun(Transform finalRearPoint_)
    {
        role = SurvivorRole.survivor;
        visualCondition = SurvivorVisualCondition.Wounded;
        ApplyRoleVisual();
        isInitializedAsRescueSurvivor = true;
        finalRearPoint = finalRearPoint_;
        defenseMoveTarget = finalRearPoint;
        ClearRepairTarget();
        ClearEngineerAssignment();
        SetInteractionVisible(true);
        ChangeState(SurvivorState.RescueEntering);
    }

    // 치료 버튼 입력으로 병원 이동을 시작한다
    public bool TryStartTreatment()
    {
        if (!CanRequestTreatment)
        {
            Debug.LogWarning("[Survivor] 치료를 시작할 수 없는 상태입니다.", this);
            return false;
        }

        if (hospitalPoint == null)
        {
            Debug.LogWarning("[Survivor] 병원 이동 포인트가 없어 치료를 시작할 수 없습니다.", this);
            return false;
        }

        defenseMoveTarget = hospitalPoint;
        ClearRepairTarget();
        ClearEngineerAssignment();
        ChangeState(SurvivorState.MovingToHospital);
        return true;
    }

    // 치료된 생존자에게 새 역할을 부여한다
    public bool TryAssignRole(SurvivorRole nextRole)
    {
        if (!CanAssignRole || nextRole == SurvivorRole.survivor)
        {
            return false;
        }

        role = nextRole;
        visualCondition = SurvivorVisualCondition.Normal;
        ApplyRoleVisual();
        ClearRepairTarget();
        ClearEngineerAssignment();
        ChangeState(GetIdleStateForRole());
        GameManager.Inst?.MarkSurvivorStateDirty();
        return true;
    }

    // 엔지니어를 터렛 버프 대상 선택 대기 상태로 전환한다
    public bool TryBeginEngineerAssignment()
    {
        if (!CanBeginEngineerAssignment)
        {
            return false;
        }

        ClearEngineerAssignment();
        return true;
    }

    // 엔지니어를 지정 터렛 버프 수신기에 배치한다
    public bool TryAssignEngineerToTurret(TurretEngineerBuffReceiver buffReceiver, Transform standbyPoint)
    {
        return TryAssignEngineerToTurret(buffReceiver, null, standbyPoint);
    }

    // 엔지니어를 지정 터렛 슬롯의 버프 수신기에 배치한다
    public bool TryAssignEngineerToTurret(TurretEngineerBuffReceiver buffReceiver, TurretBaseSlot turretSlot, Transform standbyPoint)
    {
        if (role != SurvivorRole.engineer || buffReceiver == null)
        {
            return false;
        }

        if (!buffReceiver.CanRegisterEngineer(this))
        {
            return false;
        }

        assignedEngineerBuffReceiver = buffReceiver;
        assignedTurretSlot = turretSlot;
        ClearRepairTarget();

        if (standbyPoint == null)
        {
            if (!CompleteEngineerBoarding())
            {
                ClearEngineerAssignment();
                ChangeState(SurvivorState.EngineerReady);
                return false;
            }
        }
        else
        {
            defenseMoveTarget = standbyPoint;
            ChangeState(SurvivorState.MovingToEngineerStandby);
        }

        return true;
    }

    // 마지막으로 배치됐던 터렛 슬롯에 엔지니어를 다시 등록한다
    public bool TryReassignEngineerToStoredTurret()
    {
        if (role != SurvivorRole.engineer || assignedTurretSlot == null || assignedTurretSlot.CurrentTurret == null)
        {
            return false;
        }

        TurretEngineerBuffReceiver buffReceiver = assignedTurretSlot.CurrentTurret.GetComponent<TurretEngineerBuffReceiver>();
        if (buffReceiver == null)
        {
            buffReceiver = assignedTurretSlot.CurrentTurret.gameObject.AddComponent<TurretEngineerBuffReceiver>();
        }

        return TryAssignEngineerToTurret(buffReceiver, assignedTurretSlot, assignedTurretSlot.BuildPoint);
    }

    // 배치된 터렛이 사라졌을 때 엔지니어 배치 상태를 해제한다
    public void ReleaseEngineerAssignment(TurretEngineerBuffReceiver buffReceiver)
    {
        if (assignedEngineerBuffReceiver != buffReceiver)
        {
            return;
        }

        assignedEngineerBuffReceiver = null;
        if (role == SurvivorRole.engineer)
        {
            SetInteractionVisible(true);
            defenseMoveTarget = null;
            ChangeState(SurvivorState.EngineerReady);
        }
    }

    // 탑승 중인 엔지니어를 터렛에서 내리고 집결 지점으로 이동시킨다
    public bool TryDismountEngineerFromTurret()
    {
        if (role != SurvivorRole.engineer || assignedEngineerBuffReceiver == null)
        {
            return false;
        }

        ReturnEngineerToGathering();
        return true;
    }

    // 엔지니어의 기존 터렛 배치를 정리하고 집결 지점으로 복귀시킨다
    public void ReturnEngineerToGathering()
    {
        if (role != SurvivorRole.engineer)
        {
            return;
        }

        ClearEngineerAssignment();
        assignedTurretSlot = null;
        SetInteractionVisible(true);

        if (finalRearPoint == null)
        {
            defenseMoveTarget = null;
            ChangeState(SurvivorState.EngineerReady);
            return;
        }

        defenseMoveTarget = finalRearPoint;
        ChangeState(SurvivorState.ReturningToEngineerGathering);
    }

    // NavMeshAgent 이동 수치를 지정한 정지 거리로 맞춘다
    private void ConfigureAgent(float stoppingDistance)
    {
        if (spec == null)
        {
            return;
        }

        agent.speed = spec.GetMoveSpeed();
        agent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
        agent.updateRotation = false;
        agent.updatePosition = false;
    }

    // 현재 상태에 맞는 갱신 로직을 실행한다
    private void UpdateState()
    {
        switch (state)
        {
            case SurvivorState.Idle:
                UpdateIdle();
                break;
            case SurvivorState.MoveToTarget:
                UpdateMoveToTarget();
                break;
            case SurvivorState.Repairing:
                UpdateRepairing();
                break;
            case SurvivorState.Retreating:
            case SurvivorState.ReturningToDefensePoint:
            case SurvivorState.RescueEntering:
            case SurvivorState.MovingToHospital:
            case SurvivorState.ReturningFromHospital:
            case SurvivorState.MovingToEngineerStandby:
            case SurvivorState.ReturningToEngineerGathering:
                UpdateDefensePointMove();
                break;
            case SurvivorState.InTreatment:
                UpdateTreatment();
                break;
            case SurvivorState.Vaulting:
                UpdateVaulting();
                break;
        }
    }

    // 대기 중 일정 주기로 수리 대상을 찾는다
    private void UpdateIdle()
    {
        if (!CanRepairObstacles)
        {
            return;
        }

        searchTimer -= Time.deltaTime;
        if (searchTimer > 0f)
        {
            return;
        }

        searchTimer = GetTargetSearchInterval();
        TryFindRepairTarget();
    }

    // 수리 대상까지 이동한다
    private void UpdateMoveToTarget()
    {
        if (!IsValidRepairTarget())
        {
            ClearRepairTarget();
            ChangeState(SurvivorState.Idle);
            return;
        }

        agent.isStopped = false;
        moveTimer += Time.deltaTime;
        destinationRefreshTimer -= Time.deltaTime;
        vaultDetectionTimer -= Time.deltaTime;

        if (!agent.isOnNavMesh)
        {
            AbortRepairTarget("[Survivor] NavMesh 위에 없어 수리 예약을 해제합니다.");
            return;
        }

        agent.nextPosition = transform.position;

        if (!RefreshRepairDestinationIfNeeded())
        {
            AbortRepairTarget("[Survivor] 수리 대상까지 경로를 만들 수 없어 예약을 해제합니다.");
            return;
        }

        if (TryStartVault())
        {
            return;
        }

        if (moveTimeout > 0f && moveTimer >= moveTimeout)
        {
            LogDefenseMoveWarning("[Survivor] 수리 대상까지 이동 시간이 오래 걸리고 있습니다.");
        }

        if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            AbortRepairTarget("[Survivor] 수리 대상까지 유효한 경로가 없어 예약을 해제합니다.");
            return;
        }

        RotateByAgentPath();

        if (IsInRepairRange())
        {
            ChangeState(SurvivorState.Repairing);
        }
    }

    // 대상의 내구도를 최대 체력 비례 수리율 기준으로 회복한다
    private void UpdateRepairing()
    {
        if (!IsValidRepairTarget())
        {
            ClearRepairTarget();
            ChangeState(SurvivorState.Idle);
            return;
        }

        if (!IsInRepairRange())
        {
            ChangeState(SurvivorState.MoveToTarget);
            return;
        }

        agent.isStopped = true;
        LookAt(repairTarget.transform.position);

        float repairAmount = CalculateRepairAmount(repairTarget);
        repairTarget.Repair(repairAmount);

        if (!repairTarget.IsDamaged)
        {
            if (repairTarget.hpUI != null)
            {
                repairTarget.hpUI.gameObject.SetActive(false);
            }

            ClearRepairTarget();
            ChangeState(SurvivorState.Idle);
        }
    }

    // 대상 최대 체력과 런타임 배율을 반영한 이번 프레임 수리량을 계산한다
    private float CalculateRepairAmount(Obstacle target)
    {
        if (spec == null || target == null || target.TotalHp <= 0.0f || runtimeRepairRateMultiplier <= 0.0f)
        {
            return 0.0f;
        }

        float repairRatioPerSecond = spec.GetRepairMaxHpRatioPerSecond();
        if (repairRatioPerSecond <= 0.0f)
        {
            return 0.0f;
        }

        return target.TotalHp * repairRatioPerSecond * runtimeRepairRateMultiplier * Time.deltaTime;
    }

    // 아이템 등 외부 효과에서 수리 속도 배율을 설정한다
    public void SetRepairRateMultiplier(float multiplier)
    {
        runtimeRepairRateMultiplier = Mathf.Max(0.0f, multiplier);
    }

    // 방어선 대피 또는 복귀 포인트까지 이동한다
    private void UpdateDefensePointMove()
    {
        if (defenseMoveRetryTimer > 0f)
        {
            defenseMoveRetryTimer -= Time.deltaTime;
            return;
        }

        if (defenseMoveTarget == null)
        {
            if (!TryRestoreDefenseMoveTarget())
            {
                AbortDefensePointMove("[Survivor] 방어선 이동 포인트가 사라져 이동을 중단합니다.", false);
            }

            return;
        }

        agent.isStopped = false;
        moveTimer += Time.deltaTime;
        destinationRefreshTimer -= Time.deltaTime;
        vaultDetectionTimer -= Time.deltaTime;

        if (!agent.isOnNavMesh)
        {
            if (!TryRecoverAgentToNavMesh())
            {
                AbortDefensePointMove("[Survivor] NavMesh 위에 없어 방어선 이동을 잠시 후 재시도합니다.", true);
            }

            return;
        }

        agent.nextPosition = transform.position;

        if (!RefreshDefenseDestinationIfNeeded())
        {
            AbortDefensePointMove("[Survivor] 방어선 포인트까지 경로를 만들 수 없어 잠시 후 재시도합니다.", true);
            return;
        }

        if (TryStartVault())
        {
            return;
        }

        if (defensePointMoveTimeout > 0f && moveTimer >= defensePointMoveTimeout)
        {
            LogDefenseMoveWarning("[Survivor] 방어선 포인트까지 이동 시간이 오래 걸리고 있습니다.");
        }

        if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            AbortDefensePointMove("[Survivor] 방어선 포인트까지 유효한 경로가 없어 잠시 후 재시도합니다.", true);
            return;
        }

        RotateByAgentPath();

        if (HasReachedDefensePoint())
        {
            SurvivorState completedState = state;
            CompleteDefensePointMove(completedState);
        }
    }

    // 치료 대기 시간을 갱신하고 완료 시 최후방 복귀를 시작한다
    private void UpdateTreatment()
    {
        treatmentTimer -= Time.deltaTime;
        if (treatmentTimer > 0f)
        {
            return;
        }

        SetInteractionVisible(true);
        visualCondition = SurvivorVisualCondition.Normal;
        ApplyRoleVisual();

        if (finalRearPoint == null)
        {
            ChangeState(SurvivorState.RoleSelectionReady);
            GameManager.Inst?.MarkSurvivorStateDirty();
            return;
        }

        defenseMoveTarget = finalRearPoint;
        ChangeState(SurvivorState.ReturningFromHospital);
    }

    // 장애물 넘기 중 위치 보간을 갱신한다
    private void UpdateVaulting()
    {
        vaultTimer += Time.deltaTime;
        float duration = Mathf.Max(0.05f, vaultDuration);
        float progress = Mathf.Clamp01(vaultTimer / duration);
        float smoothedProgress = progress * progress * (3f - 2f * progress);
        transform.position = Vector3.Lerp(vaultStartPosition, vaultEndPosition, smoothedProgress);

        if (progress < 1f)
        {
            return;
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.nextPosition = transform.position;
        }

        ChangeState(stateBeforeVault);
    }

    // 이동 애니메이션 루트모션으로 실제 위치를 갱신한다
    private void OnAnimatorMove()
    {
        if (!IsAgentMoveState() || agent == null || !agent.isOnNavMesh)
        {
            return;
        }

        if (state == SurvivorState.MoveToTarget && repairTarget == null)
        {
            return;
        }

        if (state != SurvivorState.MoveToTarget && defenseMoveTarget == null)
        {
            return;
        }

        Vector3 moveDelta = anim.deltaPosition;
        moveDelta.y = 0f;

        float moveDistance = moveDelta.magnitude;
        if (moveDistance <= 0.0001f)
        {
            moveDistance = spec != null ? spec.GetMoveSpeed() * Time.deltaTime : agent.speed * Time.deltaTime;
        }

        if (moveDistance <= 0.0001f)
        {
            return;
        }

        Vector3 moveDirection = agent.desiredVelocity;
        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            moveDirection = GetDirectionToCurrentMoveTarget();
        }

        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        moveDirection.Normalize();
        Vector3 targetPosition = transform.position + moveDirection * moveDistance;
        targetPosition.y = transform.position.y;

        Vector3 currentTargetDirection = GetDirectionToCurrentMoveTarget();
        if (currentTargetDirection.sqrMagnitude > 0.0001f && moveDistance * moveDistance >= currentTargetDirection.sqrMagnitude)
        {
            targetPosition = transform.position + currentTargetDirection;
            targetPosition.y = transform.position.y;
        }

        transform.position = targetPosition;
        agent.nextPosition = transform.position;
    }

    // GameManager에서 수리 대상을 가져온다
    private void TryFindRepairTarget()
    {
        if (!CanRepairObstacles || GameManager.Inst == null)
        {
            return;
        }

        if (GameManager.Inst.TryGetRepairTarget(transform.position, this, out Obstacle obstacle))
        {
            repairTarget = obstacle;
            repairTargetDefenseLineIndex = TryGetObstacleDefenseLineIndex(obstacle, out int defenseLineIndex) ? defenseLineIndex : NO_DEFENSE_LINE;
            ChangeState(SurvivorState.MoveToTarget);
        }
    }

    // 수리 대상 탐색 주기를 최소값 이상으로 보정한다
    private float GetTargetSearchInterval()
    {
        return Mathf.Max(0.1f, Mathf.Max(minTargetSearchInterval, spec.targetSearchInterval));
    }

    // 수리 목적지 갱신을 일정 주기로 제한해 NavMesh 비용을 줄인다
    private bool RefreshRepairDestinationIfNeeded()
    {
        if (destinationRefreshTimer > 0f)
        {
            return true;
        }

        destinationRefreshTimer = Mathf.Max(0.1f, destinationRefreshInterval);
        return repairTarget != null && agent.SetDestination(repairTarget.transform.position);
    }

    // 방어선 목적지 갱신을 일정 주기로 제한해 NavMesh 비용을 줄인다
    private bool RefreshDefenseDestinationIfNeeded()
    {
        if (destinationRefreshTimer > 0f)
        {
            return true;
        }

        destinationRefreshTimer = Mathf.Max(0.1f, destinationRefreshInterval);
        return defenseMoveTarget != null && agent.SetDestination(defenseMoveTarget.position);
    }

    // 이동 실패 상황에서 예약을 해제하고 재탐색을 지연한다
    private void AbortRepairTarget(string message)
    {
        ClearRepairTarget();
        ChangeState(SurvivorState.Idle);
        searchTimer = GetTargetSearchInterval();
        Debug.LogWarning(message, this);
    }

    // 방어선 이동 실패 상황에서 명령을 해제한다
    private void AbortDefensePointMove(string message, bool retryMove)
    {
        if (state == SurvivorState.MovingToEngineerStandby)
        {
            defenseMoveTarget = null;
            ClearEngineerAssignment();
            ChangeState(SurvivorState.EngineerReady);
            searchTimer = GetTargetSearchInterval();
            LogDefenseMoveWarning("[Survivor] 엔지니어가 터렛 대기 위치로 이동할 수 없어 버프 배치를 취소합니다.");
            return;
        }

        if (retryMove && defenseMoveTarget != null && IsAgentMoveState())
        {
            agent.isStopped = true;
            moveTimer = 0f;
            destinationRefreshTimer = 0f;
            vaultDetectionTimer = 0f;
            defenseMoveRetryTimer = Mathf.Max(0.05f, defensePointRetryInterval);
            LogDefenseMoveWarning(message);
            return;
        }

        defenseMoveTarget = null;
        ChangeState(GetIdleStateForRole());
        searchTimer = GetTargetSearchInterval();
        LogDefenseMoveWarning(message);
    }

    // 방어선 이동 경고 로그를 일정 시간 간격으로 제한한다
    private void LogDefenseMoveWarning(string message)
    {
        if (Time.time < nextDefenseMoveWarningTime)
        {
            return;
        }

        nextDefenseMoveWarningTime = Time.time + Mathf.Max(0.1f, defenseMoveWarningInterval);
        Debug.Log(message, this);
    }

    // 현재 상태에 맞는 이동 목적지가 비어 있으면 저장된 기준 지점으로 복구한다
    private bool TryRestoreDefenseMoveTarget()
    {
        switch (state)
        {
            case SurvivorState.RescueEntering:
            case SurvivorState.ReturningFromHospital:
                defenseMoveTarget = finalRearPoint;
                break;
            case SurvivorState.MovingToHospital:
                defenseMoveTarget = hospitalPoint;
                break;
            case SurvivorState.MovingToEngineerStandby:
                defenseMoveTarget = assignedTurretSlot == null ? null : assignedTurretSlot.BuildPoint;
                break;
            case SurvivorState.ReturningToEngineerGathering:
                defenseMoveTarget = finalRearPoint;
                break;
        }

        return defenseMoveTarget != null;
    }

    // 에이전트가 NavMesh 밖으로 밀려난 경우 가까운 NavMesh 위치로 복구한다
    private bool TryRecoverAgentToNavMesh()
    {
        if (agent == null || !agent.enabled)
        {
            return false;
        }

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit navMeshHit, Mathf.Max(0.1f, navMeshRecoverySampleDistance), NavMesh.AllAreas))
        {
            return false;
        }

        agent.Warp(navMeshHit.position);
        transform.position = navMeshHit.position;
        agent.nextPosition = transform.position;
        destinationRefreshTimer = 0f;
        moveTimer = 0f;
        return true;
    }

    // 현재 수리 대상이 유효한지 확인한다
    private bool IsValidRepairTarget()
    {
        return repairTarget != null && repairTarget.CanBeRepairedBy(this);
    }

    // 수리 가능 거리 안에 있는지 확인한다
    private bool IsInRepairRange()
    {
        Vector3 offset = repairTarget.transform.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= spec.repairRange * spec.repairRange;
    }

    // 방어선 이동 포인트에 도착했는지 확인한다
    private bool HasReachedDefensePoint()
    {
        if (agent.pathPending)
        {
            return false;
        }

        Vector3 offset = defenseMoveTarget.position - transform.position;
        offset.y = 0f;
        float arriveDistance = GetCurrentArriveDistance();
        if (offset.sqrMagnitude <= arriveDistance * arriveDistance)
        {
            return true;
        }

        return HasReachedAgentPathEnd(arriveDistance);
    }

    // NavMesh 경로 끝에 도달했는지 확인한다
    private bool HasReachedAgentPathEnd(float arriveDistance)
    {
        if (agent == null || !agent.hasPath)
        {
            return false;
        }

        if (agent.pathStatus != NavMeshPathStatus.PathComplete && agent.pathStatus != NavMeshPathStatus.PathPartial)
        {
            return false;
        }

        float remainingDistance = agent.remainingDistance;
        if (float.IsInfinity(remainingDistance) || float.IsNaN(remainingDistance))
        {
            return false;
        }

        return remainingDistance <= Mathf.Max(0.05f, arriveDistance);
    }

    // 방어선 이동 완료 처리를 공통으로 적용한다
    private void CompleteDefensePointMove(SurvivorState completedState)
    {
        defenseMoveTarget = null;
        StopAgentPath();

        if (completedState == SurvivorState.ReturningToDefensePoint)
        {
            activeDefenseLineIndex = NO_DEFENSE_LINE;
        }

        HandleDefensePointMoveCompleted(completedState);
    }

    // 현재 Agent 경로를 정지하고 남은 경로를 정리한다
    private void StopAgentPath()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;
        agent.ResetPath();
    }

    // 현재 이동 상태에 맞는 도착 인정 거리를 반환한다
    private float GetCurrentArriveDistance()
    {
        if (state == SurvivorState.MovingToEngineerStandby)
        {
            return Mathf.Max(0.1f, engineerStandbyArriveDistance);
        }

        return Mathf.Max(0.05f, defensePointStoppingDistance);
    }

    // 대상 방향으로 회전한다
    private void LookAt(Vector3 position)
    {
        Vector3 direction = position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * spec.rotationSpeed);
    }

    // Agent 경로 진행 방향으로 회전한다
    private void RotateByAgentPath()
    {
        Vector3 direction = agent.desiredVelocity;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * spec.rotationSpeed);
    }

    // 현재 이동 경로의 전방 장애물을 감지해 넘기 상태로 전환한다
    private bool TryStartVault()
    {
        if (Time.time < nextVaultTime || vaultDetectionTimer > 0f || vaultObstacleLayerMask.value == 0)
        {
            return false;
        }

        vaultDetectionTimer = Mathf.Max(0.02f, vaultDetectionInterval);

        Vector3 direction = agent.desiredVelocity;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = GetDirectionToCurrentMoveTarget();
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = transform.forward;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        direction.Normalize();

        // 현재 목적지 방향을 기준으로 넘어야 할 장애물인지 확인한다
        Vector3 toTarget = GetDirectionToCurrentMoveTarget();

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            Mathf.Max(0.01f, vaultDetectionRadius),
            direction,
            vaultHits,
            Mathf.Max(0.01f, vaultDetectionDistance),
            vaultObstacleLayerMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = vaultHits[i].collider;
            if (hitCollider == null || hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            Obstacle obstacle = hitCollider.GetComponentInParent<Obstacle>();
            if (obstacle == null)
            {
                continue;
            }

            if (ShouldSkipVaultObstacle(obstacle))
            {
                continue;
            }

            // 장애물 방향 계산
            Vector3 toObstacle = obstacle.transform.position - transform.position;
            toObstacle.y = 0f;

            // 대피 중인 경우: 목적지 방향과 장애물 방향의 내적으로 판단
            // 내적이 양수면 같은 방향(뒤쪽 장애물), 음수면 반대 방향(앞쪽 장애물)
            if (state == SurvivorState.Retreating && toTarget.sqrMagnitude > 0.001f)
            {
                float dot = Vector3.Dot(toTarget.normalized, toObstacle.normalized);
                //Debug.Log($"[Survivor] {name} 대피 중 vault 감지 - 장애물: {obstacle.name}, 내적: {dot:F2}");

                // 내적이 0보다 작으면 앞쪽 장애물이므로 넘지 않음
                if (dot < 0f)
                {
                    //Debug.Log($"[Survivor] {name} 대피 중 앞쪽 장애물 {obstacle.name}은 넘지 않음");
                    continue;
                }
            }

            Vector3 landingPosition = GetVaultLandingPosition(obstacle, direction);
            //Debug.Log($"[Survivor] {name} vault 시작 - 상태: {state}, 장애물: {obstacle.name}");
            StartVault(landingPosition);
            return true;
        }

        return false;
    }

    // 수리 이동 중 넘으면 안 되는 방어선 장애물인지 확인한다
    private bool ShouldSkipVaultObstacle(Obstacle obstacle)
    {
        if (role != SurvivorRole.constructionWorker || state != SurvivorState.MoveToTarget)
        {
            return false;
        }

        if (obstacle == null || repairTarget == null || repairTargetDefenseLineIndex < 0)
        {
            return false;
        }

        if (!TryGetObstacleDefenseLineIndex(obstacle, out int obstacleDefenseLineIndex))
        {
            return false;
        }

        return obstacleDefenseLineIndex == repairTargetDefenseLineIndex;
    }

    // 장애물이 속한 방어선 인덱스를 GameManager에서 조회한다
    private bool TryGetObstacleDefenseLineIndex(Obstacle obstacle, out int defenseLineIndex)
    {
        defenseLineIndex = NO_DEFENSE_LINE;
        return GameManager.Inst != null && GameManager.Inst.TryGetDefenseLineIndex(obstacle, out defenseLineIndex);
    }

    // 현재 이동 상태에 맞는 목적지 방향을 반환한다
    private Vector3 GetDirectionToCurrentMoveTarget()
    {
        if (state == SurvivorState.MoveToTarget && repairTarget != null)
        {
            Vector3 repairDirection = repairTarget.transform.position - transform.position;
            repairDirection.y = 0f;
            return repairDirection;
        }

        if (defenseMoveTarget == null)
        {
            return Vector3.zero;
        }

        Vector3 direction = defenseMoveTarget.position - transform.position;
        direction.y = 0f;
        return direction;
    }

    // 장애물 넘기 착지 위치를 계산한다
    private Vector3 GetVaultLandingPosition(Obstacle obstacle, Vector3 direction)
    {
        Vector3 landingPosition = obstacle.GetVaultLandingPosition(transform.position, direction, vaultForwardOffset, vaultVerticalOffset);
        landingPosition.y = transform.position.y + vaultVerticalOffset;

        if (NavMesh.SamplePosition(landingPosition, out NavMeshHit navMeshHit, 1.0f, NavMesh.AllAreas))
        {
            landingPosition = navMeshHit.position;
        }

        return landingPosition;
    }

    // 장애물 넘기 애니메이션과 위치 이동을 시작한다
    private void StartVault(Vector3 landingPosition)
    {
        stateBeforeVault = state;
        vaultStartPosition = transform.position;
        vaultEndPosition = landingPosition;
        vaultTimer = 0f;
        nextVaultTime = Time.time + Mathf.Max(0.05f, vaultCooldown);
        LookAt(vaultEndPosition);

        if (hasVaultParameter)
        {
            anim.SetTrigger(vaultHash);
        }
        else if (!loggedMissingVaultParameter)
        {
            loggedMissingVaultParameter = true;
            Debug.LogWarning("[Survivor] 장애물 넘기 애니메이터 파라미터가 없어 이동 처리만 실행합니다.", this);
        }

        ChangeState(SurvivorState.Vaulting);
    }

    // 장애물 레이어가 비어 있으면 Obstacle 레이어를 기본값으로 사용한다
    private void ConfigureDefaultVaultLayerMask()
    {
        if (vaultObstacleLayerMask.value != 0)
        {
            return;
        }

        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer >= 0)
        {
            vaultObstacleLayerMask = 1 << obstacleLayer;
        }
    }

    // 게임 매니저에 생존자를 등록한다
    private void RegisterWithGameManager()
    {
        if (GameManager.Inst != null)
        {
            GameManager.Inst.RegisterSurvivor(this);
        }
    }

    // 현재 상태가 NavMeshAgent 이동 상태인지 확인한다
    private bool IsAgentMoveState()
    {
        return state == SurvivorState.MoveToTarget
            || state == SurvivorState.Retreating
            || state == SurvivorState.ReturningToDefensePoint
            || state == SurvivorState.RescueEntering
            || state == SurvivorState.MovingToHospital
            || state == SurvivorState.ReturningFromHospital
            || state == SurvivorState.MovingToEngineerStandby
            || state == SurvivorState.ReturningToEngineerGathering;
    }

    // 상태를 변경하고 진입 처리를 적용한다
    private void ChangeState(SurvivorState nextState)
    {
        state = nextState;

        if (agent == null)
        {
            return;
        }

        agent.isStopped = !IsAgentMoveState();

        if (state == SurvivorState.Idle)
        {
            searchTimer = 0f;
        }

        if (state == SurvivorState.MoveToTarget)
        {
            ConfigureAgent(spec != null ? spec.repairRange : 0f);
            destinationRefreshTimer = 0f;
            vaultDetectionTimer = 0f;
            moveTimer = 0f;
        }

        if (state == SurvivorState.Retreating || state == SurvivorState.ReturningToDefensePoint)
        {
            ConfigureAgent(defensePointStoppingDistance);
            destinationRefreshTimer = 0f;
            vaultDetectionTimer = 0f;
            moveTimer = 0f;
        }

        if (state == SurvivorState.RescueEntering ||
            state == SurvivorState.MovingToHospital ||
            state == SurvivorState.ReturningFromHospital ||
            state == SurvivorState.MovingToEngineerStandby ||
            state == SurvivorState.ReturningToEngineerGathering)
        {
            ConfigureAgent(defensePointStoppingDistance);
            destinationRefreshTimer = 0f;
            vaultDetectionTimer = 0f;
            moveTimer = 0f;
        }

        if (state == SurvivorState.InTreatment)
        {
            treatmentTimer = Mathf.Max(0f, defaultTreatmentDuration);
            SetInteractionVisible(false);
        }
    }

    // 현재 애니메이션 파라미터를 갱신한다
    private void UpdateAnimation()
    {
        if (anim.runtimeAnimatorController == null)
        {
            return;
        }

        bool isMoving = IsAgentMoveState() && agent.desiredVelocity.sqrMagnitude > 0.01f;
        bool isRepairing = state == SurvivorState.Repairing;
        anim.speed = isMoving && baseRootMotionMoveSpeed > 0f ? spec.GetMoveSpeed() / baseRootMotionMoveSpeed : 1f;

        if (hasMoveSpeedParameter)
        {
            anim.SetFloat(moveSpeedHash, isMoving ? 1f : 0f);
        }

        if (hasRepairParameter)
        {
            anim.SetBool(repairHash, isRepairing);
        }
    }

    // 이동 완료 상태에 맞는 후속 상태로 전환한다
    private void HandleDefensePointMoveCompleted(SurvivorState completedState)
    {
        switch (completedState)
        {
            case SurvivorState.RescueEntering:
                ChangeState(SurvivorState.TreatmentReady);
                break;
            case SurvivorState.MovingToEngineerStandby:
                if (!CompleteEngineerBoarding())
                {
                    ClearEngineerAssignment();
                    ChangeState(SurvivorState.EngineerReady);
                }
                break;
            case SurvivorState.ReturningFromHospital:
                ChangeState(SurvivorState.RoleSelectionReady);
                GameManager.Inst?.MarkSurvivorStateDirty();
                break;
            case SurvivorState.ReturningToEngineerGathering:
                ChangeState(SurvivorState.EngineerReady);
                break;
            case SurvivorState.MovingToHospital:
                ChangeState(SurvivorState.InTreatment);
                break;
            default:
                ChangeState(GetIdleStateForRole());
                break;
        }
    }

    // 현재 역할에 맞는 대기 상태를 반환한다
    private SurvivorState GetIdleStateForRole()
    {
        return role == SurvivorRole.engineer ? SurvivorState.EngineerReady : SurvivorState.Idle;
    }

    // 현재 런타임 상태를 저장 가능한 치료 진행 단계로 정규화한다
    private SurvivorRestoreStage GetRestoreStage()
    {
        if (role != SurvivorRole.survivor)
        {
            return SurvivorRestoreStage.RoleSelectionPending;
        }

        if (state == SurvivorState.ReturningFromHospital || state == SurvivorState.RoleSelectionReady)
        {
            return SurvivorRestoreStage.RoleSelectionPending;
        }

        return SurvivorRestoreStage.TreatmentPending;
    }

    // 엔지니어 터렛 배치 등록을 해제한다
    private void ClearEngineerAssignment()
    {
        if (assignedEngineerBuffReceiver == null)
        {
            return;
        }

        assignedEngineerBuffReceiver.UnregisterEngineer(this);
        assignedEngineerBuffReceiver = null;
    }

    // 엔지니어가 터렛 위치에 도착했을 때 실제 버프 등록과 탑승 숨김 처리를 완료한다
    private bool CompleteEngineerBoarding()
    {
        if (assignedEngineerBuffReceiver == null)
        {
            return false;
        }

        if (!assignedEngineerBuffReceiver.TryRegisterEngineer(this))
        {
            Debug.LogWarning("[Survivor] 엔지니어 터렛 탑승 등록에 실패했습니다.", this);
            return false;
        }

        defenseMoveTarget = null;
        ChangeState(SurvivorState.EngineerAssigned);
        SetInteractionVisible(false);
        return true;
    }

    // 치료 중 시각 오브젝트와 상호작용 콜라이더 노출을 전환한다
    private void SetInteractionVisible(bool visible)
    {
        if (visibleRoot != null)
        {
            visibleRoot.SetActive(visible);
        }

        if (interactionCollider != null)
        {
            interactionCollider.enabled = visible;
        }

        if (agent != null)
        {
            agent.enabled = visible;
        }
    }

    // 현재 역할 인덱스에 맞는 Mesh와 Material을 렌더러에 적용한다
    private void ApplyRoleVisual()
    {
        if (roleVisualEntries == null || roleVisualEntries.Count == 0)
        {
            return;
        }

        int roleIndex = (int)role;
        if (roleIndex < 0 || roleIndex >= roleVisualEntries.Count)
        {
            return;
        }

        RoleVisualEntry entry = roleVisualEntries[roleIndex];
        if (entry == null)
        {
            return;
        }

        VisualSet visualSet = entry.GetVisualSet(visualCondition);
        if (visualSet == null || visualSet.Mesh == null)
        {
            ApplyDefaultRoleVisual();
            return;
        }

        ApplyRoleVisualSet(visualSet.Mesh, visualSet.Materials);
    }

    // 캐시한 기본 Mesh와 Material을 렌더러에 되돌린다
    private void ApplyDefaultRoleVisual()
    {
        ApplyRoleVisualSet(defaultRoleMesh, defaultRoleMaterials);
    }

    // 지정한 Mesh와 Material을 현재 역할 렌더러에 적용한다
    private void ApplyRoleVisualSet(Mesh mesh, Material[] materials)
    {
        if (roleSkinnedMeshRenderer == null || mesh == null)
        {
            return;
        }

        roleSkinnedMeshRenderer.sharedMesh = mesh;
        if (materials != null && materials.Length > 0)
        {
            roleSkinnedMeshRenderer.sharedMaterials = materials;
        }
    }

    // 역할 외형 변경 전 렌더러의 기본 Mesh와 Material을 저장한다
    private void CacheDefaultRoleVisual()
    {
        if (roleSkinnedMeshRenderer != null)
        {
            defaultRoleMesh = roleSkinnedMeshRenderer.sharedMesh;
            defaultRoleMaterials = roleSkinnedMeshRenderer.sharedMaterials;
            return;
        }
    }

    // visibleRoot에서 역할 외형을 적용할 SkinnedMeshRenderer를 찾는다
    private void AutoBindRoleVisualReferences()
    {
        if (visibleRoot != null)
        {
            roleSkinnedMeshRenderer = visibleRoot.GetComponent<SkinnedMeshRenderer>();
            return;
        }

        roleSkinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
    }

    // 상호작용 참조가 비어 있으면 기본 참조를 자동 연결한다
    private void AutoBindInteractionReferences()
    {
        if (visibleRoot == null)
        {
            visibleRoot = transform.childCount > 0 ? transform.GetChild(0).gameObject : null;
        }

        if (interactionCollider == null)
        {
            interactionCollider = GetComponent<Collider>();
        }
    }

    // 수리 대상 예약을 해제한다
    private void ClearRepairTarget()
    {
        if (repairTarget != null)
        {
            repairTarget.ClearRepairReservation(this);
            repairTarget = null;
        }

        repairTargetDefenseLineIndex = NO_DEFENSE_LINE;
    }

    // 웨이브 재시작 복귀 이동이 불가능할 때 방어선 상태를 즉시 초기화한다
    private void ResetDefenseLineStateImmediatelyForWaveRestart()
    {
        activeDefenseLineIndex = NO_DEFENSE_LINE;
        defenseMoveTarget = null;
        searchTimer = 0f;
        destinationRefreshTimer = 0f;
        moveTimer = 0f;

        if (agent != null && agent.enabled)
        {
            ChangeState(GetIdleStateForRole());
            return;
        }

        state = GetIdleStateForRole();
    }

    // 애니메이터에 지정한 파라미터가 있는지 확인한다
    private bool HasAnimatorParameter(int parameterHash)
    {
        AnimatorControllerParameter[] parameters = anim.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == parameterHash)
            {
                return true;
            }
        }

        return false;
    }
}
