using UnityEngine;

public enum StatusEffectVisualType
{
    FrostSlow,
    Poison
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

    [Header("처치 예고 표시")]
    public string lethalIndicatorChildName;
    public Vector3 lethalIndicatorLocalPositionOffset;

    [System.NonSerialized] public GameObject[] runtimeInstances;
    [System.NonSerialized] public GameObject[] lethalIndicatorInstances;
}

/// <summary>
/// 좀비 상태이상에 따라 대상 렌더러 또는 지정 앵커 기반 비주얼 이펙트를 켜고 끄는 공통 컨트롤러다.
/// </summary>
public class StatusEffectVisualController : MonoBehaviour
{
    [Header("프로스트 슬로우 비주얼")]
    [SerializeField] private GameObject frostSlowVisualPrefab;
    [SerializeField] private Renderer[] frostTargetRenderers;
    [SerializeField, Min(0.01f)] private float frostParticleScaleMultiplier = 1.0f;

    [Header("포이즌 비주얼")]
    [SerializeField] private GameObject poisonVisualPrefab;
    [SerializeField] private Renderer[] poisonTargetRenderers;
    [SerializeField, Min(0.01f)] private float poisonParticleScaleMultiplier = 1.0f;

    [Header("상태이상 비주얼 슬롯")]
    [SerializeField] private StatusEffectVisualSlot[] visualSlots;

    private GameObject[] frostSlowVisualInstances;
    private bool frostSlowVisualActive;
    private GameObject[] poisonVisualInstances;
    private bool poisonVisualActive;

    // 컴포넌트가 비활성화될 때 상태이상 비주얼과 임시 머티리얼을 정리한다
    private void OnDisable()
    {
        SetFrostSlowActive(false);
        SetPoisonActive(false);
    }

    // 오브젝트가 파괴될 때 상태이상 비주얼과 임시 머티리얼을 정리한다
    private void OnDestroy()
    {
        SetFrostSlowActive(false);
        SetPoisonActive(false);
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
            EnsureFrostSlowVisualInstances();
            EnsureVisualSlotInstances(StatusEffectVisualType.FrostSlow);
        }

        SetFrostSlowInstancesActive(isActive);
        SetVisualSlotInstancesActive(StatusEffectVisualType.FrostSlow, isActive);
        frostSlowVisualActive = isActive;

        if (!isActive)
        {
            StripRuntimeOverlayMaterials();
            StripVisualSlotOverlayMaterials(StatusEffectVisualType.FrostSlow);
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
            EnsurePoisonVisualInstances();
            EnsureVisualSlotInstances(StatusEffectVisualType.Poison);
        }

        SetPoisonInstancesActive(isActive);
        SetVisualSlotInstancesActive(StatusEffectVisualType.Poison, isActive);
        poisonVisualActive = isActive;

