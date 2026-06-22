using ProjectZDefense.StatusEffects;
using UnityEngine;

public enum StatusEffectVisualType
{
    FrostSlow,
    Poison,
    ElectroStun,
    IgnitionBurn,
    IgnitionReaction
}

public enum StatusEffectVisualAttachMode
{
    Anchor,
    RendererOverlay
}

/// <summary>
/// 상태이상별 비주얼 프리팹, 부착 위치, 렌더러 Overlay 대상을 정의하는 슬롯 데이터다.
/// </summary>
[System.Serializable]
public sealed class StatusEffectVisualSlot
{
    [Header("상태이상")]
    public StatusEffectVisualType visualType;
    public IgnitionReactionType ignitionReactionType;
    public StatusEffectVisualAttachMode attachMode;

    [Header("프리팹")]
    public GameObject visualPrefab;

    [Header("앵커 부착")]
    public Transform anchor;
    public Vector3 localPositionOffset;
    public Vector3 localEulerOffset;
    public Vector3 localScale = Vector3.one;

    [Header("렌더러 오버레이")]
    public Renderer[] targetRenderers;
    [Min(0.01f)] public float particleScaleMultiplier = 1.0f;

    [Header("재생 옵션")]
    public bool restartParticlesOnEnable = true;
    [Min(0.0f)] public float fadeInDuration;
    [Min(0.0f)] public float fadeOutDuration;

    [Header("처치 예고 표시")]
    public string lethalIndicatorChildName;
    public Vector3 lethalIndicatorLocalPositionOffset;

    [System.NonSerialized] public GameObject[] runtimeInstances;
    [System.NonSerialized] public GameObject[] lethalIndicatorInstances;
    [System.NonSerialized] public Material[][] originalSharedMaterials;
}

/// <summary>
/// 좀비 상태이상에 따라 대상 렌더러 또는 지정 앵커 기반 비주얼 이펙트를 켜고 끄는 공통 컨트롤러다.
/// </summary>
public class StatusEffectVisualController : MonoBehaviour
{
    [Header("상태이상 비주얼 슬롯")]
    [SerializeField] private StatusEffectVisualSlot[] visualSlots;

    private bool frostSlowVisualActive;
    private bool poisonVisualActive;
    private bool electroStunVisualActive;
    private bool ignitionBurnVisualActive;
    private IgnitionReactionType activeIgnitionReactionType;

    // 컴포넌트가 비활성화될 때 상태이상 비주얼과 임시 머티리얼을 정리한다
    private void OnDisable()
    {
        SetFrostSlowActive(false);
        SetPoisonActive(false);
        SetElectroStunActive(false);
        SetIgnitionBurnActive(false);
        SetIgnitionReactionActive(IgnitionReactionType.None);
        RestoreVisualSlotOriginalMaterials(StatusEffectVisualType.FrostSlow);
        RestoreVisualSlotOriginalMaterials(StatusEffectVisualType.Poison);
        RestoreVisualSlotOriginalMaterials(StatusEffectVisualType.IgnitionBurn);
        RestoreVisualSlotOriginalMaterials(StatusEffectVisualType.IgnitionReaction);
    }

    // 오브젝트가 파괴될 때 상태이상 비주얼과 임시 머티리얼을 정리한다
    private void OnDestroy()
    {
        SetFrostSlowActive(false);
        SetPoisonActive(false);
        SetElectroStunActive(false);
        SetIgnitionBurnActive(false);
        SetIgnitionReactionActive(IgnitionReactionType.None);
        RestoreVisualSlotOriginalMaterials(StatusEffectVisualType.FrostSlow);
        RestoreVisualSlotOriginalMaterials(StatusEffectVisualType.Poison);
        RestoreVisualSlotOriginalMaterials(StatusEffectVisualType.IgnitionBurn);
        RestoreVisualSlotOriginalMaterials(StatusEffectVisualType.IgnitionReaction);
    }

