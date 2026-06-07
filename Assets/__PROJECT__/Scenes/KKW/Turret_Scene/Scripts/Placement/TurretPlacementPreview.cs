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

    public void SetVisible(bool isVisible)
    {
        if (previewObject == null)
        {
            return;
        }

        previewObject.SetActive(isVisible);
    }

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
