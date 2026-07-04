/// <summary>
/// 터렛 트리 노드와 연결선의 현재 진행 상태를 정의한다.
/// </summary>
public enum TurretTechTreeNodeState
{
    Locked = 0,
    BlockedByLevel = 1,
    BlockedByCost = 2,
    Ready = 3,
    Unlocked = 4
}