    // 프로스트 슬로우 상태에 맞춰 얼음 메시 이펙트를 활성화하거나 비활성화한다
    public void SetFrostSlowActive(bool isActive)
    {
        if (frostSlowVisualActive == isActive)
        {
            return;
        }

        if (isActive)
        {
            EnsureVisualSlotInstances(StatusEffectVisualType.FrostSlow);
            CacheVisualSlotOriginalMaterials(StatusEffectVisualType.FrostSlow);
        }

        bool hasDelayedMaterialRestore = SetVisualSlotInstancesActive(StatusEffectVisualType.FrostSlow, isActive);
        frostSlowVisualActive = isActive;

        if (!isActive && !hasDelayedMaterialRestore)
        {
            RestoreVisualSlotOriginalMaterials(StatusEffectVisualType.FrostSlow);
        }
    }

    // 포이즌 상태에 맞춰 독 이펙트를 활성화하거나 비활성화한다
    public void SetPoisonActive(bool isActive)
    {
        if (poisonVisualActive == isActive)
        {
            return;
        }

        if (isActive)
        {
            EnsureVisualSlotInstances(StatusEffectVisualType.Poison);
            CacheVisualSlotOriginalMaterials(StatusEffectVisualType.Poison);
        }

        bool hasDelayedMaterialRestore = SetVisualSlotInstancesActive(StatusEffectVisualType.Poison, isActive);
        poisonVisualActive = isActive;

        if (!isActive)
        {
            SetPoisonLethalIndicatorActive(false);
            if (!hasDelayedMaterialRestore)
            {
                RestoreVisualSlotOriginalMaterials(StatusEffectVisualType.Poison);
            }
        }
    }

    // Electro 경직 상태에 맞춰 전기 경직 이펙트를 활성화하거나 비활성화한다
    public void SetElectroStunActive(bool isActive)
    {
        if (electroStunVisualActive == isActive)
        {
            return;
        }

        if (isActive)
        {
            EnsureVisualSlotInstances(StatusEffectVisualType.ElectroStun);
        }

        SetVisualSlotInstancesActive(StatusEffectVisualType.ElectroStun, isActive);
        electroStunVisualActive = isActive;
    }

    // Ignition 화상 상태에 맞춰 화염 메시 이펙트를 활성화하거나 비활성화한다
    public void SetIgnitionBurnActive(bool isActive)
    {
        if (ignitionBurnVisualActive == isActive)
        {
            return;
        }

        if (isActive)
        {
            EnsureVisualSlotInstances(StatusEffectVisualType.IgnitionBurn);
            CacheVisualSlotOriginalMaterials(StatusEffectVisualType.IgnitionBurn);
        }

        bool hasDelayedMaterialRestore = SetVisualSlotInstancesActive(StatusEffectVisualType.IgnitionBurn, isActive);
        ignitionBurnVisualActive = isActive;

        if (!isActive && !hasDelayedMaterialRestore)
        {
            RestoreVisualSlotOriginalMaterials(StatusEffectVisualType.IgnitionBurn);
        }
    }

    // Ignition 속성 반응 타입에 맞춰 강화 화염 비주얼을 전환한다
    public void SetIgnitionReactionActive(IgnitionReactionType reactionType)
    {
        if (activeIgnitionReactionType == reactionType)
        {
            return;
        }

        if (activeIgnitionReactionType != IgnitionReactionType.None)
        {
            bool hasDelayedMaterialRestore = SetIgnitionReactionSlotInstancesActive(activeIgnitionReactionType, false);
            if (!hasDelayedMaterialRestore)
            {
                RestoreVisualSlotOriginalMaterials(StatusEffectVisualType.IgnitionReaction);
            }
        }

        activeIgnitionReactionType = reactionType;
        if (activeIgnitionReactionType == IgnitionReactionType.None)
        {
            return;
        }

        EnsureIgnitionReactionSlotInstances(activeIgnitionReactionType);
        CacheIgnitionReactionSlotOriginalMaterials(activeIgnitionReactionType);
        SetIgnitionReactionSlotInstancesActive(activeIgnitionReactionType, true);
    }

