using System;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 터렛 트리 UI의 노드 표시 데이터, 프리뷰 영상, 상태별 색상을 관리한다.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/UI/Turret Tech Tree View Profile")]
public class TurretTechTreeViewProfileSO : ScriptableObject
{
    [Header("기본 해금 루트")]
    [SerializeField] private TurretDefinitionSO[] defaultUnlockedDefinitions = Array.Empty<TurretDefinitionSO>();

    [Header("노드 표시 데이터")]
    [SerializeField] private TurretTechTreeNodeViewData[] nodes = Array.Empty<TurretTechTreeNodeViewData>();

    [Header("노드 색상")]
    [SerializeField] private Color lockedNodeColor = new Color(0.35f, 0.37f, 0.42f, 0.38f);
    [SerializeField] private Color blockedByLevelNodeColor = new Color(0.48f, 0.62f, 0.78f, 0.68f);
    [SerializeField] private Color blockedByCostNodeColor = new Color(1.0f, 0.66f, 0.22f, 0.78f);
    [SerializeField] private Color readyNodeColor = Color.white;
    [SerializeField] private Color unlockedNodeColor = Color.white;

    [Header("라인 색상")]
    [SerializeField] private Color lockedLineColor = new Color(0.24f, 0.26f, 0.30f, 0.35f);
    [SerializeField] private Color blockedByLevelLineColor = new Color(0.42f, 0.58f, 0.76f, 0.62f);
    [SerializeField] private Color blockedByCostLineColor = new Color(1.0f, 0.66f, 0.22f, 0.66f);
    [SerializeField] private Color readyLineColor = new Color(0.2f, 0.95f, 1.0f, 0.95f);
    [SerializeField] private Color unlockedLineColor = new Color(0.86f, 0.68f, 0.32f, 1.0f);

    [Header("상태 문구")]
    [SerializeField] private string lockedText = "이전 터렛 필요";
    [SerializeField] private string blockedByLevelText = "레벨 부족";
    [SerializeField] private string blockedByCostText = "재료 부족";
    [SerializeField] private string readyText = "진화 가능";
    [SerializeField] private string unlockedText = "해금 완료";

    [Header("프리뷰 영상 페이드")]
    [SerializeField] private bool usePreviewVideoFade = true;
    [SerializeField, Min(0.0f)] private float previewVideoFadeInDuration = 0.5f;
    [SerializeField, Min(0.0f)] private float previewVideoFadeOutDuration = 0.5f;

    [Header("미완료 라인 펄스")]
    [Tooltip("해금 완료가 아닌 연결선의 알파 펄스를 사용할지 결정한다.")]
    [SerializeField] private bool useIncompleteLinePulse = true;
    [Tooltip("미완료 연결선 알파가 한 번 왕복하는 시간(초)이다.")]
    [SerializeField, Min(0.1f)] private float incompleteLinePulseDuration = 2.0f;
    [Tooltip("미완료 연결선 펄스의 최소 알파값이다.")]
    [SerializeField, Range(0.0f, 1.0f)] private float incompleteLinePulseMinAlpha = 0.3f;
    [Tooltip("미완료 연결선 펄스의 최대 알파값이다.")]
    [SerializeField, Range(0.0f, 1.0f)] private float incompleteLinePulseMaxAlpha = 0.7f;

    public TurretDefinitionSO[] DefaultUnlockedDefinitions => defaultUnlockedDefinitions;
    public TurretTechTreeNodeViewData[] Nodes => nodes;
    public bool UsePreviewVideoFade => usePreviewVideoFade;
    public float PreviewVideoFadeInDuration => previewVideoFadeInDuration;
    public float PreviewVideoFadeOutDuration => previewVideoFadeOutDuration;
    public bool UseIncompleteLinePulse => useIncompleteLinePulse;
    public float IncompleteLinePulseDuration => incompleteLinePulseDuration;
    public float IncompleteLinePulseMinAlpha => incompleteLinePulseMinAlpha;
    public float IncompleteLinePulseMaxAlpha => incompleteLinePulseMaxAlpha;

    // 지정 터렛 정의에 맞는 노드 표시 데이터를 찾는다
    public TurretTechTreeNodeViewData FindNodeData(TurretDefinitionSO definition)
    {
        if (definition == null || nodes == null)
        {
            return null;
        }

        for (int i = 0; i < nodes.Length; i++)
        {
            TurretTechTreeNodeViewData node = nodes[i];
            if (node != null && node.Definition == definition)
            {
                return node;
            }
        }

        return null;
    }

    // 상태에 맞는 노드 색상을 반환한다
    public Color GetNodeColor(TurretTechTreeNodeState state)
    {
        switch (state)
        {
            case TurretTechTreeNodeState.Unlocked:
                return unlockedNodeColor;
            case TurretTechTreeNodeState.Ready:
                return readyNodeColor;
            case TurretTechTreeNodeState.BlockedByCost:
                return blockedByCostNodeColor;
            case TurretTechTreeNodeState.BlockedByLevel:
                return blockedByLevelNodeColor;
            default:
                return lockedNodeColor;
        }
    }

    // 상태에 맞는 연결선 색상을 반환한다
    public Color GetLineColor(TurretTechTreeNodeState state)
    {
        switch (state)
        {
            case TurretTechTreeNodeState.Unlocked:
                return unlockedLineColor;
            case TurretTechTreeNodeState.Ready:
                return readyLineColor;
            case TurretTechTreeNodeState.BlockedByCost:
                return blockedByCostLineColor;
            case TurretTechTreeNodeState.BlockedByLevel:
                return blockedByLevelLineColor;
            default:
                return lockedLineColor;
        }
    }

    // 상태에 맞는 안내 문구를 반환한다
    public string GetStateText(TurretTechTreeNodeState state)
    {
        switch (state)
        {
            case TurretTechTreeNodeState.Unlocked:
                return unlockedText;
            case TurretTechTreeNodeState.Ready:
                return readyText;
            case TurretTechTreeNodeState.BlockedByCost:
                return blockedByCostText;
            case TurretTechTreeNodeState.BlockedByLevel:
                return blockedByLevelText;
            default:
                return lockedText;
        }
    }

    // 인스펙터 배열을 안전한 기본값으로 보정한다
    private void OnValidate()
    {
        defaultUnlockedDefinitions = defaultUnlockedDefinitions ?? Array.Empty<TurretDefinitionSO>();
        nodes = nodes ?? Array.Empty<TurretTechTreeNodeViewData>();
        previewVideoFadeInDuration = Mathf.Max(0.0f, previewVideoFadeInDuration);
        previewVideoFadeOutDuration = Mathf.Max(0.0f, previewVideoFadeOutDuration);
        incompleteLinePulseDuration = Mathf.Max(0.1f, incompleteLinePulseDuration);
        if (incompleteLinePulseMaxAlpha < incompleteLinePulseMinAlpha)
        {
            incompleteLinePulseMaxAlpha = incompleteLinePulseMinAlpha;
        }
    }
}

/// <summary>
/// 터렛 트리 노드 하나의 표시용 에셋 참조와 보조 텍스트를 정의한다.
/// </summary>
[Serializable]
public class TurretTechTreeNodeViewData
{
    [Header("터렛")]
    [SerializeField] private TurretDefinitionSO definition;
    [SerializeField] private Sprite overrideIcon;

    [Header("미리보기")]
    [SerializeField] private VideoClip previewClip;

    public TurretDefinitionSO Definition => definition;
    public Sprite OverrideIcon => overrideIcon;
    public VideoClip PreviewClip => previewClip;
}
