using System.Collections.Generic;
using UnityEngine;

public class TurretPlacementPreview
{
    private readonly List<Renderer> renderers = new List<Renderer>();
    private MaterialPropertyBlock propertyBlock;
    private GameObject previewObject;

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
        DisableGameplayComponents(previewObject);
        CacheRenderers();
        SetValid(false);
    }

    public void Hide()
    {
        if (previewObject != null)
        {
            Object.Destroy(previewObject);
        }

        previewObject = null;
        renderers.Clear();
    }

    public void SetPose(Vector3 position, Quaternion rotation)
    {
        if (previewObject == null)
        {
            return;
        }

        previewObject.transform.SetPositionAndRotation(position, rotation);
    }

    public void SetVisible(bool isVisible)
    {
        if (previewObject == null)
        {
            return;
        }

        previewObject.SetActive(isVisible);
    }

    public void SetValid(bool isValid)
    {
        Color tintColor = isValid ? new Color(0.2f, 1.0f, 0.35f, 0.45f) : new Color(1.0f, 0.15f, 0.12f, 0.45f);
        ApplyTint(tintColor);
    }

    private void CacheRenderers()
    {
        renderers.Clear();
        if (previewObject == null)
        {
            return;
        }

        previewObject.GetComponentsInChildren(true, renderers);
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
