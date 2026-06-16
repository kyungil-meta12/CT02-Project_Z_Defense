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
    [Header("데미지")]
    [Min(0.01f)] public float damageTickInterval = 0.2f;
    [Min(0.0f)] public float damageMultiplier = 1.0f;
    public bool treatTurretDamageAsDps = true;

    [Header("타겟팅")]
    public BeamAttackTargetMode targetMode = BeamAttackTargetMode.CurrentTarget;
    [Min(1)] public int maxTargets = 1;
    [Min(0.01f)] public float pierceRadius = 0.35f;
    [Min(1)] public int damageBufferSize = 16;
    public LayerMask damageLayerMask = Physics.DefaultRaycastLayers;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("프로스트 상태")]
    [Min(0.0f)] public float freezeDuration = 0.0f;
    [Min(0.0f)] public float slowBuildUpDuration = 0.0f;
    [Range(0.0f, 1.0f)] public float maxSlowRatio = 0.0f;
    [Min(0.0f)] public float slowHoldDuration = 0.0f;
    [Range(0.0f, 1.0f)] public float freezeTriggerRatio = 0.9f;
    public GameObject freezeEffectPrefab;
    [Min(0.0f)] public float freezeEffectDuration = 5.5f;
    [Min(0.0f)] public float freezeExplosionDamageDelay = 2.2f;
    [Min(0.0f)] public float freezeExplosionRadius = 0.0f;
    [Min(0.0f)] public float freezeExplosionDamage = 0.0f;
    public LayerMask freezeExplosionLayerMask = Physics.DefaultRaycastLayers;
    [Min(0.0f)] public float freezeCooldownPerTarget = 0.0f;
    [Range(0.0f, 1.0f)] public float freezeExplosionSlowRatio = 0.3f;
    [Min(0.0f)] public float freezeExplosionSlowDuration = 1.0f;

    public bool HasFrostStatus
    {
        get
        {
            return maxSlowRatio > 0.0f && (slowBuildUpDuration > 0.0f || slowHoldDuration > 0.0f)
                || freezeDuration > 0.0f
                || freezeEffectPrefab != null
                || freezeExplosionDamage > 0.0f
                || freezeExplosionSlowRatio > 0.0f && freezeExplosionSlowDuration > 0.0f;
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
        freezeDuration = Mathf.Max(0.0f, freezeDuration);
        slowBuildUpDuration = Mathf.Max(0.0f, slowBuildUpDuration);
        maxSlowRatio = Mathf.Clamp01(maxSlowRatio);
        slowHoldDuration = Mathf.Max(0.0f, slowHoldDuration);
        freezeTriggerRatio = Mathf.Clamp01(freezeTriggerRatio);
        freezeEffectDuration = Mathf.Max(0.0f, freezeEffectDuration);
        freezeExplosionDamageDelay = Mathf.Max(0.0f, freezeExplosionDamageDelay);
        freezeExplosionRadius = Mathf.Max(0.0f, freezeExplosionRadius);
        freezeExplosionDamage = Mathf.Max(0.0f, freezeExplosionDamage);
        freezeCooldownPerTarget = Mathf.Max(0.0f, freezeCooldownPerTarget);
        freezeExplosionSlowRatio = Mathf.Clamp01(freezeExplosionSlowRatio);
        freezeExplosionSlowDuration = Mathf.Max(0.0f, freezeExplosionSlowDuration);
    }
}
