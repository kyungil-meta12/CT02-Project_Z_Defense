using System.Collections.Generic;

// 샵에서 구매 가능한 터렛부터 진화 트리를 펼쳐 도달 가능한 모든 터렛 종류의 재화별 누적 비용을 계산한다.
internal sealed class TurretEvolutionGraphBuilder
{
    // 상점 엔트리 목록에서 시작해 진화 그래프 전체를 깊이(Tier) 오름차순으로 펼친다
    public List<TurretEvolutionNode> Build(List<TurretShopEntrySO> shopEntries, List<ReportWarning> warnings)
    {
        List<TurretEvolutionNode> nodes = new List<TurretEvolutionNode>();
        Dictionary<TurretDefinitionSO, TurretEvolutionNode> nodeByDefinition = new Dictionary<TurretDefinitionSO, TurretEvolutionNode>();
        Queue<TurretEvolutionNode> queue = new Queue<TurretEvolutionNode>();

        for (int i = 0; i < shopEntries.Count; i++)
        {
            TurretShopEntrySO entry = shopEntries[i];
            if (entry == null || entry.TurretDefinition == null || nodeByDefinition.ContainsKey(entry.TurretDefinition))
            {
                continue;
            }

            TurretEvolutionNode root = new TurretEvolutionNode
            {
                Definition = entry.TurretDefinition,
                RootShopEntry = entry,
                Tier = 0,
                CumulativeReachCost = new Dictionary<RewardCurrencyType, int>(),
                NonRootCoinCost = 0
            };
            TurretEconomySimulationCalculator.AddCosts(root.CumulativeReachCost, entry.GetPlacementCosts(0));

            nodeByDefinition.Add(root.Definition, root);
            nodes.Add(root);
            queue.Enqueue(root);
        }

        while (queue.Count > 0)
        {
            ExpandNode(queue.Dequeue(), nodeByDefinition, nodes, queue, warnings);
        }

        return nodes;
    }

    // 노드의 진화 후보를 펼쳐 다음 Tier 노드로 추가한다
    private static void ExpandNode(TurretEvolutionNode node, Dictionary<TurretDefinitionSO, TurretEvolutionNode> nodeByDefinition, List<TurretEvolutionNode> nodes, Queue<TurretEvolutionNode> queue, List<ReportWarning> warnings)
    {
        TurretEvolutionProgressionSO progression = node.Definition.evolutionProgressionProfile;
        TurretEvolutionEntry[] entries = progression == null ? null : progression.evolutionEntries;
        int requiredLevel = progression == null ? 0 : progression.GetNextRequiredEvolutionLevel(1);
        if (entries == null || entries.Length == 0 || requiredLevel <= 0)
        {
            node.IsTerminal = true;
            return;
        }

        node.RequiredEvolutionLevel = requiredLevel;
        node.UpgradeCostToRequiredLevel = new Dictionary<RewardCurrencyType, int>();
        if (node.Definition.upgradeCostProfile != null)
        {
            TurretEconomySimulationCalculator.AddCosts(node.UpgradeCostToRequiredLevel, node.Definition.upgradeCostProfile.GetCosts(1, requiredLevel));
        }

        for (int i = 0; i < entries.Length; i++)
        {
            TurretEvolutionEntry entry = entries[i];
            if (entry == null || entry.targetDefinition == null || nodeByDefinition.ContainsKey(entry.targetDefinition))
            {
                continue;
            }

            TurretEvolutionNode child = new TurretEvolutionNode
            {
                Definition = entry.targetDefinition,
                RootShopEntry = node.RootShopEntry,
                Tier = node.Tier + 1,
                CumulativeReachCost = TurretEconomySimulationCalculator.CloneCosts(node.CumulativeReachCost),
                NonRootCoinCost = node.NonRootCoinCost + TurretEconomySimulationCalculator.GetCoinAmount(node.UpgradeCostToRequiredLevel) + TurretEconomySimulationCalculator.GetCoinCost(entry.evolutionCosts)
            };
            TurretEconomySimulationCalculator.AddCosts(child.CumulativeReachCost, node.UpgradeCostToRequiredLevel);
            TurretEconomySimulationCalculator.AddCosts(child.CumulativeReachCost, entry.evolutionCosts);

            nodeByDefinition.Add(child.Definition, child);
            nodes.Add(child);
            queue.Enqueue(child);
        }
    }
}
