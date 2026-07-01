using System.Collections.Generic;
using DinoFracture;
using UnityEngine;

/// <summary>
/// 방어선 장애물의 체력, 피격, 파편화, 수리 예약, 생존자 넘기 위치 계산을 관리한다.
/// </summary>
public class Obstacle : MonoBehaviour, IDamageable
{
    private const float MIN_VAULT_MIRROR_DISTANCE = 0.1f;
    private const float VAULT_CENTER_EPSILON = 0.001f;

    public ObstacleSpec spec;
    public HpUI hpUI;

    [SerializeField] private GameObject preFracturedPiecesPrefab;

    public float TotalHp { get; private set; }
    public float CurrHp { get; private set; }
    public bool IsAlive { get; private set; }
    public bool IsDamaged => IsAlive && CurrHp < TotalHp;
    public bool HasFractured => hasNotifiedFracture;
    public Survivor ReservedRepairer { get; private set; }

    private PreFracturedGeometry fractureGeometry;
    private bool hasNotifiedFracture;
    private int runtimeLevel = 1;
    private bool hasInitializedHealth;

    // 장애물 파편화 컴포넌트를 캐싱한다
    private void Awake()
    {
        fractureGeometry = GetComponent<PreFracturedGeometry>();
    }

    // 활성화될 때 장애물 목록에 등록한다
    private void OnEnable()
    {
        if (GameManager.Inst != null)
        {
            GameManager.Inst.RegisterObstacle(this);
        }
    }

    // 비활성화될 때 장애물 목록에서 해제한다
    private void OnDisable()
    {
        if (GameManager.Inst != null)
        {
            GameManager.Inst.UnregisterObstacle(this);
        }
    }

    // 시작 시 기본 스펙 기반 체력을 초기화한다
    private void Start()
    {
        if (!ValidateRequiredReferences())
        {
            enabled = false;
            return;
        }

        if (GameManager.Inst != null)
        {
            GameManager.Inst.RegisterObstacle(this);
        }

        EnsurePreFracturedPieces(); //파편 미리 생성하여 최적화

        if (!hasInitializedHealth)
        {
            ApplyRuntimeLevel(spec, Mathf.Max(1, spec.level), false);
        }
    }

    // 지정 스펙과 레벨을 기준으로 체력 상태를 갱신한다
    public void ApplyRuntimeLevel(ObstacleSpec spec_, int level, bool preserveHpRatio)
    {
        float hpRatio = TotalHp > 0.0f ? Mathf.Clamp01(CurrHp / TotalHp) : 1.0f;
        ApplyRuntimeLevel(spec_, level, preserveHpRatio && hasInitializedHealth ? hpRatio : 1.0f);
    }

    // 지정 스펙과 레벨, 체력 비율을 기준으로 체력 상태를 갱신한다
    public void ApplyRuntimeLevel(ObstacleSpec spec_, int level, float hpRatio)
    {
        if (spec_ == null)
        {
            return;
        }

        spec = spec_;
        runtimeLevel = Mathf.Max(1, level);
        TotalHp = CalculateTotalHp(spec, runtimeLevel);
        CurrHp = TotalHp * Mathf.Clamp01(hpRatio);
        CurrHp = Mathf.Clamp(CurrHp, 0.0f, TotalHp);
        IsAlive = CurrHp > 0.0f;
        ReservedRepairer = null;
        hasNotifiedFracture = false;
        hasInitializedHealth = true;

        if (hpUI != null)
        {
            hpUI.gameObject.SetActive(true);
            hpUI.InputTotalHp(TotalHp);
            hpUI.InputCurrHp(CurrHp);
            hpUI.gameObject.SetActive(false);
        }
    }
    
    //파편화 
    public void Fracture()
    {
        if (fractureGeometry == null || fractureGeometry.IsProcessingFracture || !EnsurePreFracturedPieces())
        {
            return;
        }

        CameraController.Inst.AddShake(0.8f);
        fractureGeometry.Fracture();
        NotifyFractureEvent();
    }

    // 방어선 붕괴 처리를 위해 파편화 발생을 게임 매니저에 알린다
    private void NotifyFractureEvent()
    {
        if (hasNotifiedFracture)
        {
            //Debug.Log($"[Obstacle] {name} 이미 파괴 알림 전송됨");
            return;
        }

        hasNotifiedFracture = true;

        if (GameManager.Inst != null)
        {
            //Debug.Log($"[Obstacle] {name} 파괴됨! GameManager에 알림 전송");
            GameManager.Inst.NotifyObstacleFractured(this);
        }
        else
        {
            Debug.LogError($"[Obstacle] {name} 파괴되었지만 GameManager가 없습니다!");
        }
    }

