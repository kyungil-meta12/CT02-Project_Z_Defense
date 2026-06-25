using DamageNumbersPro;
using UnityEngine;

/// <summary>
/// DamageNumbersPro Mesh 프리팹을 사용해 데미지 팝업을 표시하는 백엔드.
/// </summary>
public sealed class DnpDamagePopupBackend : IDamagePopupRenderBackend
{
    private const string DNP_DAMAGE_POPUP_RESOURCE_PATH = "UI/DNP_DamagePopup_RedGlow";

    private readonly DamagePopupSettings settings;
    private DamageNumberMesh fallbackPrefab;

    public bool IsAvailable => GetPrefab(DamagePopupType.Normal) != null;

    // DamageNumbersPro 백엔드에 필요한 설정 참조를 저장한다
    public DnpDamagePopupBackend(DamagePopupSettings settings)
    {
        this.settings = settings;
    }

    // DamageNumbersPro 프리팹의 자체 풀을 미리 생성한다
    public void Prewarm()
    {
        DamageNumberMesh normalPrefab = GetPrefab(DamagePopupType.Normal);
        DamageNumberMesh criticalPrefab = GetPrefab(DamagePopupType.Critical);
        DamageNumberMesh heavyPrefab = GetPrefab(DamagePopupType.Heavy);

        PrewarmPrefab(normalPrefab);
        if (criticalPrefab != normalPrefab)
        {
            PrewarmPrefab(criticalPrefab);
        }

        if (heavyPrefab != normalPrefab && heavyPrefab != criticalPrefab)
        {
            PrewarmPrefab(heavyPrefab);
        }
    }

    // DamageNumbersPro Mesh 백엔드로 데미지 팝업을 생성한다
    public bool TrySpawn(int damageValue, Vector3 position, DamagePopupType damageType, Camera targetCamera)
    {
        DamageNumberMesh prefab = GetPrefab(damageType);
        if (prefab == null)
        {
            return false;
        }

        DamageNumber popup = prefab.Spawn(position, damageValue);
        if (popup == null)
        {
            return false;
        }

        ConfigurePopup(popup, damageType, targetCamera);
        return true;
    }

    // 지정한 DamageNumbersPro 프리팹의 풀을 미리 채운다
    private static void PrewarmPrefab(DamageNumberMesh prefab)
    {
        if (prefab == null)
        {
            return;
        }

        prefab.PrewarmPool();
    }

    // 설정 프리팹이 비어 있으면 Resources 기본 DNP 프리팹을 반환한다
    private DamageNumberMesh GetPrefab(DamagePopupType damageType)
    {
        if (settings == null)
        {
            return null;
        }

        DamageNumberMesh prefab = settings.GetDnpPrefab(damageType);
        if (prefab != null)
        {
            return prefab;
        }

        if (fallbackPrefab == null)
        {
            fallbackPrefab = Resources.Load<DamageNumberMesh>(DNP_DAMAGE_POPUP_RESOURCE_PATH);
        }

        return fallbackPrefab;
    }

    // DamageNumbersPro 팝업에 프로젝트 타입별 스타일을 적용한다
    private void ConfigurePopup(DamageNumber popup, DamagePopupType damageType, Camera targetCamera)
    {
        if (targetCamera != null)
        {
            popup.cameraOverride = targetCamera.transform;
            popup.fovCamera = targetCamera;
            popup.orthographicCamera = targetCamera;
            if (targetCamera.orthographic)
            {
                popup.renderThroughWalls = false;
                popup.consistentScreenSize = false;
            }
        }

        popup.SetColor(settings.GetDamageColor(damageType));
        popup.enableLeftText = settings.DnpUseTypePrefix && damageType != DamagePopupType.Normal;
        popup.leftText = settings.GetDnpPrefix(damageType);
        float scaleMultiplier = settings.DnpScale * settings.GetScaleMultiplier(damageType);
        popup.SetScale(scaleMultiplier);
        popup.UpdateText();
    }
}
