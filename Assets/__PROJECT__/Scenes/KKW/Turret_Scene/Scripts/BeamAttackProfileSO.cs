using UnityEngine;

public enum BeamAttackTargetMode
{
    CurrentTarget,
    PierceLine,
    ChainNearest
}

/// <summary>
/// 빔 터렛의 데미지 틱, 관통, 다중 타겟, Frost 상태 효과 설정을 관리한다.
/// </summary>
[CreateAssetMenu(menuName = "Project Z Defense/Beam Attack Profile")]
public class BeamAttackProfileSO : ScriptableObject
{
    [Header("Damage")]
    [Min(0.01f)] public float damageTickInterval = 0.2f;
    [Min(0.0f)] public float damageMultiplier = 1.0f;
    public bool treatTurretDamageAsDps = true;

    [Header("Targeting")]
    public BeamAttackTargetMode targetMode = BeamAttackTargetMode.CurrentTarget;
    [Min(1)] public int maxTargets = 1;
    [Min(0.01f)] public float pierceRadius = 0.35f;
    [Min(1)] public int damageBufferSize = 16;
    public LayerMask damageLayerMask = Physics.DefaultRaycastLayers;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Frost Status")]
    [Range(0.0f, 1.0f)] public float slowRatio = 0.0f;
    [Min(0.0f)] public float slowDuration = 0.0f;
    [Min(0.0f)] public float freezeDuration = 0.0f;

    public bool HasFrostStatus
    {
        get
        {
            return slowRatio > 0.0f && slowDuration > 0.0f || freezeDuration > 0.0f;
        }
    }

    // 인스펙터에서 입력한 빔 공격 값을 안전한 범위로 보정한다
    private void OnValidate()
    {
        damageTickInterval = Mathf.Max(0.01f, damageTickInterval);
        damageMultiplier = Mathf.Max(0.0f, damageMultiplier);
        maxTargets = Mathf.Max(1, maxTargets);
        pierceRadius = Mathf.Max(0.01f, pierceRadius);
        damageBufferSize = Mathf.Max(maxTargets, damageBufferSize);
        slowRatio = Mathf.Clamp01(slowRatio);
        slowDuration = Mathf.Max(0.0f, slowDuration);
        freezeDuration = Mathf.Max(0.0f, freezeDuration);
    }
}
