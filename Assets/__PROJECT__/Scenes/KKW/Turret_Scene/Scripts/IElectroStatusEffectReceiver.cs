using UnityEngine;

namespace ProjectZDefense.StatusEffects
{
    /// <summary>
    /// Electro 상태 효과 적용에 필요한 체인, 쇼크 스택, 과부하, 경직 데이터를 전달하는 값 타입이다.
    /// </summary>
    public struct ElectroStatusPayload
    {
        public bool hasElectroStatus;
        public int maxChainTargets;
        public float chainRadius;
        public float chainDamageFalloffPerJump;
        public LayerMask chainTargetLayerMask;
        public int maxShockStackCount;
        public float shockStackDuration;
        public bool canElectroHitTriggerOverload;
        public bool canNonElectroDamageTriggerOverload;
        public float overloadRadius;
        public float overloadDamageMultiplier;
        public float stunDuration;
        public float stunDurationFalloffPerJump;
        public float minimumStunDuration;
        public float bossStunDurationMultiplier;
        public bool playChainLinkEffect;
        public global::ElectroStatusProfileSO sourceProfile;
        public GameObject chainLinkEffectPrefab;
        public float chainLinkEffectDuration;
        public float chainLinkVerticalOffset;
        public Vector3 chainLinkSourceAxis;
        public bool useChainLinkEndpointFit;
        public Vector3 chainLinkLocalStartPoint;
        public Vector3 chainLinkLocalEndPoint;
        public Vector3 chainLinkLocalPositionOffset;
        public Vector3 chainLinkRotationEulerOffset;
        public float chainLinkLengthScaleMultiplier;
        public float chainLinkThicknessScale;
        public bool playChainCoreLine;
        public Material chainCoreLineMaterial;
        public Color chainCoreLineStartColor;
        public Color chainCoreLineEndColor;
        public float chainCoreLineWidth;
        public float chainCoreLineStartDelay;
        public float chainCoreLineDuration;
    }

    /// <summary>
    /// Electro 계열 공격에서 쇼크, 과부하, 경직 상태 효과를 받을 수 있는 런타임 대상 인터페이스다.
    /// </summary>
    public interface IElectroStatusEffectReceiver
    {
        // 대상에게 Electro 상태 효과를 적용한다
        void ApplyElectroStatus(ElectroStatusPayload payload, int chainIndex, float sourceDamage);
    }
}