    // 포이즌 틱데미지로 사망이 확정된 대상의 표시 아이콘을 켜거나 끈다
    public void SetPoisonLethalIndicatorActive(bool isActive)
    {
        SetLethalIndicatorActive(StatusEffectVisualType.Poison, isActive);
    }

    // 지정한 상태이상 타입에 해당하는 확장 비주얼 슬롯 인스턴스를 준비한다
    private void EnsureVisualSlotInstances(StatusEffectVisualType visualType)
    {
        if (visualSlots == null)
        {
            return;
        }

        for (int i = 0; i < visualSlots.Length; i++)
        {
            StatusEffectVisualSlot slot = visualSlots[i];
            if (slot == null || slot.visualType != visualType || slot.visualPrefab == null)
            {
                continue;
            }

            EnsureVisualSlotInstance(slot);
        }
    }

    // 단일 확장 비주얼 슬롯의 인스턴스를 준비한다
    private void EnsureVisualSlotInstance(StatusEffectVisualSlot slot)
    {
        if (slot.attachMode == StatusEffectVisualAttachMode.RendererOverlay)
        {
            EnsureRendererOverlaySlotInstances(slot);
            return;
        }

        EnsureAnchorSlotInstance(slot);
    }

    // 앵커에 부착되는 단일 비주얼 인스턴스를 준비한다
    private void EnsureAnchorSlotInstance(StatusEffectVisualSlot slot)
    {
        if (slot.runtimeInstances != null && slot.runtimeInstances.Length == 1 && slot.runtimeInstances[0] != null)
        {
            return;
        }

        ClearSlotInstances(slot);
        slot.runtimeInstances = new GameObject[1];
        Transform parent = slot.anchor == null ? transform : slot.anchor;
        GameObject visualInstance = Instantiate(slot.visualPrefab, parent);
        visualInstance.name = slot.visualPrefab.name + "_" + parent.name;
        visualInstance.transform.localPosition = slot.localPositionOffset;
        visualInstance.transform.localRotation = Quaternion.Euler(slot.localEulerOffset);
        visualInstance.transform.localScale = slot.localScale;
        EnsureAlphaFader(visualInstance);
        visualInstance.SetActive(false);
        slot.runtimeInstances[0] = visualInstance;
        CacheSlotLethalIndicators(slot);
        SetSlotLethalIndicatorsActive(slot, false);
    }

    // 렌더러 Overlay 방식의 슬롯 인스턴스를 대상 렌더러 수에 맞춰 준비한다
    private void EnsureRendererOverlaySlotInstances(StatusEffectVisualSlot slot)
    {
        if (slot.targetRenderers == null)
        {
            return;
        }

        if (slot.runtimeInstances == null || slot.runtimeInstances.Length != slot.targetRenderers.Length)
        {
            ClearSlotInstances(slot);
            slot.runtimeInstances = new GameObject[slot.targetRenderers.Length];
        }

        for (int i = 0; i < slot.targetRenderers.Length; i++)
        {
            Renderer targetRenderer = slot.targetRenderers[i];
            if (targetRenderer == null || slot.runtimeInstances[i] != null)
            {
                continue;
            }

            GameObject visualInstance = CreateOverlayVisualInstance(slot.visualPrefab, targetRenderer, transform, slot.particleScaleMultiplier);
            slot.runtimeInstances[i] = visualInstance;
        }

        CacheSlotLethalIndicators(slot);
        SetSlotLethalIndicatorsActive(slot, false);
    }

