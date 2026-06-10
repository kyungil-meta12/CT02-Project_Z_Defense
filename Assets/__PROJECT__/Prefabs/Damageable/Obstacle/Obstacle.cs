using System.Collections.Generic;
using DinoFracture;
using UnityEngine;

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

        //레벨에 맞춰서 hp 설정
        TotalHp = spec.Hp + spec.level * spec.levelWeight;
        CurrHp = TotalHp;
        IsAlive = true;
        ReservedRepairer = null;
        hasNotifiedFracture = false;
        
        hpUI.gameObject.SetActive(true);
        hpUI.InputTotalHp(TotalHp);
        hpUI.InputCurrHp(TotalHp);
        hpUI.gameObject.SetActive(false);
    }

    //todo 레벨업시 호출, 레벨 가중치를 hp최대치에 추가
    public void LevelUp()
    {
        if (!ValidateRequiredReferences())
        {
            return;
        }

        TotalHp = spec.Hp + spec.level * spec.levelWeight;
        hpUI.InputTotalHp(TotalHp);
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
    public void TakeDamage(float damage)
    {
        if(!IsAlive)
        {
            return;
        }
        CurrHp -= damage;
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

    public void Destroy()
    {
        Destroy(gameObject);
    }
    
    public void SetPosition(Transform t)
    {
        transform.position = t.position;
    }
}
