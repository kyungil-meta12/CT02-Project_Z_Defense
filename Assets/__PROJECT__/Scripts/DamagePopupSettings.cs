using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "DamagePopupSettings", menuName = "__PROJECT__/UI/Damage Popup Settings")]
public class DamagePopupSettings : ScriptableObject
{
    [Header("Pool")]
    [Tooltip("MemoryPool에서 사용할 데미지 팝업 프리팹")]
    [SerializeField] private DamagePopup damagePopupPrefab;
    [Tooltip("초기 생성해 둘 데미지 팝업 개수")]
    [SerializeField, Min(1)] private int initialPoolSize = 32;

    [Header("Text")]
    [Tooltip("데미지 숫자 폰트 크기")]
    [SerializeField, Min(1)] private int fontSize = 24;
    [Tooltip("데미지 숫자에 사용할 TMP 폰트 에셋. 비워두면 TMP 기본 폰트를 사용한다.")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [Tooltip("데미지 숫자 색상")]
    [SerializeField] private Color damageColor = new Color(1f, 0.35f, 0.12f, 1f);

    [Header("Motion")]
    [Tooltip("데미지 팝업 유지 시간")]
    [SerializeField, Min(0.01f)] private float lifetime = 0.75f;
    [Tooltip("대상 위치 기준 위쪽 생성 높이")]
    [SerializeField] private float heightOffset = 2.2f;
    [Tooltip("유지 시간 동안 이동할 거리")]
    [SerializeField] private Vector3 moveOffset = new Vector3(0f, 1.2f, 0f);
    [Tooltip("생성 직후 크기")]
    [SerializeField, Min(0f)] private float startScale = 1.15f;
    [Tooltip("사라질 때 크기")]
    [SerializeField, Min(0f)] private float endScale = 0.85f;

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
}