    // OverlayFX 기반 비주얼 인스턴스를 만들고 대상 렌더러를 연결한다
    private static GameObject CreateOverlayVisualInstance(GameObject visualPrefab, Renderer targetRenderer, Transform parent, float particleScaleMultiplier)
    {
        GameObject visualInstance = Instantiate(visualPrefab, parent);
        visualInstance.name = visualPrefab.name + "_" + targetRenderer.name;
        visualInstance.transform.localPosition = Vector3.zero;
        visualInstance.transform.localRotation = Quaternion.identity;
        visualInstance.transform.localScale = Vector3.one;
        ConfigureOverlayFx(visualInstance, targetRenderer);
        ApplyParticleScaleMultiplier(visualInstance, particleScaleMultiplier);
        EnsureAlphaFader(visualInstance);
        visualInstance.SetActive(false);
        return visualInstance;
    }

    // MeshFX 프리팹의 OverlayFX 대상 렌더러를 런타임에 연결한다
    private static void ConfigureOverlayFx(GameObject visualInstance, Renderer targetRenderer)
    {
        OverlayFX overlayFx = visualInstance.GetComponent<OverlayFX>();
        if (overlayFx == null)
        {
            Debug.LogWarning("[StatusEffectVisualController] 상태이상 OverlayFX 프리팹에 OverlayFX가 없습니다. 비주얼 프리팹 연결을 확인해주세요.", visualInstance);
            return;
        }

        EnsureUniqueOverlayMaterial(overlayFx);
        overlayFx.targetRenderer = targetRenderer;
        ConfigureParticleShapes(overlayFx, targetRenderer);
    }

    // OverlayFX 머티리얼을 인스턴스별로 복제해 페이드 알파가 원본 에셋에 영향을 주지 않게 한다
    private static void EnsureUniqueOverlayMaterial(OverlayFX overlayFx)
    {
        if (overlayFx.overlayMaterial == null)
        {
            return;
        }

        overlayFx.overlayMaterial = new Material(overlayFx.overlayMaterial);
    }

    // OverlayFX 파티클 Shape를 대상 렌더러에 맞춰 즉시 연결한다
    private static void ConfigureParticleShapes(OverlayFX overlayFx, Renderer targetRenderer)
    {
        if (overlayFx.particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < overlayFx.particleSystems.Count; i++)
        {
            ParticleSystem particleSystem = overlayFx.particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;

            if (targetRenderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                shape.skinnedMeshRenderer = skinnedMeshRenderer;
            }
            else if (targetRenderer is MeshRenderer meshRenderer)
            {
                shape.shapeType = ParticleSystemShapeType.MeshRenderer;
                shape.meshRenderer = meshRenderer;
            }
        }
    }

    // 지정한 상태이상 타입에 해당하는 확장 슬롯 인스턴스를 활성화하거나 비활성화한다
    private bool SetVisualSlotInstancesActive(StatusEffectVisualType visualType, bool isActive)
    {
        if (visualSlots == null)
        {
            return false;
        }

        bool hasDelayedMaterialRestore = false;
        for (int i = 0; i < visualSlots.Length; i++)
        {
            StatusEffectVisualSlot slot = visualSlots[i];
            if (slot == null || slot.visualType != visualType)
            {
                continue;
            }

            hasDelayedMaterialRestore |= SetInstancesActive(slot, isActive);
        }

        return hasDelayedMaterialRestore;
    }

    // 지정한 Ignition 반응 타입에 해당하는 비주얼 슬롯 인스턴스를 준비한다
    private void EnsureIgnitionReactionSlotInstances(IgnitionReactionType reactionType)
    {
        if (visualSlots == null)
        {
            return;
        }

        for (int i = 0; i < visualSlots.Length; i++)
        {
            StatusEffectVisualSlot slot = visualSlots[i];
            if (!IsIgnitionReactionSlot(slot, reactionType) || slot.visualPrefab == null)
            {
                continue;
            }

            EnsureVisualSlotInstance(slot);
        }
    }

    // 지정한 Ignition 반응 타입 슬롯의 원본 머티리얼을 캐시한다
    private void CacheIgnitionReactionSlotOriginalMaterials(IgnitionReactionType reactionType)
    {
        if (visualSlots == null)
        {
            return;
        }

        for (int i = 0; i < visualSlots.Length; i++)
        {
            StatusEffectVisualSlot slot = visualSlots[i];
            if (!IsIgnitionReactionSlot(slot, reactionType))
            {
                continue;
            }

            CacheOriginalSharedMaterials(slot);
        }
    }

