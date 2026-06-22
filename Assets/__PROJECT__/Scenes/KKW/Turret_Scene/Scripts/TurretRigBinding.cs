using ProjectZima.PolygonModularTurretsPack;
using UnityEngine;

/// <summary>
/// 터렛 프리팹의 회전 리그, 조준 기준점, 총구 기준점을 런타임 컴포넌트에 연결한다.
/// </summary>
public sealed class TurretRigBinding : MonoBehaviour
{
    private enum TurretRotationPolicy
    {
        YawOnly = 0,
        YawAndPitch = 1,
        SeparateBaseAndHead = 2
    }

    [Header("대상 컴포넌트")]
    [SerializeField] private Turret targetTurret;
    [SerializeField] private TargetFinder targetFinder;
    [SerializeField] private Gun targetGun;

    [Header("리그 참조")]
    [SerializeField] private Transform yawPivot;
    [SerializeField] private Transform aimPivot;
    [SerializeField] private Transform muzzlePivot;
    [SerializeField] private Transform[] muzzlePivots;

    [Header("자동 복구 이름")]
    [SerializeField] private string fallbackYawPivotName;
    [SerializeField] private string fallbackAimPivotName = "SM_Flamethrower_Turret_Head";
    [SerializeField] private string fallbackMuzzlePivotName = "FireNozzle";

    [Header("회전 정책")]
    [SerializeField] private TurretRotationPolicy rotationPolicy = TurretRotationPolicy.YawOnly;
    [SerializeField] private bool bindTargetFinderPivot = true;

    [Header("총구 부모 보정")]
    [SerializeField] private bool parentMuzzleToAimPivot = true;
    [SerializeField] private bool applyPrimaryMuzzleLocalPose;
    [SerializeField] private Vector3 muzzleLocalPosition = new Vector3(0.0f, 0.6f, 1.05f);
    [SerializeField] private Vector3 muzzleLocalEulerAngles;

    // 시작 시 터렛 리그 참조를 런타임 컴포넌트에 연결한다
    private void Awake()
    {
        ResolveBindings();
    }

    // 등록된 총구 pivot 개수를 반환한다
    public int GetMuzzlePivotCount()
    {
        if (muzzlePivots == null || muzzlePivots.Length == 0)
        {
            return muzzlePivot == null ? 0 : 1;
        }

        return muzzlePivots.Length;
    }

    // 지정한 인덱스의 총구 pivot을 안전하게 반환한다
    public bool TryGetMuzzlePivot(int index, out Transform result)
    {
        result = null;

        if (muzzlePivots == null || muzzlePivots.Length == 0)
        {
            if (index == 0 && muzzlePivot != null)
            {
                result = muzzlePivot;
                return true;
            }

            return false;
        }

        if (index < 0 || index >= muzzlePivots.Length || muzzlePivots[index] == null)
        {
            return false;
        }

        result = muzzlePivots[index];
        return true;
    }

    // 컴포넌트와 리그 참조를 찾은 뒤 회전, 탐색, 총구 참조를 적용한다
    private void ResolveBindings()
    {
        CacheComponents();
        ResolveRigReferences();

        if (aimPivot == null)
        {
            Debug.LogWarning($"터렛 조준 기준 오브젝트를 찾지 못했습니다: {fallbackAimPivotName}", this);
            return;
        }

        ApplyTurretBinding();
        ApplyTargetFinderBinding();
        ApplyGunBinding();
        ApplyMuzzleParenting();
    }

    // 같은 오브젝트에 붙은 대상 컴포넌트를 캐시한다
    private void CacheComponents()
    {
        if (targetTurret == null)
        {
            targetTurret = GetComponent<Turret>();
        }

        if (targetFinder == null)
        {
            targetFinder = GetComponent<TargetFinder>();
        }

        if (targetGun == null)
        {
            targetGun = GetComponent<Gun>();
        }
    }

    // 직접 참조가 비어 있을 때 이름 fallback으로 리그 Transform을 찾는다
    private void ResolveRigReferences()
    {
        if (aimPivot == null)
        {
            aimPivot = FindChildByName(transform, fallbackAimPivotName);
        }

        if (yawPivot == null)
        {
            yawPivot = string.IsNullOrEmpty(fallbackYawPivotName) ? aimPivot : FindChildByName(transform, fallbackYawPivotName);
        }

        if (muzzlePivot == null)
        {
            muzzlePivot = FindChildByName(transform, fallbackMuzzlePivotName);
        }

        if ((muzzlePivots == null || muzzlePivots.Length == 0) && muzzlePivot != null)
        {
            muzzlePivots = new[] { muzzlePivot };
        }
    }