        if (!isActive)
        {
            SetPoisonLethalIndicatorActive(false);
            StripRuntimeOverlayMaterials(poisonTargetRenderers, GetPoisonOverlayMaterialName());
            StripVisualSlotOverlayMaterials(StatusEffectVisualType.Poison);
        }
    }

    // 포이즌 틱데미지로 사망이 확정된 대상의 표시 아이콘을 켜거나 끈다
    public void SetPoisonLethalIndicatorActive(bool isActive)
    {
        SetLethalIndicatorActive(StatusEffectVisualType.Poison, isActive);
    }

    // 연결된 대상 렌더러 수에 맞춰 프로스트 비주얼 인스턴스를 준비한다
    private void EnsureFrostSlowVisualInstances()
    {
        if (frostSlowVisualPrefab == null || frostTargetRenderers == null)
        {
            return;
        }

        if (frostSlowVisualInstances == null || frostSlowVisualInstances.Length != frostTargetRenderers.Length)
        {
            ClearFrostSlowVisualInstances();
            frostSlowVisualInstances = new GameObject[frostTargetRenderers.Length];
        }

        for (int i = 0; i < frostTargetRenderers.Length; i++)
        {
            Renderer targetRenderer = frostTargetRenderers[i];
            if (targetRenderer == null || frostSlowVisualInstances[i] != null)
            {
                continue;
            }

            GameObject visualInstance = CreateOverlayVisualInstance(frostSlowVisualPrefab, targetRenderer, transform, frostParticleScaleMultiplier);
            frostSlowVisualInstances[i] = visualInstance;
        }
    }

    // 연결된 대상 렌더러 수에 맞춰 포이즌 비주얼 인스턴스를 준비한다
    private void EnsurePoisonVisualInstances()
    {
        if (poisonVisualPrefab == null || poisonTargetRenderers == null)
        {
            return;
        }

        if (poisonVisualInstances == null || poisonVisualInstances.Length != poisonTargetRenderers.Length)
        {
            ClearPoisonVisualInstances();
            poisonVisualInstances = new GameObject[poisonTargetRenderers.Length];
        }

        for (int i = 0; i < poisonTargetRenderers.Length; i++)
        {
            Renderer targetRenderer = poisonTargetRenderers[i];
            if (targetRenderer == null || poisonVisualInstances[i] != null)
            {
                continue;
            }

            GameObject visualInstance = CreateOverlayVisualInstance(poisonVisualPrefab, targetRenderer, transform, poisonParticleScaleMultiplier);
            poisonVisualInstances[i] = visualInstance;
        }
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

        overlayFx.targetRenderer = targetRenderer;
        ConfigureParticleShapes(overlayFx, targetRenderer);
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

    // 준비된 프로스트 비주얼 인스턴스들을 활성화하거나 비활성화한다
    private void SetFrostSlowInstancesActive(bool isActive)
    {
        SetInstancesActive(frostSlowVisualInstances, isActive, true);
    }

    // 준비된 포이즌 비주얼 인스턴스들을 활성화하거나 비활성화한다
    private void SetPoisonInstancesActive(bool isActive)
    {
        SetInstancesActive(poisonVisualInstances, isActive, true);
    }

    // 지정한 상태이상 타입에 해당하는 확장 슬롯 인스턴스를 활성화하거나 비활성화한다
    private void SetVisualSlotInstancesActive(StatusEffectVisualType visualType, bool isActive)
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

            SetInstancesActive(slot.runtimeInstances, isActive, slot.restartParticlesOnEnable);
        }
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
    private static void SetInstancesActive(GameObject[] instances, bool isActive, bool restartParticlesOnEnable)
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

            visualInstance.SetActive(isActive);
            if (isActive && restartParticlesOnEnable)
            {
                RestartParticles(visualInstance);
            }
        }
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

    // 생성된 프로스트 비주얼 인스턴스를 모두 제거한다
    private void ClearFrostSlowVisualInstances()
    {
        ClearInstances(frostSlowVisualInstances);
        frostSlowVisualInstances = null;
    }

    // 생성된 포이즌 비주얼 인스턴스를 모두 제거한다
    private void ClearPoisonVisualInstances()
    {
        ClearInstances(poisonVisualInstances);
        poisonVisualInstances = null;
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

    // OverlayFX가 주입한 런타임 머티리얼 슬롯을 대상 렌더러에서 제거한다
    private void StripRuntimeOverlayMaterials()
    {
        StripRuntimeOverlayMaterials(frostTargetRenderers, GetFrostOverlayMaterialName());
    }

    // 지정한 상태이상 타입에 해당하는 확장 슬롯 Overlay 머티리얼을 제거한다
    private void StripVisualSlotOverlayMaterials(StatusEffectVisualType visualType)
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

            StripRuntimeOverlayMaterials(slot.targetRenderers, GetOverlayMaterialName(slot.visualPrefab));
        }
    }

    // 지정한 렌더러 배열에서 지정한 OverlayFX 런타임 머티리얼 슬롯을 제거한다
    private static void StripRuntimeOverlayMaterials(Renderer[] targetRenderers, string overlayMaterialName)
    {
        if (targetRenderers == null)
        {
            return;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            StripRuntimeOverlayMaterial(targetRenderer, overlayMaterialName);
        }
    }

    // 프로스트 비주얼 프리팹에서 제거할 OverlayFX 머티리얼 이름을 얻는다
    private string GetFrostOverlayMaterialName()
    {
        return GetOverlayMaterialName(frostSlowVisualPrefab);
    }

    // 포이즌 비주얼 프리팹에서 제거할 OverlayFX 머티리얼 이름을 얻는다
    private string GetPoisonOverlayMaterialName()
    {
        return GetOverlayMaterialName(poisonVisualPrefab);
    }

    // 지정한 OverlayFX 프리팹에서 제거할 Overlay 머티리얼 이름을 얻는다
    private static string GetOverlayMaterialName(GameObject visualPrefab)
    {
        OverlayFX overlayFx = visualPrefab == null ? null : visualPrefab.GetComponent<OverlayFX>();
        if (overlayFx == null || overlayFx.overlayMaterial == null)
        {
            return string.Empty;
        }

        return overlayFx.overlayMaterial.name;
    }

    // 단일 렌더러에서 OverlayFX 런타임 머티리얼을 제거한다
    private static void StripRuntimeOverlayMaterial(Renderer targetRenderer, string overlayMaterialName)
    {
        Material[] materials = targetRenderer.sharedMaterials;
        bool changed = false;

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null || !IsRuntimeOverlayMaterial(material, overlayMaterialName))
            {
                continue;
            }

            materials[i] = null;
            changed = true;
        }

        if (changed)
        {
            targetRenderer.sharedMaterials = materials;
        }
    }

    // 지정한 머티리얼이 이 컨트롤러가 추가한 런타임 OverlayFX 머티리얼인지 확인한다
    private static bool IsRuntimeOverlayMaterial(Material material, string overlayMaterialName)
    {
        if (string.IsNullOrEmpty(overlayMaterialName))
        {
            return material.name.EndsWith("(Runtime)");
        }

        return material.name.StartsWith(overlayMaterialName) && material.name.EndsWith("(Runtime)");
    }
}
