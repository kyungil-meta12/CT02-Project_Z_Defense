using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class Survivor : MonoBehaviour
{
    private enum SurvivorState
    {
        Idle,
        MoveToTarget,
        Repairing,
        Retreating,
        ReturningToDefensePoint,
        Vaulting
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

    [Header("방어선 이동")]
    [SerializeField] private float defensePointStoppingDistance = 0.4f;
    [SerializeField] private float defensePointMoveTimeout = 12f;

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
    private int moveSpeedHash;
    private int repairHash;
    private int vaultHash;
    private int activeDefenseLineIndex = NO_DEFENSE_LINE;
    private bool hasMoveSpeedParameter;
    private bool hasRepairParameter;
    private bool hasVaultParameter;
    private bool loggedMissingVaultParameter;

    public int ActiveDefenseLineIndex => activeDefenseLineIndex;

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
        ConfigureDefaultVaultLayerMask();
        ConfigureAgent(spec != null ? spec.repairRange : 0f);
    }

    // 활성화될 때 기본 대기 상태로 초기화한다
    private void OnEnable()
    {
        RegisterWithGameManager();
        ChangeState(SurvivorState.Idle);
        defenseMoveTarget = null;
        activeDefenseLineIndex = NO_DEFENSE_LINE;
        searchTimer = 0f;
        destinationRefreshTimer = 0f;
        moveTimer = 0f;
        vaultTimer = 0f;
        vaultDetectionTimer = 0f;
    }

    private void Start()
    {
        RegisterWithGameManager();
    }

    // 비활성화될 때 수리 예약과 게임 매니저 등록을 해제한다
    private void OnDisable()
    {
        ClearRepairTarget();
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
    public void StartDefenseLineRetreat(int defenseLineIndex, Transform retreatPoint)
    {
        if (retreatPoint == null)
        {
            Debug.LogWarning("[Survivor] 대피 포인트가 없어 방어선 대피 명령을 무시합니다.", this);
            return;
        }

        if (activeDefenseLineIndex > defenseLineIndex && state != SurvivorState.ReturningToDefensePoint)
        {
            return;
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

        if (activeDefenseLineIndex > defenseLineIndex)
        {
            return;
        }

        activeDefenseLineIndex = defenseLineIndex;
        defenseMoveTarget = restoredPoint;
        ClearRepairTarget();
        ChangeState(SurvivorState.ReturningToDefensePoint);
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
                UpdateDefensePointMove();
                break;
            case SurvivorState.Vaulting:
                UpdateVaulting();
                break;
        }
    }

    // 대기 중 일정 주기로 수리 대상을 찾는다
    private void UpdateIdle()
    {
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

        if (moveTimeout > 0f && moveTimer >= moveTimeout)
        {
            AbortRepairTarget("[Survivor] 수리 대상까지 이동 시간이 초과되어 예약을 해제합니다.");
            return;
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

    // 대상의 내구도를 초당 수리량 기준으로 회복한다
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

        float repairAmount = spec.GetRepairHpPerSecond() * Time.deltaTime;
        repairTarget.Repair(repairAmount);

        if (!repairTarget.IsDamaged)
        {
            repairTarget.hpUI.gameObject.SetActive(false);
            ClearRepairTarget();
            ChangeState(SurvivorState.Idle);
        }
    }

    // 방어선 대피 또는 복귀 포인트까지 이동한다
    private void UpdateDefensePointMove()
    {
        if (defenseMoveTarget == null)
        {
            AbortDefensePointMove("[Survivor] 방어선 이동 포인트가 사라져 이동을 중단합니다.");
            return;
        }

        agent.isStopped = false;
        moveTimer += Time.deltaTime;
        destinationRefreshTimer -= Time.deltaTime;
        vaultDetectionTimer -= Time.deltaTime;

        if (!agent.isOnNavMesh)
        {
            AbortDefensePointMove("[Survivor] NavMesh 위에 없어 방어선 이동을 중단합니다.");
            return;
        }

        agent.nextPosition = transform.position;

        if (!RefreshDefenseDestinationIfNeeded())
        {
            AbortDefensePointMove("[Survivor] 방어선 포인트까지 경로를 만들 수 없어 이동을 중단합니다.");
            return;
        }

        if (defensePointMoveTimeout > 0f && moveTimer >= defensePointMoveTimeout)
        {
            AbortDefensePointMove("[Survivor] 방어선 포인트까지 이동 시간이 초과되었습니다.");
            return;
        }

        if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            AbortDefensePointMove("[Survivor] 방어선 포인트까지 유효한 경로가 없습니다.");
            return;
        }

        if (TryStartVault())
        {
            return;
        }

        RotateByAgentPath();

        if (HasReachedDefensePoint())
        {
            bool wasReturning = state == SurvivorState.ReturningToDefensePoint;
            defenseMoveTarget = null;

            if (wasReturning)
            {
                activeDefenseLineIndex = NO_DEFENSE_LINE;
            }

            ChangeState(SurvivorState.Idle);
        }
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

        if ((state == SurvivorState.Retreating || state == SurvivorState.ReturningToDefensePoint) && defenseMoveTarget == null)
        {
            return;
        }

        transform.position = anim.rootPosition;
        agent.nextPosition = transform.position;
    }

    // GameManager에서 수리 대상을 가져온다
    private void TryFindRepairTarget()
    {
        if (GameManager.Inst == null)
        {
            return;
        }

        if (GameManager.Inst.TryGetRepairTarget(transform.position, this, out Obstacle obstacle))
        {
            repairTarget = obstacle;
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
    private void AbortDefensePointMove(string message)
    {
        defenseMoveTarget = null;
        ChangeState(SurvivorState.Idle);
        searchTimer = GetTargetSearchInterval();
        Debug.LogWarning(message, this);
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
        float stoppingDistance = Mathf.Max(0.05f, defensePointStoppingDistance);
        return offset.sqrMagnitude <= stoppingDistance * stoppingDistance;
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

    // 대피 또는 복귀 중 전방 장애물을 감지해 넘기 상태로 전환한다
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
            direction = transform.forward;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        direction.Normalize();

        // 목적지 방향 계산 (대피 중인지 복귀 중인지 확인용)
        Vector3 toTarget = Vector3.zero;
        if (defenseMoveTarget != null)
        {
            toTarget = defenseMoveTarget.position - transform.position;
            toTarget.y = 0f;
        }

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
            || state == SurvivorState.ReturningToDefensePoint;
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
            moveTimer = 0f;
        }

        if (state == SurvivorState.Retreating || state == SurvivorState.ReturningToDefensePoint)
        {
            ConfigureAgent(defensePointStoppingDistance);
            destinationRefreshTimer = 0f;
            vaultDetectionTimer = 0f;
            moveTimer = 0f;
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

    // 수리 대상 예약을 해제한다
    private void ClearRepairTarget()
    {
        if (repairTarget != null)
        {
            repairTarget.ClearRepairReservation(this);
            repairTarget = null;
        }
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
