using UnityEngine;

/// <summary>
/// 빙결 이펙트 재생 후 지정된 지연 시간에 폭발 데미지를 한 번 적용한다.
/// </summary>
public class FrostFreezeExplosionDamageTimer : MonoBehaviour
{
    private FrostStatusPayload payload;
    private Vector3 explosionPosition;
    private float remainingDelay;
    private bool initialized;

    // 지연 데미지 적용에 필요한 폭발 데이터와 위치를 초기화한다
    public void Init(FrostStatusPayload payload_, Vector3 explosionPosition_, float delay)
    {
        payload = payload_;
        explosionPosition = explosionPosition_;
        remainingDelay = Mathf.Max(0.0f, delay);
        initialized = true;

        if (remainingDelay <= 0.0f)
        {
            ApplyDamageNow();
        }
    }

    // 활성화 중 지연 시간을 누적해 폭발 데미지를 적용한다
    private void Update()
    {
        if (!initialized)
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
        initialized = false;
        remainingDelay = 0.0f;
    }

    // 예약된 폭발 데미지를 한 번 적용하고 타이머를 종료한다
    private void ApplyDamageNow()
    {
        if (!initialized)
        {
            return;
        }

        initialized = false;
        FrostStatusEffectUtility.ApplyExplosionDamage(payload, explosionPosition);
    }
}
