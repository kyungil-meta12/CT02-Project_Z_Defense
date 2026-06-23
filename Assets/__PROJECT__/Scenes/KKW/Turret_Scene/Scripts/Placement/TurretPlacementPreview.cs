using System.Collections.Generic;
using UnityEngine;

public class TurretPlacementPreview
{
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Material[]> originalSharedMaterials = new List<Material[]>();
    private MaterialPropertyBlock propertyBlock;
    private GameObject previewObject;
    private Vector3 originalLocalScale = Vector3.one;

    public bool IsActive
    {
        get
        {
            return previewObject != null;
        }
    }

    // 지정한 프리팹으로 배치 프리뷰 오브젝트를 생성한다
    public void Show(GameObject prefab)
    {
        Hide();

        if (prefab == null)
        {
            return;
        }

        previewObject = Object.Instantiate(prefab);
        previewObject.name = prefab.name + " Placement Preview";
        originalLocalScale = previewObject.transform.localScale;
        DisableGameplayComponents(previewObject);
        CacheRenderers();
    }

    // 현재 배치 프리뷰 오브젝트를 제거하고 캐시를 초기화한다
    public void Hide()
    {
        if (previewObject != null)
        {
            Object.Destroy(previewObject);
        }

        previewObject = null;
        originalLocalScale = Vector3.one;
        renderers.Clear();
        originalSharedMaterials.Clear();
    }

    // 빌드 포인트 하위에 프리뷰를 붙여 실제 설치 계층 스케일을 반영한다
    public void SnapTo(Transform buildPoint, Vector3 localOffset, float scaleMultiplier)
    {
        if (previewObject == null || buildPoint == null)
        {
            return;
        }

        previewObject.transform.SetParent(buildPoint, false);
        previewObject.transform.localPosition = localOffset;
        previewObject.transform.localRotation = Quaternion.identity;
        previewObject.transform.localScale = originalLocalScale * Mathf.Max(0.01f, scaleMultiplier);
    }

    // 빌드 포인트 하위에 프리뷰를 붙이고 지정한 로컬 회전을 적용한다
    public void SnapTo(Transform buildPoint, Vector3 localOffset, Quaternion localRotation, float scaleMultiplier)
    {
        if (previewObject == null || buildPoint == null)
        {
            return;
        }

        previewObject.transform.SetParent(buildPoint, false);
        previewObject.transform.localPosition = localOffset;
        previewObject.transform.localRotation = localRotation;
        previewObject.transform.localScale = originalLocalScale * Mathf.Max(0.01f, scaleMultiplier);
    }

    // 월드 위치에 프리뷰를 배치하고 프리팹 원본 스케일만 적용한다
    public void SetPose(Vector3 position, Quaternion rotation, float scaleMultiplier)
    {
        if (previewObject == null)
        {
            return;
        }

        previewObject.transform.SetParent(null, true);
        previewObject.transform.SetPositionAndRotation(position, rotation);
        previewObject.transform.localScale = originalLocalScale * Mathf.Max(0.01f, scaleMultiplier);
    }

    // 월드 위치에 프리뷰를 배치하되 실제 설치 부모의 월드 스케일까지 반영한다
    public void SetPose(Vector3 position, Quaternion rotation, float scaleMultiplier, Vector3 referenceLossyScale)
    {
        if (previewObject == null)
        {
            return;
        }

        previewObject.transform.SetParent(null, true);
        previewObject.transform.SetPositionAndRotation(position, rotation);
        previewObject.transform.localScale = Vector3.Scale(originalLocalScale, GetSafeScale(referenceLossyScale)) * Mathf.Max(0.01f, scaleMultiplier);
    }

    // 프리뷰 오브젝트 표시 상태를 변경한다
    public void SetVisible(bool isVisible)
    {
        if (previewObject == null)
        {
            return;
        }

        previewObject.SetActive(isVisible);
    }

    // 배치 가능 여부에 따라 프리뷰 머티리얼과 색상을 적용한다
    public void SetVisualState(bool isValid, Material validMaterial, Material invalidMaterial, Color validTintColor, Color invalidTintColor)
    {
        Color tintColor = isValid ? validTintColor : invalidTintColor;
        Material previewMaterial = isValid ? validMaterial : invalidMaterial;

        if (previewMaterial != null)
        {
            ApplyMaterial(previewMaterial);
            ApplyTint(tintColor);
            return;
        }

        RestoreOriginalMaterials();
        ApplyTint(tintColor);
    }

    // 프리뷰 렌더러와 원본 머티리얼 배열을 캐시한다
    private void CacheRenderers()
    {
        renderers.Clear();
        originalSharedMaterials.Clear();

        if (previewObject == null)
        {
            return;
        }

        previewObject.GetComponentsInChildren(true, renderers);
        foreach (Renderer previewRenderer in renderers)
        {
            if (previewRenderer == null)
            {
                originalSharedMaterials.Add(new Material[0]);
                continue;
            }

            originalSharedMaterials.Add(previewRenderer.sharedMaterials);
        }
    }

    // 모든 프리뷰 렌더러에 지정한 머티리얼을 적용한다
    private void ApplyMaterial(Material previewMaterial)
    {
        foreach (Renderer previewRenderer in renderers)
        {
            if (previewRenderer == null)
            {
                continue;
            }

            Material[] materials = previewRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = previewMaterial;
            }

            previewRenderer.sharedMaterials = materials;
        }
    }

    // 프리뷰 렌더러 머티리얼을 생성 당시 값으로 복구한다
    private void RestoreOriginalMaterials()
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer previewRenderer = renderers[i];
            if (previewRenderer == null || i >= originalSharedMaterials.Count)
            {
                continue;
            }

            previewRenderer.sharedMaterials = originalSharedMaterials[i];
        }
    }

    // 프리뷰 렌더러에 색상 틴트를 적용한다
    private void ApplyTint(Color tintColor)
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        foreach (Renderer previewRenderer in renderers)
        {
            if (previewRenderer == null)
            {
                continue;
            }

            previewRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", tintColor);
            propertyBlock.SetColor("_BaseColor", tintColor);
            previewRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    // 월드 프리뷰에 적용할 참조 스케일을 안전한 양수 값으로 보정한다
    private static Vector3 GetSafeScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(scale.x)),
            Mathf.Max(0.01f, Mathf.Abs(scale.y)),
            Mathf.Max(0.01f, Mathf.Abs(scale.z)));
    }

    // 프리뷰에서 실제 게임플레이 컴포넌트와 콜라이더가 동작하지 않도록 비활성화한다
    private static void DisableGameplayComponents(GameObject targetObject)
    {
        MonoBehaviour[] behaviours = targetObject.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
            {
                continue;
            }

            behaviour.enabled = false;
        }

        Collider[] colliders = targetObject.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
    }
}
