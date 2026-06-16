using UnityEngine;

/// <summary>
/// 좀비 상태이상에 따라 대상 렌더러 기반 비주얼 이펙트를 켜고 끄는 공통 컨트롤러다.
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
        }

        SetFrostSlowInstancesActive(isActive);
        frostSlowVisualActive = isActive;

        if (!isActive)
        {
            StripRuntimeOverlayMaterials();
        }
    }

    // 포이즌 상태에 맞춰 독 메시 이펙트를 활성화하거나 비활성화한다
    public void SetPoisonActive(bool isActive)
    {
        if (poisonVisualActive == isActive)
        {
            return;
        }

        if (isActive)
        {
            EnsurePoisonVisualInstances();
        }

        SetPoisonInstancesActive(isActive);
        poisonVisualActive = isActive;

        if (!isActive)
        {
            StripRuntimeOverlayMaterials(poisonTargetRenderers, GetPoisonOverlayMaterialName());
        }
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

            GameObject visualInstance = Instantiate(frostSlowVisualPrefab, transform);
            visualInstance.name = frostSlowVisualPrefab.name + "_" + targetRenderer.name;
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;
            visualInstance.transform.localScale = Vector3.one;
            ConfigureOverlayFx(visualInstance, targetRenderer);
            ApplyFrostParticleScaleMultiplier(visualInstance);
            visualInstance.SetActive(false);
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

            GameObject visualInstance = Instantiate(poisonVisualPrefab, transform);
            visualInstance.name = poisonVisualPrefab.name + "_" + targetRenderer.name;
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;
            visualInstance.transform.localScale = Vector3.one;
            ConfigureOverlayFx(visualInstance, targetRenderer);
            ApplyParticleScaleMultiplier(visualInstance, poisonParticleScaleMultiplier);
            visualInstance.SetActive(false);
            poisonVisualInstances[i] = visualInstance;
        }
    }

    // MeshFX 프리팹의 OverlayFX 대상 렌더러를 런타임에 연결한다
    private static void ConfigureOverlayFx(GameObject visualInstance, Renderer targetRenderer)
    {
        OverlayFX overlayFx = visualInstance.GetComponent<OverlayFX>();
        if (overlayFx == null)
        {
            Debug.LogWarning("[StatusEffectVisualController] 프로스트 비주얼 프리팹에 OverlayFX가 없습니다. MeshFX_Frozen 프리팹 연결을 확인해주세요.", visualInstance);
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
        if (frostSlowVisualInstances == null)
        {
            return;
        }

        for (int i = 0; i < frostSlowVisualInstances.Length; i++)
        {
            GameObject visualInstance = frostSlowVisualInstances[i];
            if (visualInstance == null)
            {
                continue;
            }

            visualInstance.SetActive(isActive);
        }
    }

    // 준비된 포이즌 비주얼 인스턴스들을 활성화하거나 비활성화한다
    private void SetPoisonInstancesActive(bool isActive)
    {
        if (poisonVisualInstances == null)
        {
            return;
        }

        for (int i = 0; i < poisonVisualInstances.Length; i++)
        {
            GameObject visualInstance = poisonVisualInstances[i];
            if (visualInstance == null)
            {
                continue;
            }

            visualInstance.SetActive(isActive);
        }
    }

    // OverlayFX가 대상 크기로 맞춘 파티클 크기에 프리팹별 보정 배율을 적용한다
    private void ApplyFrostParticleScaleMultiplier(GameObject visualInstance)
    {
        ApplyParticleScaleMultiplier(visualInstance, frostParticleScaleMultiplier);
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
        if (frostSlowVisualInstances == null)
        {
            return;
        }

        for (int i = 0; i < frostSlowVisualInstances.Length; i++)
        {
            GameObject visualInstance = frostSlowVisualInstances[i];
            if (visualInstance == null)
            {
                continue;
            }

            Destroy(visualInstance);
            frostSlowVisualInstances[i] = null;
        }
    }

    // 생성된 포이즌 비주얼 인스턴스를 모두 제거한다
    private void ClearPoisonVisualInstances()
    {
        if (poisonVisualInstances == null)
        {
            return;
        }

        for (int i = 0; i < poisonVisualInstances.Length; i++)
        {
            GameObject visualInstance = poisonVisualInstances[i];
            if (visualInstance == null)
            {
                continue;
            }

            Destroy(visualInstance);
            poisonVisualInstances[i] = null;
        }
    }

    // OverlayFX가 주입한 런타임 머티리얼 슬롯을 대상 렌더러에서 제거한다
    private void StripRuntimeOverlayMaterials()
    {
        StripRuntimeOverlayMaterials(frostTargetRenderers, GetFrostOverlayMaterialName());
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
        OverlayFX overlayFx = frostSlowVisualPrefab == null ? null : frostSlowVisualPrefab.GetComponent<OverlayFX>();
        if (overlayFx == null || overlayFx.overlayMaterial == null)
        {
            return string.Empty;
        }

        return overlayFx.overlayMaterial.name;
    }

    // 포이즌 비주얼 프리팹에서 제거할 OverlayFX 머티리얼 이름을 얻는다
    private string GetPoisonOverlayMaterialName()
    {
        OverlayFX overlayFx = poisonVisualPrefab == null ? null : poisonVisualPrefab.GetComponent<OverlayFX>();
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