    //사전 생성된 파편 오브젝트를 준비하고 PreFracturedGeometry에 연결한다
    private bool EnsurePreFracturedPieces()
    {
        if (fractureGeometry.GeneratedPieces != null)
        {
            return true;
        }

        if (preFracturedPiecesPrefab == null)
        {
            Debug.LogError("[Obstacle] 사전 생성된 파편 프리팹이 할당되지 않았습니다.", this);
            return false;
        }

        GameObject generatedPieces = Instantiate(preFracturedPiecesPrefab, transform.parent);
        generatedPieces.name = $"{name}FracturePieces";
        generatedPieces.SetActive(false);
        fractureGeometry.GeneratedPieces = generatedPieces;

        return true;
    }

    //피격
    public void TakeDamage(DamageInfo damageInfo)
    {
        if(!IsAlive)
        {
            return;
        }
        CurrHp -= Mathf.Max(0f, damageInfo.Damage);
        CurrHp = Mathf.Clamp(CurrHp, 0f, TotalHp);
        if (hpUI != null)
        {
            hpUI.gameObject.SetActive(true);
            hpUI.InputCurrHp(CurrHp);
        }

        if (CurrHp <= 0f)
        {
            if (hpUI != null)
            {
                hpUI.gameObject.SetActive(false); // hp UI 비활성화
            }

            IsAlive = false; // 생존 상태 비활성화
            ReservedRepairer = null;

            Fracture(); //DinoFracture 파편 효과
        }
    }

    // 수리 가능 여부를 확인한다
    public bool CanBeRepairedBy(Survivor survivor)
    {
        if (survivor == null || !IsDamaged)
        {
            return false;
        }

        return ReservedRepairer == null || ReservedRepairer == survivor;
    }

    // 수리 작업자를 예약한다
    public bool TryReserveRepair(Survivor survivor)
    {
        if (!CanBeRepairedBy(survivor))
        {
            return false;
        }

        ReservedRepairer = survivor;
        return true;
    }

    // 수리 작업자 예약을 해제한다
    public void ClearRepairReservation(Survivor survivor)
    {
        if (ReservedRepairer == survivor)
        {
            ReservedRepairer = null;
        }
    }

    // 내구도를 회복한다
    public void Repair(float amount)
    {
        if (!IsAlive || amount <= 0f)
        {
            return;
        }

        CurrHp = Mathf.Clamp(CurrHp + amount, 0f, TotalHp);
        if (hpUI != null)
        {
            hpUI.InputCurrHp(CurrHp);
        }
    }

    // 장애물 넘기 시 생존자가 착지할 반대편 위치를 계산한다
    public Vector3 GetVaultLandingPosition(Vector3 survivorPosition, Vector3 moveDirection, float fallbackForwardOffset, float fallbackVerticalOffset)
    {
        float obstacleX = transform.position.x;
        float mirroredXOffset = obstacleX - survivorPosition.x;

        if (Mathf.Abs(mirroredXOffset) <= VAULT_CENTER_EPSILON)
        {
            float directionX = Mathf.Abs(moveDirection.x) > VAULT_CENTER_EPSILON ? moveDirection.x : transform.right.x;
            mirroredXOffset = Mathf.Sign(directionX == 0f ? 1f : directionX) * Mathf.Max(MIN_VAULT_MIRROR_DISTANCE, fallbackForwardOffset);
        }

        Vector3 landingPosition = survivorPosition;
        landingPosition.x = obstacleX + mirroredXOffset;
        landingPosition.y += fallbackVerticalOffset;

        return landingPosition;
    }

    // 장애물 실행에 필요한 참조가 준비됐는지 확인한다
    private bool ValidateRequiredReferences()
    {
        if (spec == null)
        {
            //Debug.LogError("[Obstacle] 장애물 스펙이 할당되지 않았습니다.", this);
            return false;
        }

        if (hpUI == null)
        {
            //Debug.LogError("[Obstacle] HP UI가 할당되지 않았습니다.", this);
            return false;
        }

        return true;
    }

    // 장애물 게임 오브젝트를 런타임에서 제거한다
    public void Destroy()
    {
        Destroy(gameObject);
    }
    
    // 지정 트랜스폼 위치로 장애물을 이동한다
    public void SetPosition(Transform t)
    {
        transform.position = t.position;
    }

    // 지정 스펙과 레벨로 최대 체력을 계산한다
    private static float CalculateTotalHp(ObstacleSpec spec_, int level)
    {
        if (spec_ == null)
        {
            return 0.0f;
        }

        return spec_.Hp * (1.0f + Mathf.Max(1, level) * spec_.levelHpPercentPerLevel * 0.01f);
    }
}
