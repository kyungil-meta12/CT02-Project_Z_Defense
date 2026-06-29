using UnityEngine;

/// <summary>
/// 런타임에 생성된 터렛 계층을 월드 터치 선택 대상 레이어로 보정한다.
/// </summary>
public static class TurretSelectionLayerUtility
{
    private const string SELECTABLE_LAYER_NAME = "TurretBase";

    // 터렛 루트와 모든 자식 오브젝트를 선택 가능한 레이어로 변경한다
    public static void ApplyTo(GameObject turretObject, Object logContext)
    {
        if (turretObject == null)
        {
            return;
        }

        int selectableLayer = LayerMask.NameToLayer(SELECTABLE_LAYER_NAME);
        if (selectableLayer < 0)
        {
            Debug.LogWarning("[TurretSelectionLayerUtility] TurretBase 레이어를 찾을 수 없어 터렛 선택 레이어를 보정하지 못했습니다.", logContext);
            return;
        }

        ApplyToTransform(turretObject.transform, selectableLayer);
    }

    // 지정한 Transform 하위 계층 전체에 레이어를 재귀 적용한다
    private static void ApplyToTransform(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
        {
            ApplyToTransform(root.GetChild(i), layer);
        }
    }
}
