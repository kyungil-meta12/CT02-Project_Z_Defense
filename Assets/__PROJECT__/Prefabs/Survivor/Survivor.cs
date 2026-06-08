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
        Repairing
    }

    [Header("생존자 스펙")]
    public SurvivorSpec spec;

    [Header("애니메이션")]
    [SerializeField] private string moveSpeedParameter = "MoveSpeed";
    [SerializeField] private string repairParameter = "IsRepairing";
    [SerializeField] private float baseRootMotionMoveSpeed = 3.5f;

    [Header("탐색 최적화")]
    [SerializeField] private float minTargetSearchInterval = 0.25f;
    [SerializeField] private float destinationRefreshInterval = 0.5f;
    [SerializeField] private float moveTimeout = 8f;

    private Animator anim;
    private NavMeshAgent agent;
    private Obstacle repairTarget;
    private SurvivorState state;
    private float searchTimer;
    private float destinationRefreshTimer;
    private float moveTimer;
    private int moveSpeedHash;
    private int repairHash;
    private bool hasMoveSpeedParameter;
    private bool hasRepairParameter;

    // 필요한 컴포넌트와 애니메이터 파라미터를 초기화한다
    private void Awake()
    {
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        moveSpeedHash = Animator.StringToHash(moveSpeedParameter);
        repairHash = Animator.StringToHash(repairParameter);
        hasMoveSpeedParameter = HasAnimatorParameter(moveSpeedHash);
        hasRepairParameter = HasAnimatorParameter(repairHash);
        ConfigureAgent();
    }

    // 활성화될 때 기본 대기 상태로 초기화한다
    private void OnEnable()
    {
        ChangeState(SurvivorState.Idle);
        searchTimer = 0f;
        destinationRefreshTimer = 0f;
        moveTimer = 0f;
    }

    // 비활성화될 때 수리 예약을 해제한다
    private void OnDisable()
    {
        ClearRepairTarget();
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

    // NavMeshAgent 이동 수치를 스펙에 맞춘다
    private void ConfigureAgent()
    {
        if (spec == null)
        {
            return;
        }

        agent.speed = spec.GetMoveSpeed();
        agent.stoppingDistance = spec.repairRange;
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

        if (!RefreshDestinationIfNeeded())
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
            ClearRepairTarget();
            ChangeState(SurvivorState.Idle);
        }
    }

    // 이동 애니메이션 루트모션으로 실제 위치를 갱신한다
    private void OnAnimatorMove()
    {
        if (state != SurvivorState.MoveToTarget || repairTarget == null || agent == null || !agent.isOnNavMesh)
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

    // 목적지 갱신을 일정 주기로 제한해 NavMesh 비용을 줄인다
    private bool RefreshDestinationIfNeeded()
    {
        if (destinationRefreshTimer > 0f)
        {
            return true;
        }

        destinationRefreshTimer = Mathf.Max(0.1f, destinationRefreshInterval);
        return repairTarget != null && agent.SetDestination(repairTarget.transform.position);
    }

    // 이동 실패 상황에서 예약을 해제하고 재탐색을 지연한다
    private void AbortRepairTarget(string message)
    {
        ClearRepairTarget();
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

    // 상태를 변경하고 진입 처리를 적용한다
    private void ChangeState(SurvivorState nextState)
    {
        state = nextState;

        if (agent == null)
        {
            return;
        }

        agent.isStopped = state != SurvivorState.MoveToTarget;

        if (state == SurvivorState.Idle)
        {
            searchTimer = 0f;
        }

        if (state == SurvivorState.MoveToTarget)
        {
            ConfigureAgent();
            destinationRefreshTimer = 0f;
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

        bool isMoving = state == SurvivorState.MoveToTarget && agent.desiredVelocity.sqrMagnitude > 0.01f;
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