    // 지정한 Ignition 반응 타입 슬롯 인스턴스만 활성화하거나 비활성화한다
    private bool SetIgnitionReactionSlotInstancesActive(IgnitionReactionType reactionType, bool isActive)
    {
        if (visualSlots == null)
        {
            return false;
        }

        bool hasDelayedMaterialRestore = false;
        for (int i = 0; i < visualSlots.Length; i++)
        {
            StatusEffectVisualSlot slot = visualSlots[i];
            if (!IsIgnitionReactionSlot(slot, reactionType))
            {
                continue;
            }

            hasDelayedMaterialRestore |= SetInstancesActive(slot, isActive);
        }

        return hasDelayedMaterialRestore;
    }

    // 슬롯이 지정한 Ignition 반응 타입에 해당하는지 확인한다
    private static bool IsIgnitionReactionSlot(StatusEffectVisualSlot slot, IgnitionReactionType reactionType)
    {
        return slot != null &&
            slot.visualType == StatusEffectVisualType.IgnitionReaction &&
            slot.ignitionReactionType == reactionType;
    }

    // 지정한 상태이상 타입의 처치 예고 표시 오브젝트를 켜거나 끈다
    private void SetLethalIndicatorActive(StatusEffectVisualType visualType, bool isActive)
    {
        if (visualSlots == null)
        {
            return;
        }

        for (int i = 0; i < visualSlots.Length; i++)
        {
            StatusEffectVisualSlot slot = visualSlots[i];
            if (slot == null || slot.visualType != visualType)
            {
                continue;
            }

            CacheSlotLethalIndicators(slot);
            SetSlotLethalIndicatorsActive(slot, isActive);
        }
    }

    // 슬롯 인스턴스 하위에서 처치 예고 표시 오브젝트를 찾아 캐시한다
    private static void CacheSlotLethalIndicators(StatusEffectVisualSlot slot)
    {
        if (slot == null || string.IsNullOrWhiteSpace(slot.lethalIndicatorChildName) || slot.runtimeInstances == null)
        {
            return;
        }

        if (slot.lethalIndicatorInstances != null && slot.lethalIndicatorInstances.Length == slot.runtimeInstances.Length)
        {
            return;
        }

        slot.lethalIndicatorInstances = new GameObject[slot.runtimeInstances.Length];
        for (int i = 0; i < slot.runtimeInstances.Length; i++)
        {
            GameObject visualInstance = slot.runtimeInstances[i];
            if (visualInstance == null)
            {
                continue;
            }

            Transform indicatorTransform = FindChildRecursive(visualInstance.transform, slot.lethalIndicatorChildName);
            ApplyLethalIndicatorTransformOffset(indicatorTransform, slot.lethalIndicatorLocalPositionOffset);
            slot.lethalIndicatorInstances[i] = indicatorTransform == null ? null : indicatorTransform.gameObject;
        }
    }

    // 처치 예고 표시가 본체와 겹치지 않도록 슬롯에 설정된 로컬 위치 오프셋을 적용한다
    private static void ApplyLethalIndicatorTransformOffset(Transform indicatorTransform, Vector3 localPositionOffset)
    {
        if (indicatorTransform == null || localPositionOffset == Vector3.zero)
        {
            return;
        }

        indicatorTransform.localPosition += localPositionOffset;
    }

    // 슬롯에 캐시된 처치 예고 표시 오브젝트를 활성화하거나 비활성화한다
    private static void SetSlotLethalIndicatorsActive(StatusEffectVisualSlot slot, bool isActive)
    {
        if (slot == null || slot.lethalIndicatorInstances == null)
        {
            return;
        }

        for (int i = 0; i < slot.lethalIndicatorInstances.Length; i++)
        {
            GameObject indicatorInstance = slot.lethalIndicatorInstances[i];
            if (indicatorInstance == null)
            {
                continue;
            }

            indicatorInstance.SetActive(isActive);
        }
    }