    // 회전 정책에 맞춰 Turret의 rotator/head 참조와 회전 옵션을 적용한다
    private void ApplyTurretBinding()
    {
        if (targetTurret == null)
        {
            Debug.LogWarning("터렛 리그 바인딩에 필요한 Turret 컴포넌트를 찾지 못했습니다.", this);
            return;
        }

        Transform resolvedYawPivot = yawPivot != null ? yawPivot : aimPivot;
        targetTurret.rotatorMesh = resolvedYawPivot.gameObject;
        targetTurret.headMesh = aimPivot.gameObject;

        switch (rotationPolicy)
        {
            case TurretRotationPolicy.YawOnly:
                targetTurret.rotatorMesh = aimPivot.gameObject;
                targetTurret.headMesh = aimPivot.gameObject;
                targetTurret.canRotateXRotator = false;
                targetTurret.canRotateHeadSeparately = false;
                break;
            case TurretRotationPolicy.YawAndPitch:
                targetTurret.rotatorMesh = aimPivot.gameObject;
                targetTurret.headMesh = aimPivot.gameObject;
                targetTurret.canRotateXRotator = true;
                targetTurret.canRotateHeadSeparately = false;
                break;
            case TurretRotationPolicy.SeparateBaseAndHead:
                targetTurret.canRotateXRotator = false;
                targetTurret.canRotateHeadSeparately = true;
                break;
        }
    }

    // TargetFinder의 탐색 기준점을 조준 pivot으로 설정한다
    private void ApplyTargetFinderBinding()
    {
        if (!bindTargetFinderPivot || targetFinder == null)
        {
            return;
        }

        targetFinder.pivotObject = aimPivot.gameObject;
    }

    // Gun의 총구 오브젝트를 muzzle pivot으로 설정한다
    private void ApplyGunBinding()
    {
        Transform primaryMuzzle = GetPrimaryMuzzlePivot();
        if (targetGun == null || primaryMuzzle == null)
        {
            return;
        }

        targetGun.muzzleObject = primaryMuzzle.gameObject;
    }

    // 필요 시 총구 pivot들을 조준 pivot 하위로 이동하고 primary 총구 보정값을 적용한다
    private void ApplyMuzzleParenting()
    {
        if (!parentMuzzleToAimPivot)
        {
            return;
        }

        if (muzzlePivots != null && muzzlePivots.Length > 0)
        {
            for (int i = 0; i < muzzlePivots.Length; i++)
            {
                ParentMuzzleToAimPivot(muzzlePivots[i]);
            }
        }
        else
        {
            ParentMuzzleToAimPivot(muzzlePivot);
        }

        ApplyPrimaryMuzzlePose();
    }

    // primary 총구로 사용할 첫 번째 유효 muzzle pivot을 반환한다
    private Transform GetPrimaryMuzzlePivot()
    {
        if (muzzlePivots != null)
        {
            for (int i = 0; i < muzzlePivots.Length; i++)
            {
                if (muzzlePivots[i] != null)
                {
                    return muzzlePivots[i];
                }
            }
        }

        return muzzlePivot;
    }

    // 총구 pivot을 조준 pivot 하위로 이동하되 현재 월드 자세는 유지한다
    private void ParentMuzzleToAimPivot(Transform targetMuzzlePivot)
    {
        if (targetMuzzlePivot == null || aimPivot == null || targetMuzzlePivot.parent == aimPivot)
        {
            return;
        }

        targetMuzzlePivot.SetParent(aimPivot, true);
    }

    // 옵션이 켜져 있을 때 primary 총구의 로컬 위치와 회전을 보정한다
    private void ApplyPrimaryMuzzlePose()
    {
        if (!applyPrimaryMuzzleLocalPose)
        {
            return;
        }

        Transform primaryMuzzle = GetPrimaryMuzzlePivot();
        if (primaryMuzzle == null)
        {
            return;
        }

        primaryMuzzle.localPosition = muzzleLocalPosition;
        primaryMuzzle.localRotation = Quaternion.Euler(muzzleLocalEulerAngles);
    }

    // 자식 계층에서 지정한 이름의 Transform을 재귀적으로 찾는다
    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform result = FindChildByName(root.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
