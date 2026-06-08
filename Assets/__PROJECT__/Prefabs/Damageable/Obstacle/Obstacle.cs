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

    private PreFracturedGeometry fractureGeometry;

    private void Awake()
    {
        fractureGeometry = GetComponent<PreFracturedGeometry>();
    }

    private void Start()
    {
        //레벨에 맞춰서 hp 설정
        TotalHp = spec.Hp + spec.level * spec.levelWeight;
        CurrHp = TotalHp;
        IsAlive = true;
        
        hpUI.InputTotalHp(TotalHp);
        hpUI.InputCurrHp(TotalHp);
    }

    //todo 레벨업시 호출, 레벨 가중치를 hp최대치에 추가
    public void LevelUp()
    {
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
        hpUI.InputCurrHp(CurrHp);

        if (CurrHp <= 0f)
        {
            hpUI.gameObject.SetActive(false); // hp UI 비활성화
            IsAlive = false; // 생존 상태 비활성화
            
            Fracture(); //DinoFracture 파편 효과
        }
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
