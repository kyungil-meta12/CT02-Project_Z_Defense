using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "DamagePopupSettings", menuName = "__PROJECT__/UI/Damage Popup Settings")]
public class DamagePopupSettings : ScriptableObject
{
    public const int DEFAULT_INITIAL_POOL_SIZE = 32;
    public const int DEFAULT_FONT_SIZE = 24;
    public const float DEFAULT_LIFETIME = 0.75f;
    public const float DEFAULT_HEIGHT_OFFSET = 2.2f;
    public const float DEFAULT_START_SCALE = 1.15f;
    public const float DEFAULT_END_SCALE = 0.85f;

    [Header("Pool")]
    [Tooltip("MemoryPool에서 사용할 데미지 팝업 프리팹")]
    [SerializeField] private DamagePopup damagePopupPrefab;
    [Tooltip("초기 생성해 둘 데미지 팝업 개수")]
    [SerializeField, Min(1)] private int initialPoolSize = DEFAULT_INITIAL_POOL_SIZE;

    [Header("Text")]
    [Tooltip("데미지 숫자 폰트 크기")]
    [SerializeField, Min(1)] private int fontSize = DEFAULT_FONT_SIZE;
    [Tooltip("데미지 숫자에 사용할 TMP 폰트 에셋. 비워두면 TMP 기본 폰트를 사용한다.")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [Tooltip("데미지 숫자 색상")]
    [SerializeField] private Color damageColor = new Color(1f, 0.35f, 0.12f, 1f);

    [Header("Motion")]
    [Tooltip("데미지 팝업 유지 시간")]
    [SerializeField, Min(0.01f)] private float lifetime = DEFAULT_LIFETIME;
    [Tooltip("대상 위치 기준 위쪽 생성 높이")]
    [SerializeField] private float heightOffset = DEFAULT_HEIGHT_OFFSET;
    [Tooltip("유지 시간 동안 이동할 거리")]
    [SerializeField] private Vector3 moveOffset = new Vector3(0f, 1.2f, 0f);
    [Tooltip("생성 직후 크기")]
    [SerializeField, Min(0f)] private float startScale = DEFAULT_START_SCALE;
    [Tooltip("사라질 때 크기")]
    [SerializeField, Min(0f)] private float endScale = DEFAULT_END_SCALE;

    public DamagePopup DamagePopupPrefab => damagePopupPrefab;
    public int InitialPoolSize => initialPoolSize;
    public int FontSize => fontSize;
    public TMP_FontAsset FontAsset => fontAsset;
    public float Lifetime => lifetime;
    public float HeightOffset => heightOffset;
    public Vector3 MoveOffset => moveOffset;
    public Color DamageColor => damageColor;
    public float StartScale => startScale;
    public float EndScale => endScale;

    public static DamagePopupSettings CreateRuntimeDefault()
    {
        return CreateInstance<DamagePopupSettings>();
    }
}
