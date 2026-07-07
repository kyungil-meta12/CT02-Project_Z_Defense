using System.Collections;
using UnityEngine;

/// <summary>
/// 활성화된 GameObject를 지정 시간 후 비활성화하는 재사용 런타임 유틸리티입니다.
/// </summary>
[DisallowMultipleComponent]
public class DisableAfterSeconds : MonoBehaviour
{
    [Header("비활성화 설정")]
    [SerializeField, Min(0.0f)] private float delay = 5.0f;
    [SerializeField] private bool useUnscaledTime;

    private Coroutine disableCoroutine;

    // 오브젝트가 활성화될 때 비활성화 예약을 시작한다
    private void OnEnable()
    {
        RestartDisableTimer();
    }

    // 오브젝트가 비활성화될 때 진행 중인 예약을 정리한다
    private void OnDisable()
    {
        StopDisableTimer();
    }

    // 인스펙터 입력값을 유효 범위로 보정한다
    private void OnValidate()
    {
        delay = Mathf.Max(0.0f, delay);
    }

    // 비활성화 타이머를 처음부터 다시 시작한다
    public void RestartDisableTimer()
    {
        StopDisableTimer();
        disableCoroutine = StartCoroutine(DisableRoutine());
    }

    // 진행 중인 비활성화 타이머를 중단한다
    public void StopDisableTimer()
    {
        if (disableCoroutine == null)
        {
            return;
        }

        StopCoroutine(disableCoroutine);
        disableCoroutine = null;
    }

    // 지정 시간 대기 후 GameObject를 비활성화한다
    private IEnumerator DisableRoutine()
    {
        if (useUnscaledTime)
        {
            yield return new WaitForSecondsRealtime(delay);
        }
        else
        {
            yield return new WaitForSeconds(delay);
        }

        disableCoroutine = null;
        gameObject.SetActive(false);
    }
}
