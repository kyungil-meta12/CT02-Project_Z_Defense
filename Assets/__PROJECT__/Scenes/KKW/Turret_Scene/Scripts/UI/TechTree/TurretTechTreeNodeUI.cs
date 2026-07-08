using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 수동 배치된 터렛 트리 노드의 아이콘, 상태 색상, 클릭 입력을 제어한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretTechTreeNodeUI : MonoBehaviour
{
    [Header("터렛")]
    [SerializeField] private TurretDefinitionSO definition;

    [Header("표시 참조")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private Graphic pulseGraphic;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private Button button;

    [Header("연출")]
    [SerializeField, Min(0.0f)] private float readyPulseSpeed = 3.0f;
    [SerializeField, Range(0.0f, 1.0f)] private float readyPulseMinAlpha = 0.35f;
    [SerializeField, Range(0.0f, 1.0f)] private float readyPulseMaxAlpha = 0.95f;

    private TurretTechTreeUIController owner;
    private TurretTechTreeNodeState currentState;
    private Color pulseBaseColor = Color.white;
    private bool isPulseActive;
    private bool hasRequiredReferences;

    public TurretDefinitionSO Definition => definition;
    public TurretTechTreeNodeState CurrentState => currentState;

    // 컴포넌트 추가 시 기본 하위 참조를 자동 연결한다
    private void Reset()
    {
        iconImage = iconImage != null ? iconImage : GetComponent<Image>();
        button = button != null ? button : GetComponent<Button>();
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length > 0)
        {
            nameText = texts[0];
        }

        if (texts.Length > 1)
        {
            stateText = texts[1];
        }
    }

    // 시작 전에 버튼 이벤트를 연결한다
    private void Awake()
    {
        hasRequiredReferences = ValidateRequiredReferences();
        if (pulseGraphic != null)
        {
            pulseBaseColor = pulseGraphic.color;
        }

        BindButton();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindButton();
    }

    // Ready 상태일 때 테두리 펄스 투명도를 갱신한다
    private void Update()
    {
        if (!isPulseActive || pulseGraphic == null)
        {
            return;
        }

        float pulse = (Mathf.Sin(Time.unscaledTime * readyPulseSpeed) + 1.0f) * 0.5f;
        Color color = pulseBaseColor;
        color.a = Mathf.Lerp(readyPulseMinAlpha, readyPulseMaxAlpha, pulse);
        pulseGraphic.color = color;
    }

    // 상위 터렛 트리 컨트롤러 참조를 설정한다
    public void Initialize(TurretTechTreeUIController owner_)
    {
        owner = owner_;
    }

    // 현재 노드 상태와 표시 데이터를 UI에 적용한다
    public void ApplyState(TurretTechTreeNodeState state, TurretTechTreeViewProfileSO profile)
    {
        if (!hasRequiredReferences)
        {
            return;
        }

        currentState = state;
        TurretTechTreeNodeViewData nodeData = profile == null ? null : profile.FindNodeData(definition);
        Sprite icon = ResolveIcon(nodeData);
        Color nodeColor = profile == null ? Color.white : profile.GetNodeColor(state);

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.color = nodeColor;
            iconImage.preserveAspect = true;
        }

        if (frameImage != null)
        {
            frameImage.color = nodeColor;
        }

        if (nameText != null)
        {
            nameText.text = GetDisplayName(definition);
        }

        if (stateText != null)
        {
            stateText.text = profile == null ? string.Empty : profile.GetStateText(state);
        }

        SetPulseActive(state == TurretTechTreeNodeState.Ready, profile == null ? Color.white : profile.GetLineColor(state));
    }

    // 노드 클릭을 상위 컨트롤러에 전달한다
    private void OnNodeClicked()
    {
        if (owner != null)
        {
            owner.ShowNodeDetail(this);
        }
    }

    // 버튼 클릭 이벤트를 등록한다
    private void BindButton()
    {
        if (!hasRequiredReferences || button == null)
        {
            return;
        }

        button.onClick.RemoveListener(OnNodeClicked);
        button.onClick.AddListener(OnNodeClicked);
    }

    // 버튼 클릭 이벤트를 해제한다
    private void UnbindButton()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnNodeClicked);
        }
    }

    // Ready 상태 펄스 표시 여부를 적용한다
    private void SetPulseActive(bool isActive, Color pulseColor)
    {
        isPulseActive = isActive;
        if (pulseGraphic == null)
        {
            return;
        }

        pulseBaseColor = pulseColor;
        pulseGraphic.gameObject.SetActive(isActive);
        if (!isActive)
        {
            Color color = pulseBaseColor;
            color.a = 0.0f;
            pulseGraphic.color = color;
        }
    }

    // 노드 데이터와 터렛 정의에서 아이콘을 결정한다
    private Sprite ResolveIcon(TurretTechTreeNodeViewData nodeData)
    {
        if (nodeData != null && nodeData.OverrideIcon != null)
        {
            return nodeData.OverrideIcon;
        }

        return definition == null ? null : definition.uiIcon;
    }

    // 런타임에 필요한 인스펙터 참조가 모두 연결됐는지 확인한다
    private bool ValidateRequiredReferences()
    {
        bool isValid = true;
        isValid &= LogMissingReference(definition, nameof(definition));
        isValid &= LogMissingReference(iconImage, nameof(iconImage));
        isValid &= LogMissingReference(frameImage, nameof(frameImage));
        isValid &= LogMissingReference(pulseGraphic, nameof(pulseGraphic));
        isValid &= LogMissingReference(nameText, nameof(nameText));
        isValid &= LogMissingReference(stateText, nameof(stateText));
        isValid &= LogMissingReference(button, nameof(button));
        return isValid;
    }

    // 단일 인스펙터 참조 누락 여부를 로그로 알린다
    private bool LogMissingReference(Object reference, string fieldName)
    {
        if (reference != null)
        {
            return true;
        }

        Debug.LogWarning("[터렛 트리 노드 UI] " + fieldName + " 참조가 비어 있습니다. 인스펙터에서 직접 연결해야 합니다.", this);
        return false;
    }

    // 터렛 정의의 표시 이름을 반환한다
    private static string GetDisplayName(TurretDefinitionSO turretDefinition)
    {
        if (turretDefinition == null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(turretDefinition.displayName) ? turretDefinition.name : turretDefinition.displayName;
    }
}
