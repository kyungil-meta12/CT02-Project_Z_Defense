using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 수동 배치된 터렛 트리 연결선의 부모/자식 관계와 상태 색상 표시를 담당한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretTechTreeLineUI : MonoBehaviour
{
    [Header("연결 터렛")]
    [SerializeField] private TurretDefinitionSO parentDefinition;
    [SerializeField] private TurretDefinitionSO childDefinition;

    [Header("표시 참조")]
    [SerializeField] private Graphic lineGraphic;

    [Header("연출")]
    [SerializeField, Min(0.0f)] private float readyPulseSpeed = 2.6f;
    [SerializeField, Range(0.0f, 1.0f)] private float readyPulseMinAlpha = 0.45f;
    [SerializeField, Range(0.0f, 1.0f)] private float readyPulseMaxAlpha = 1.0f;

    private Color baseColor = Color.white;
    private bool isPulseActive;

    public TurretDefinitionSO ParentDefinition => parentDefinition;
    public TurretDefinitionSO ChildDefinition => childDefinition;

    // 컴포넌트 추가 시 현재 그래픽 참조를 자동 연결한다
    private void Reset()
    {
        lineGraphic = lineGraphic != null ? lineGraphic : GetComponent<Graphic>();
    }

    // 시작 전에 그래픽 참조와 기본 색상을 준비한다
    private void Awake()
    {
        if (lineGraphic == null)
        {
            lineGraphic = GetComponent<Graphic>();
        }

        if (lineGraphic != null)
        {
            baseColor = lineGraphic.color;
        }
    }

    // Ready 상태일 때 라인 펄스 투명도를 갱신한다
    private void Update()
    {
        if (!isPulseActive || lineGraphic == null)
        {
            return;
        }

        float pulse = (Mathf.Sin(Time.unscaledTime * readyPulseSpeed) + 1.0f) * 0.5f;
        Color color = baseColor;
        color.a = Mathf.Lerp(readyPulseMinAlpha, readyPulseMaxAlpha, pulse);
        lineGraphic.color = color;
    }

    // 연결선 상태 색상과 펄스 여부를 적용한다
    public void ApplyState(TurretTechTreeNodeState state, TurretTechTreeViewProfileSO profile)
    {
        Color color = profile == null ? Color.white : profile.GetLineColor(state);
        baseColor = color;
        isPulseActive = state == TurretTechTreeNodeState.Ready;

        if (lineGraphic != null)
        {
            lineGraphic.color = color;
        }
    }
}
