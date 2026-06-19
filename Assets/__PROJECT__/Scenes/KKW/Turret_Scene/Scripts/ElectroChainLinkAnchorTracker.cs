using UnityEngine;
using ProjectZDefense.StatusEffects;

/// <summary>
/// 지연 재생되는 Electro 체인 링크 이펙트가 시작/끝 대상의 최신 위치를 짧게 따라가도록 보정한다.
/// </summary>
[DisallowMultipleComponent]
public class ElectroChainLinkAnchorTracker : MonoBehaviour
{
    private ElectroStatusPayload payload;
    private Collider startCollider;
    private Collider endCollider;
    private Transform startTransform;
    private Transform endTransform;
    private Vector3 fallbackStartPosition;
    private Vector3 fallbackEndPosition;
    private float trackDuration;
    private float elapsedTime;
    private bool isTracking;
    private ElectroChainCoreLineEffect coreLineEffect;

    // 체인 링크가 추적할 시작/끝 대상과 대체 위치를 초기화한다
    public void Init(ElectroStatusPayload payload_, Transform startTransform_, Transform endTransform_, Vector3 fallbackStartPosition_, Vector3 fallbackEndPosition_, float trackDuration_)
    {
        payload = payload_;
        startCollider = null;
        endCollider = null;
        startTransform = startTransform_;
        endTransform = endTransform_;
        fallbackStartPosition = fallbackStartPosition_;
        fallbackEndPosition = fallbackEndPosition_;
        trackDuration = Mathf.Max(0.0f, trackDuration_);
        elapsedTime = 0.0f;
        isTracking = trackDuration > 0.0f;
        CacheCoreLineEffect();
        ApplyCurrentPlacement();
    }

    // 체인 링크가 추적할 시작/끝 콜라이더와 대체 위치를 초기화한다
    public void Init(ElectroStatusPayload payload_, Collider startCollider_, Collider endCollider_, Transform startTransform_, Transform endTransform_, Vector3 fallbackStartPosition_, Vector3 fallbackEndPosition_, float trackDuration_)
    {
        payload = payload_;
        startCollider = startCollider_;
        endCollider = endCollider_;
        startTransform = startTransform_;
        endTransform = endTransform_;
        fallbackStartPosition = fallbackStartPosition_;
        fallbackEndPosition = fallbackEndPosition_;
        trackDuration = Mathf.Max(0.0f, trackDuration_);
        elapsedTime = 0.0f;
        isTracking = trackDuration > 0.0f;
        CacheCoreLineEffect();
        ApplyCurrentPlacement();
    }

    // 비활성화될 때 추적 상태와 참조를 정리한다
    private void OnDisable()
    {
        isTracking = false;
        startCollider = null;
        endCollider = null;
        startTransform = null;
        endTransform = null;
        coreLineEffect = null;
        elapsedTime = 0.0f;
    }

    // 파티클 지연 재생 구간 동안 최신 대상 위치에 맞춰 이펙트를 재배치한다
    private void LateUpdate()
    {
        if (!isTracking)
        {
            return;
        }

        elapsedTime += Time.deltaTime;
        ApplyCurrentPlacement();
        if (elapsedTime >= trackDuration)
        {
            isTracking = false;
        }
    }

    // 현재 대상 위치 또는 대체 위치로 체인 링크 배치를 갱신한다
    private void ApplyCurrentPlacement()
    {
        Vector3 startPosition = ResolveAnchorPosition(startCollider, startTransform, fallbackStartPosition);
        Vector3 endPosition = ResolveAnchorPosition(endCollider, endTransform, fallbackEndPosition);
        ElectroChainLinkEffectUtility.ApplyPlacement(gameObject, payload, startPosition, endPosition);
        if (coreLineEffect != null)
        {
            coreLineEffect.Apply(payload, startPosition, endPosition, elapsedTime);
        }
    }

    // 코어 라인 컴포넌트를 캐시한다
    private void CacheCoreLineEffect()
    {
        coreLineEffect = GetComponent<ElectroChainCoreLineEffect>();
    }

    // 대상 Transform이 유효하면 최신 위치를 반환하고 아니면 대체 위치를 반환한다
    private static Vector3 ResolveAnchorPosition(Transform anchorTransform, Vector3 fallbackPosition)
    {
        return anchorTransform != null ? anchorTransform.position : fallbackPosition;
    }

    // 대상 Collider가 유효하면 콜라이더 중심을 반환하고 아니면 Transform 또는 대체 위치를 반환한다
    private static Vector3 ResolveAnchorPosition(Collider anchorCollider, Transform anchorTransform, Vector3 fallbackPosition)
    {
        if (anchorCollider != null)
        {
            return anchorCollider.bounds.center;
        }

        return ResolveAnchorPosition(anchorTransform, fallbackPosition);
    }
}