    // 이름이 일치하는 자식 트랜스폼을 재귀적으로 찾는다
    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nestedChild = FindChildRecursive(child, childName);
            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
    }

    // 인스턴스 배열을 활성화하고 필요하면 파티클을 재시작한다
    private static bool SetInstancesActive(StatusEffectVisualSlot slot, bool isActive)
    {
        GameObject[] instances = slot.runtimeInstances;
        if (instances == null)
        {
            return false;
        }

        bool hasDelayedMaterialRestore = false;
        for (int i = 0; i < instances.Length; i++)
        {
            GameObject visualInstance = instances[i];
            if (visualInstance == null)
            {
                continue;
            }

            StatusEffectVisualAlphaFader alphaFader = EnsureAlphaFader(visualInstance);
            if (isActive)
            {
                visualInstance.SetActive(true);
                alphaFader.PlayFadeIn(slot.fadeInDuration, slot.restartParticlesOnEnable);
                continue;
            }

            if (ShouldDelayRendererMaterialRestore(slot))
            {
                Renderer restoreRenderer = ResolveRestoreRenderer(slot, i);
                Material[] restoreMaterials = ResolveRestoreMaterials(slot, i);
                alphaFader.PlayFadeOut(slot.fadeOutDuration, restoreRenderer, restoreMaterials);
                hasDelayedMaterialRestore = true;
                continue;
            }

            alphaFader.PlayFadeOut(slot.fadeOutDuration);
        }

        return hasDelayedMaterialRestore;
    }

    // RendererOverlay 슬롯의 머티리얼 복구를 페이드 완료까지 늦춰야 하는지 확인한다
    private static bool ShouldDelayRendererMaterialRestore(StatusEffectVisualSlot slot)
    {
        return slot.attachMode == StatusEffectVisualAttachMode.RendererOverlay && slot.fadeOutDuration > 0.0f;
    }

    // 페이드 완료 후 원본 머티리얼로 되돌릴 대상 렌더러를 반환한다
    private static Renderer ResolveRestoreRenderer(StatusEffectVisualSlot slot, int index)
    {
        if (slot.targetRenderers == null || index < 0 || index >= slot.targetRenderers.Length)
        {
            return null;
        }

        return slot.targetRenderers[index];
    }

    // 페이드 완료 후 복구할 원본 머티리얼 배열을 반환한다
    private static Material[] ResolveRestoreMaterials(StatusEffectVisualSlot slot, int index)
    {
        if (slot.originalSharedMaterials == null || index < 0 || index >= slot.originalSharedMaterials.Length)
        {
            return null;
        }

        return slot.originalSharedMaterials[index];
    }

    // 상태이상 비주얼 인스턴스에 알파 페이드 컴포넌트를 보장한다
    private static StatusEffectVisualAlphaFader EnsureAlphaFader(GameObject visualInstance)
    {
        StatusEffectVisualAlphaFader alphaFader = visualInstance.GetComponent<StatusEffectVisualAlphaFader>();
        if (alphaFader == null)
        {
            alphaFader = visualInstance.AddComponent<StatusEffectVisualAlphaFader>();
        }

        return alphaFader;
    }

    // 대상 오브젝트 하위의 모든 파티클을 초기화하고 다시 재생한다
    private static void RestartParticles(GameObject visualInstance)
    {
        ParticleSystem[] particleSystems = visualInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    // OverlayFX가 대상 크기로 맞춘 파티클 크기에 프리팹별 보정 배율을 적용한다
    private static void ApplyParticleScaleMultiplier(GameObject visualInstance, float particleScaleMultiplier)
    {
        if (Mathf.Approximately(particleScaleMultiplier, 1.0f))
        {
            return;
        }

        OverlayFX overlayFx = visualInstance.GetComponent<OverlayFX>();
        if (overlayFx == null || overlayFx.particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < overlayFx.particleSystems.Count; i++)
        {
            ParticleSystem particleSystem = overlayFx.particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            main.startSizeMultiplier *= particleScaleMultiplier;
        }
    }

    // 단일 슬롯에 생성된 비주얼 인스턴스를 모두 제거한다
    private static void ClearSlotInstances(StatusEffectVisualSlot slot)
    {
        if (slot == null)
        {
            return;
        }

        ClearInstances(slot.runtimeInstances);
        slot.runtimeInstances = null;
        slot.lethalIndicatorInstances = null;
        slot.originalSharedMaterials = null;
    }

    // 인스턴스 배열에 생성된 비주얼 오브젝트를 모두 제거한다
    private static void ClearInstances(GameObject[] instances)
    {
        if (instances == null)
        {
            return;
        }

        for (int i = 0; i < instances.Length; i++)
        {
            GameObject visualInstance = instances[i];
            if (visualInstance == null)
            {
                continue;
            }

            Destroy(visualInstance);
            instances[i] = null;
        }
    }

    // 지정한 상태이상 타입의 RendererOverlay 슬롯 원본 머티리얼 배열을 캐시한다
    private void CacheVisualSlotOriginalMaterials(StatusEffectVisualType visualType)
    {
        if (visualSlots == null)
        {
            return;
        }

        for (int i = 0; i < visualSlots.Length; i++)
        {
            StatusEffectVisualSlot slot = visualSlots[i];
            if (slot == null || slot.visualType != visualType || slot.attachMode != StatusEffectVisualAttachMode.RendererOverlay)
            {
                continue;
            }

            CacheOriginalSharedMaterials(slot);
        }
    }

    // RendererOverlay 슬롯 대상 렌더러의 현재 머티리얼 배열을 저장한다
    private static void CacheOriginalSharedMaterials(StatusEffectVisualSlot slot)
    {
        if (slot == null || slot.targetRenderers == null)
        {
            return;
        }

        if (slot.originalSharedMaterials != null && slot.originalSharedMaterials.Length == slot.targetRenderers.Length)
        {
            return;
        }

        slot.originalSharedMaterials = new Material[slot.targetRenderers.Length][];
        for (int i = 0; i < slot.targetRenderers.Length; i++)
        {
            Renderer targetRenderer = slot.targetRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            slot.originalSharedMaterials[i] = targetRenderer.sharedMaterials;
        }
    }

    // 지정한 상태이상 타입의 RendererOverlay 슬롯 머티리얼을 원본 배열로 복구한다
    private void RestoreVisualSlotOriginalMaterials(StatusEffectVisualType visualType)
    {
        if (visualSlots == null)
        {
            return;
        }

        for (int i = 0; i < visualSlots.Length; i++)
        {
            StatusEffectVisualSlot slot = visualSlots[i];
            if (slot == null || slot.visualType != visualType || slot.attachMode != StatusEffectVisualAttachMode.RendererOverlay)
            {
                continue;
            }

            RestoreOriginalSharedMaterials(slot);
        }
    }

    // RendererOverlay 슬롯 대상 렌더러의 머티리얼 배열을 저장된 원본으로 되돌린다
    private static void RestoreOriginalSharedMaterials(StatusEffectVisualSlot slot)
    {
        if (slot == null || slot.targetRenderers == null || slot.originalSharedMaterials == null)
        {
            return;
        }

        int count = Mathf.Min(slot.targetRenderers.Length, slot.originalSharedMaterials.Length);
        for (int i = 0; i < count; i++)
        {
            Renderer targetRenderer = slot.targetRenderers[i];
            Material[] originalMaterials = slot.originalSharedMaterials[i];
            if (targetRenderer == null || originalMaterials == null)
            {
                continue;
            }

            targetRenderer.sharedMaterials = originalMaterials;
        }

        slot.originalSharedMaterials = null;
    }
}
