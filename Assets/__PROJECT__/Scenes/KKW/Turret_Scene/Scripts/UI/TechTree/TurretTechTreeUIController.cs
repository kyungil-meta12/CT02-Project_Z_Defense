using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전체 터렛 트리 팝업의 열기/닫기, 노드 상태 계산, 노드/라인 갱신을 조율한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretTechTreeUIController : MonoBehaviour
{
    [Header("표시 루트")]
    [SerializeField] private GameObject popupRoot;

    [Header("트리 데이터")]
    [SerializeField] private TurretTechTreeViewProfileSO viewProfile;

    [Header("수동 배치 UI")]
    [SerializeField] private ScrollRect treeScrollRect;
    [SerializeField] private TurretTechTreeNodeUI[] nodeViews = Array.Empty<TurretTechTreeNodeUI>();
    [SerializeField] private TurretTechTreeLineUI[] lineViews = Array.Empty<TurretTechTreeLineUI>();
    [SerializeField] private TurretTechTreeDetailPopupUI detailPopup;

    [Header("동작")]
    [SerializeField] private bool refreshWhenOpened = true;
    [SerializeField] private bool pauseGameWhileOpen;
    [SerializeField] private Vector2 openedScrollNormalizedPosition = new Vector2(0.5f, 0.0f);

    private readonly Dictionary<TurretDefinitionSO, TurretTechTreeNodeState> nodeStates = new Dictionary<TurretDefinitionSO, TurretTechTreeNodeState>(64);
    private readonly Dictionary<TechTreeEdgeKey, TurretTechTreeNodeState> edgeStates = new Dictionary<TechTreeEdgeKey, TurretTechTreeNodeState>(128);
    private readonly List<TurretDefinitionRuntimeController> installedTurrets = new List<TurretDefinitionRuntimeController>(16);
    private readonly List<TechTreeEdgeData> edgeData = new List<TechTreeEdgeData>(128);
    private float previousTimeScale = 1.0f;
    private bool hasPausedGame;

    // 컴포넌트 추가 시 하위 UI 참조를 자동 수집한다
    private void Reset()
    {
        popupRoot = gameObject;
        BindChildReferences();
    }

    // 시작 전에 하위 UI를 초기화하고 기본으로 숨긴다
    private void Awake()
    {
        if (popupRoot == null)
        {
            popupRoot = gameObject;
        }

        BindChildReferences();
        InitializeNodeViews();
        Hide();
    }

    // 비활성화될 때 일시정지 상태가 남지 않도록 복구한다
    private void OnDisable()
    {
        ResumeGameIfNeeded();
    }

    [ContextMenu("참조 다시 연결")]
    // 하위 노드, 라인, 상세 팝업 참조를 다시 수집한다
    public void BindChildReferences()
    {
        treeScrollRect = treeScrollRect != null ? treeScrollRect : GetComponentInChildren<ScrollRect>(true);
        nodeViews = GetComponentsInChildren<TurretTechTreeNodeUI>(true);
        lineViews = GetComponentsInChildren<TurretTechTreeLineUI>(true);
        detailPopup = detailPopup != null ? detailPopup : GetComponentInChildren<TurretTechTreeDetailPopupUI>(true);
    }

    // 터렛 트리 팝업을 표시하고 상태를 갱신한다
    public void Show()
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
        }

        PauseGameIfNeeded();
        ResetScrollPosition();

        if (refreshWhenOpened)
        {
            Refresh();
        }
    }

    // 터렛 트리 팝업을 열 때 루트 노드가 중앙 하단에서 보이도록 스크롤 위치를 초기화한다
    private void ResetScrollPosition()
    {
        if (treeScrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        treeScrollRect.StopMovement();
        treeScrollRect.normalizedPosition = openedScrollNormalizedPosition;
    }

    // 터렛 트리 팝업을 숨기고 상세 팝업과 일시정지를 정리한다
    public void Hide()
    {
        if (detailPopup != null)
        {
            detailPopup.Hide();
        }

        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }

        ResumeGameIfNeeded();
    }

    // 현재 표시 상태에 따라 터렛 트리 팝업을 열거나 닫는다
    public void Toggle()
    {
        bool isActive = popupRoot != null && popupRoot.activeSelf;
        if (isActive)
        {
            Hide();
            return;
        }

        Show();
    }

    [ContextMenu("상태 새로고침")]
    // 설치된 터렛 기준으로 노드와 연결선 상태를 다시 계산하고 UI에 적용한다
    public void Refresh()
    {
        InitializeNodeViews();
        BuildGraphData();
        CollectInstalledTurrets();
        CalculateNodeStates();
        CalculateEdgeStates();
        ApplyNodeStates();
        ApplyLineStates();
    }

    // 노드 클릭 시 상세 팝업을 표시한다
    public void ShowNodeDetail(TurretTechTreeNodeUI nodeView)
    {
        if (nodeView == null || detailPopup == null)
        {
            return;
        }

        TurretDefinitionSO definition = nodeView.Definition;
        TurretTechTreeNodeViewData nodeData = viewProfile == null ? null : viewProfile.FindNodeData(definition);
        TurretTechTreeNodeState state = GetNodeState(definition);
        detailPopup.Show(definition, nodeData, state, viewProfile);
    }

    // 지정 터렛 정의의 현재 노드 상태를 반환한다
    public TurretTechTreeNodeState GetNodeState(TurretDefinitionSO definition)
    {
        if (definition != null && nodeStates.TryGetValue(definition, out TurretTechTreeNodeState state))
        {
            return state;
        }

        return TurretTechTreeNodeState.Locked;
    }

    // 지정 부모-자식 연결의 현재 상태를 반환한다
    public TurretTechTreeNodeState GetEdgeState(TurretDefinitionSO parentDefinition, TurretDefinitionSO childDefinition)
    {
        TechTreeEdgeKey key = new TechTreeEdgeKey(parentDefinition, childDefinition);
        if (edgeStates.TryGetValue(key, out TurretTechTreeNodeState state))
        {
            return state;
        }

        return TurretTechTreeNodeState.Locked;
    }

    // 노드 뷰에 상위 컨트롤러 참조를 전달한다
    private void InitializeNodeViews()
    {
        if (nodeViews == null)
        {
            nodeViews = Array.Empty<TurretTechTreeNodeUI>();
            return;
        }

        for (int i = 0; i < nodeViews.Length; i++)
        {
            if (nodeViews[i] != null)
            {
                nodeViews[i].Initialize(this);
            }
        }
    }

    // 표시 프로필의 터렛 정의와 EvolutionProgression에서 그래프 연결 데이터를 구성한다
    private void BuildGraphData()
    {
        nodeStates.Clear();
        edgeStates.Clear();
        edgeData.Clear();

        if (viewProfile == null || viewProfile.Nodes == null)
        {
            return;
        }

        TurretTechTreeNodeViewData[] nodes = viewProfile.Nodes;
        for (int i = 0; i < nodes.Length; i++)
        {
            TurretDefinitionSO definition = nodes[i] == null ? null : nodes[i].Definition;
            if (definition == null || nodeStates.ContainsKey(definition))
            {
                continue;
            }

            nodeStates.Add(definition, TurretTechTreeNodeState.Locked);
        }

        for (int i = 0; i < nodes.Length; i++)
        {
            TurretDefinitionSO parentDefinition = nodes[i] == null ? null : nodes[i].Definition;
            AddEdgesFromDefinition(parentDefinition);
        }
    }

    // 터렛 정의의 진화 엔트리를 연결 데이터에 추가한다
    private void AddEdgesFromDefinition(TurretDefinitionSO parentDefinition)
    {
        if (parentDefinition == null || parentDefinition.evolutionProgressionProfile == null)
        {
            return;
        }

        TurretEvolutionEntry[] entries = parentDefinition.evolutionProgressionProfile.evolutionEntries;
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            TurretEvolutionEntry entry = entries[i];
            if (entry == null || entry.targetDefinition == null)
            {
                continue;
            }

            edgeData.Add(new TechTreeEdgeData(parentDefinition, entry.targetDefinition, entry));
            TechTreeEdgeKey key = new TechTreeEdgeKey(parentDefinition, entry.targetDefinition);
            if (!edgeStates.ContainsKey(key))
            {
                edgeStates.Add(key, TurretTechTreeNodeState.Locked);
            }
        }
    }

    // 현재 씬에 설치된 활성 터렛 컨트롤러들을 수집한다
    private void CollectInstalledTurrets()
    {
        installedTurrets.Clear();
        TurretDefinitionRuntimeController[] candidates = FindObjectsByType<TurretDefinitionRuntimeController>(FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            TurretDefinitionRuntimeController candidate = candidates[i];
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.CurrentTurretDefinition == null)
            {
                continue;
            }

            installedTurrets.Add(candidate);
        }
    }

    // 설치된 터렛과 기본 루트 기준으로 모든 노드 상태를 계산한다
    private void CalculateNodeStates()
    {
        ApplyDefaultUnlockedStateIfNeeded();

        for (int i = 0; i < installedTurrets.Count; i++)
        {
            TurretDefinitionRuntimeController turret = installedTurrets[i];
            if (turret == null)
            {
                continue;
            }

            SetNodeState(turret.CurrentTurretDefinition, TurretTechTreeNodeState.Unlocked);
            MarkAncestorNodesUnlocked(turret.CurrentTurretDefinition, 0);
        }

        for (int i = 0; i < installedTurrets.Count; i++)
        {
            ApplyAvailableChildStates(installedTurrets[i]);
        }
    }

    // 설치 터렛이 없을 때 기본 해금 루트만 밝게 표시한다
    private void ApplyDefaultUnlockedStateIfNeeded()
    {
        if (installedTurrets.Count > 0 || viewProfile == null || viewProfile.DefaultUnlockedDefinitions == null)
        {
            return;
        }

        TurretDefinitionSO[] definitions = viewProfile.DefaultUnlockedDefinitions;
        for (int i = 0; i < definitions.Length; i++)
        {
            SetNodeState(definitions[i], TurretTechTreeNodeState.Unlocked);
        }
    }

    // 지정 터렛 정의의 모든 가능한 조상 노드를 해금 상태로 표시한다
    private void MarkAncestorNodesUnlocked(TurretDefinitionSO childDefinition, int depth)
    {
        if (childDefinition == null || depth > 64)
        {
            return;
        }

        for (int i = 0; i < edgeData.Count; i++)
        {
            TechTreeEdgeData edge = edgeData[i];
            if (edge.ChildDefinition != childDefinition)
            {
                continue;
            }

            SetNodeState(edge.ParentDefinition, TurretTechTreeNodeState.Unlocked);
            MarkAncestorNodesUnlocked(edge.ParentDefinition, depth + 1);
        }
    }

    // 설치된 부모 터렛에서 직접 진화 가능한 자식 노드 상태를 계산한다
    private void ApplyAvailableChildStates(TurretDefinitionRuntimeController parentTurret)
    {
        if (parentTurret == null || parentTurret.CurrentTurretDefinition == null)
        {
            return;
        }

        TurretDefinitionSO parentDefinition = parentTurret.CurrentTurretDefinition;
        for (int i = 0; i < edgeData.Count; i++)
        {
            TechTreeEdgeData edge = edgeData[i];
            if (edge.ParentDefinition != parentDefinition)
            {
                continue;
            }

            TurretTechTreeNodeState state = ResolveChildState(parentTurret, edge.Entry);
            SetNodeState(edge.ChildDefinition, state);
        }
    }

    // 설치된 부모 터렛과 진화 엔트리로 자식 노드 상태를 결정한다
    private TurretTechTreeNodeState ResolveChildState(TurretDefinitionRuntimeController parentTurret, TurretEvolutionEntry entry)
    {
        if (parentTurret == null || entry == null || entry.targetDefinition == null)
        {
            return TurretTechTreeNodeState.Locked;
        }

        int requiredLevel = Mathf.Max(1, entry.requiredLevel);
        if (parentTurret.CurrentTierLevel < requiredLevel)
        {
            return TurretTechTreeNodeState.BlockedByLevel;
        }

        return CanAffordCosts(entry.evolutionCosts) ? TurretTechTreeNodeState.Ready : TurretTechTreeNodeState.BlockedByCost;
    }

    // 연결선별 상태를 부모-자식 관계 기준으로 계산한다
    private void CalculateEdgeStates()
    {
        for (int i = 0; i < edgeData.Count; i++)
        {
            TechTreeEdgeData edge = edgeData[i];
            TurretTechTreeNodeState state = ResolveEdgeState(edge);
            edgeStates[new TechTreeEdgeKey(edge.ParentDefinition, edge.ChildDefinition)] = state;
        }
    }

    // 단일 연결선의 상태를 계산한다
    private TurretTechTreeNodeState ResolveEdgeState(TechTreeEdgeData edge)
    {
        if (edge.ParentDefinition == null || edge.ChildDefinition == null)
        {
            return TurretTechTreeNodeState.Locked;
        }

        TurretTechTreeNodeState childState = GetNodeState(edge.ChildDefinition);
        TurretTechTreeNodeState parentState = GetNodeState(edge.ParentDefinition);
        if (childState == TurretTechTreeNodeState.Unlocked && parentState == TurretTechTreeNodeState.Unlocked)
        {
            return TurretTechTreeNodeState.Unlocked;
        }

        TurretDefinitionRuntimeController parentTurret = FindInstalledTurret(edge.ParentDefinition);
        if (parentTurret == null)
        {
            return TurretTechTreeNodeState.Locked;
        }

        return ResolveChildState(parentTurret, edge.Entry);
    }

    // 지정 터렛 정의를 가진 설치 터렛을 찾는다
    private TurretDefinitionRuntimeController FindInstalledTurret(TurretDefinitionSO definition)
    {
        if (definition == null)
        {
            return null;
        }

        for (int i = 0; i < installedTurrets.Count; i++)
        {
            TurretDefinitionRuntimeController turret = installedTurrets[i];
            if (turret != null && turret.CurrentTurretDefinition == definition)
            {
                return turret;
            }
        }

        return null;
    }

    // 노드 상태를 우선순위가 더 높을 때만 갱신한다
    private void SetNodeState(TurretDefinitionSO definition, TurretTechTreeNodeState state)
    {
        if (definition == null)
        {
            return;
        }

        if (!nodeStates.TryGetValue(definition, out TurretTechTreeNodeState currentState))
        {
            nodeStates.Add(definition, state);
            return;
        }

        if ((int)state > (int)currentState)
        {
            nodeStates[definition] = state;
        }
    }

    // 모든 노드 뷰에 계산된 상태를 적용한다
    private void ApplyNodeStates()
    {
        if (nodeViews == null)
        {
            return;
        }

        for (int i = 0; i < nodeViews.Length; i++)
        {
            TurretTechTreeNodeUI nodeView = nodeViews[i];
            if (nodeView == null)
            {
                continue;
            }

            nodeView.ApplyState(GetNodeState(nodeView.Definition), viewProfile);
        }
    }

    // 모든 연결선 뷰에 계산된 상태를 적용한다
    private void ApplyLineStates()
    {
        if (lineViews == null)
        {
            return;
        }

        for (int i = 0; i < lineViews.Length; i++)
        {
            TurretTechTreeLineUI lineView = lineViews[i];
            if (lineView == null)
            {
                continue;
            }

            lineView.ApplyState(GetEdgeState(lineView.ParentDefinition, lineView.ChildDefinition), viewProfile);
        }
    }

    // 비용 배열을 현재 재화로 지불할 수 있는지 확인한다
    private static bool CanAffordCosts(ResourceCost[] costs)
    {
        if (!HasPayableCosts(costs))
        {
            return true;
        }

        if (InventorySystem.Inst == null)
        {
            return false;
        }

        return InventorySystem.Inst.CanAfford(costs);
    }

    // 실제 지불해야 하는 비용이 하나 이상 있는지 확인한다
    private static bool HasPayableCosts(ResourceCost[] costs)
    {
        if (costs == null)
        {
            return false;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost != null && cost.amount > 0)
            {
                return true;
            }
        }

        return false;
    }

    // 설정이 켜져 있으면 게임 시간을 일시정지한다
    private void PauseGameIfNeeded()
    {
        if (!pauseGameWhileOpen || hasPausedGame)
        {
            return;
        }

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0.0f;
        hasPausedGame = true;
    }

    // 이 컨트롤러가 적용한 일시정지를 복구한다
    private void ResumeGameIfNeeded()
    {
        if (!hasPausedGame)
        {
            return;
        }

        Time.timeScale = previousTimeScale;
        hasPausedGame = false;
    }

    // 연결선 딕셔너리 키를 정의한다
    private readonly struct TechTreeEdgeKey : IEquatable<TechTreeEdgeKey>
    {
        private readonly TurretDefinitionSO parentDefinition;
        private readonly TurretDefinitionSO childDefinition;

        // 부모와 자식 터렛 정의로 키를 초기화한다
        public TechTreeEdgeKey(TurretDefinitionSO parentDefinition_, TurretDefinitionSO childDefinition_)
        {
            parentDefinition = parentDefinition_;
            childDefinition = childDefinition_;
        }

        // 같은 부모와 자식 정의를 가리키는지 비교한다
        public bool Equals(TechTreeEdgeKey other)
        {
            return parentDefinition == other.parentDefinition && childDefinition == other.childDefinition;
        }

        // 오브젝트 비교를 연결선 키 비교로 변환한다
        public override bool Equals(object obj)
        {
            return obj is TechTreeEdgeKey other && Equals(other);
        }

        // 부모와 자식 참조로 해시 코드를 생성한다
        public override int GetHashCode()
        {
            int parentHash = parentDefinition == null ? 0 : parentDefinition.GetHashCode();
            int childHash = childDefinition == null ? 0 : childDefinition.GetHashCode();
            return (parentHash * 397) ^ childHash;
        }
    }

    // 연결선 상태 계산에 필요한 부모, 자식, 진화 엔트리를 보관한다
    private readonly struct TechTreeEdgeData
    {
        public readonly TurretDefinitionSO ParentDefinition;
        public readonly TurretDefinitionSO ChildDefinition;
        public readonly TurretEvolutionEntry Entry;

        // 연결선 계산 데이터를 초기화한다
        public TechTreeEdgeData(TurretDefinitionSO parentDefinition, TurretDefinitionSO childDefinition, TurretEvolutionEntry entry)
        {
            ParentDefinition = parentDefinition;
            ChildDefinition = childDefinition;
            Entry = entry;
        }
    }
}
