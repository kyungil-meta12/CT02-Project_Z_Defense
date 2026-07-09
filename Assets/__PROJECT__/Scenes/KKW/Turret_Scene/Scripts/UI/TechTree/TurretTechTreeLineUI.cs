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

    private Color baseColor = Color.white;
    private float pulseDuration = 2.0f;
    private float pulseMinAlpha = 0.3f;
    private float pulseMaxAlpha = 0.7f;
    private bool isPulseActive;
    private bool hasRequiredReferences;

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
        hasRequiredReferences = ValidateRequiredReferences();
        if (lineGraphic != null)
        {
            baseColor = lineGraphic.color;
        }
    }

    // 미완료 연결선의 펄스 투명도를 갱신한다
    private void Update()
    {
        if (!isPulseActive || lineGraphic == null)
        {
            return;
        }

        float pulse = (Mathf.Sin(Time.unscaledTime / pulseDuration * Mathf.PI * 2.0f) + 1.0f) * 0.5f;
        Color color = baseColor;
        color.a = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, pulse);
        lineGraphic.color = color;
    }

    // 연결선 상태 색상과 펄스 여부를 적용한다
    public void ApplyState(TurretTechTreeNodeState state, TurretTechTreeViewProfileSO profile)
    {
        if (!hasRequiredReferences)
        {
            return;
        }

        Color color = profile == null ? Color.white : profile.GetLineColor(state);
        baseColor = color;
        ApplyPulseSettings(profile);
        isPulseActive = profile != null && profile.UseIncompleteLinePulse && state != TurretTechTreeNodeState.Unlocked;

        if (lineGraphic != null)
        {
            lineGraphic.color = color;
        }
    }

    // 프로필의 미완료 라인 펄스 설정을 적용한다
    private void ApplyPulseSettings(TurretTechTreeViewProfileSO profile)
    {
        if (profile == null)
        {
            pulseDuration = 2.0f;
            pulseMinAlpha = 0.3f;
            pulseMaxAlpha = 0.7f;
            return;
        }

        pulseDuration = Mathf.Max(0.1f, profile.IncompleteLinePulseDuration);
        pulseMinAlpha = profile.IncompleteLinePulseMinAlpha;
        pulseMaxAlpha = Mathf.Max(pulseMinAlpha, profile.IncompleteLinePulseMaxAlpha);
    }

    // 런타임에 필요한 인스펙터 참조가 모두 연결됐는지 확인한다
    private bool ValidateRequiredReferences()
    {
        bool isValid = true;
        isValid &= LogMissingReference(parentDefinition, nameof(parentDefinition));
        isValid &= LogMissingReference(childDefinition, nameof(childDefinition));
        isValid &= LogMissingReference(lineGraphic, nameof(lineGraphic));
        return isValid;
    }

    // 단일 인스펙터 참조 누락 여부를 로그로 알린다
    private bool LogMissingReference(Object reference, string fieldName)
    {
        if (reference != null)
        {
            return true;
        }

        Debug.LogWarning("[터렛 트리 라인 UI] " + fieldName + " 참조가 비어 있습니다. 인스펙터에서 직접 연결해야 합니다.", this);
        return false;
    }
}
