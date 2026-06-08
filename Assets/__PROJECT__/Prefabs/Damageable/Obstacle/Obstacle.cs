using DinoFracture;
using UnityEngine;

public class Obstacle : MonoBehaviour, IDamageable
{
    public ObstacleSpec spec;
    public HpUI hpUI;

    [SerializeField] private GameObject preFracturedPiecesPrefab;

    public float TotalHp { get; private set; }
    public float CurrHp { get; private set; }
    public bool IsAlive { get; private set; }
    public bool IsDamaged => IsAlive && CurrHp < TotalHp;
    public Survivor ReservedRepairer { get; private set; }

    private PreFracturedGeometry fractureGeometry;

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

        //레벨에 맞춰서 hp 설정
        TotalHp = spec.Hp + spec.level * spec.levelWeight;
        CurrHp = TotalHp;
        IsAlive = true;
        ReservedRepairer = null;
        
        hpUI.InputTotalHp(TotalHp);
        hpUI.InputCurrHp(TotalHp);
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

        fractureGeometry.Fracture();
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

    // 장애물 실행에 필요한 참조가 준비됐는지 확인한다
    private bool ValidateRequiredReferences()
    {
        if (spec == null)
        {
            Debug.LogError("[Obstacle] 장애물 스펙이 할당되지 않았습니다.", this);
            return false;
        }

        if (hpUI == null)
        {
            Debug.LogError("[Obstacle] HP UI가 할당되지 않았습니다.", this);
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
