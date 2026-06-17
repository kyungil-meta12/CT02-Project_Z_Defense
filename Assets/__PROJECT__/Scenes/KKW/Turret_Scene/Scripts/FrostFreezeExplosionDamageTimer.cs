using UnityEngine;
using ProjectZDefense.StatusEffects;

/// <summary>
/// 빙결 이펙트 재생 후 지정된 지연 시간에 폭발 데미지를 한 번 적용한다.
/// </summary>
public class FrostFreezeExplosionDamageTimer : MonoBehaviour
{
    private FrostStatusPayload payload;
    private Vector3 explosionPosition;
    private IDamageable ownerTarget;
    private IDamageable primaryTarget;
    private Transform primaryTargetTransform;
    private float remainingDelay;
    private bool damagePending;
    private bool initialized;

    // 지연 데미지 적용에 필요한 폭발 데이터와 위치를 초기화한다
    public void Init(FrostStatusPayload payload_, Vector3 explosionPosition_, IDamageable primaryTarget_, float delay, bool shouldApplyDamage)
    {
        payload = payload_;
        explosionPosition = explosionPosition_;
        ownerTarget = primaryTarget_;
        primaryTarget = primaryTarget_;
        primaryTargetTransform = ResolveTargetTransform(primaryTarget_);
        remainingDelay = Mathf.Max(0.0f, delay);
        damagePending = shouldApplyDamage;
        initialized = true;
        UpdateFollowPosition();

        if (damagePending && remainingDelay <= 0.0f)
        {
            ApplyDamageNow();
        }
    }

    // 활성화 중 대상 위치를 따라가고 지연 시간이 끝나면 폭발 데미지를 적용한다
    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        UpdateFollowPosition();
        if (!damagePending)
        {
            return;
        }

        remainingDelay -= Time.deltaTime;
        if (remainingDelay > 0.0f)
        {
            return;
        }

        ApplyDamageNow();
    }

    // 비활성화될 때 이전 폭발 예약 상태를 정리한다
    private void OnDisable()
    {
        ClearPendingDamage();
    }

    // 외부 생명주기에서 예약된 폭발 데미지를 취소한다
    public void CancelPendingDamage()
    {
        ClearPendingDamage();
    }

    // 지정한 대상이 현재 이펙트의 원 빙결 대상인지 확인한다
    public bool IsOwnedBy(IDamageable target)
    {
        return target != null && ownerTarget == target;
    }

    // 예약된 폭발 데미지를 한 번 적용하고 타이머를 종료한다
    private void ApplyDamageNow()
    {
        if (!initialized || !damagePending)
        {
            return;
        }

        damagePending = false;
        UpdateFollowPosition();
        FrostStatusEffectUtility.ApplyFreezePrimaryTargetDamage(payload, primaryTarget);
        FrostStatusEffectUtility.ApplyExplosionDamage(payload, explosionPosition, primaryTarget);
    }

    // 원 빙결 대상의 현재 몸통 위치로 이펙트와 폭발 기준 위치를 갱신한다
    private void UpdateFollowPosition()
    {
        if (primaryTarget == null || !primaryTarget.IsAlive || primaryTargetTransform == null)
        {
            return;
        }

        explosionPosition = TurretAimPointUtility.GetAimPosition(primaryTargetTransform.gameObject);
        transform.position = explosionPosition;
    }

    // 데미지 대상 인터페이스에서 런타임 Transform을 가져온다
    private Transform ResolveTargetTransform(IDamageable target)
    {
        Component targetComponent = target as Component;
        if (targetComponent == null)
        {
            return null;
        }

        return targetComponent.transform;
    }

    // 재사용 전 예약된 폭발 상태를 초기화한다
    private void ClearPendingDamage()
    {
        initialized = false;
        ownerTarget = null;
        primaryTarget = null;
        primaryTargetTransform = null;
        remainingDelay = 0.0f;
        damagePending = false;
    }
}
