using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class DetachedPooledChildReturner : MonoBehaviour
{
    private Coroutine returnRoutine;
    private Transform originalParent;
    private Vector3 localPosition;
    private Quaternion localRotation;
    private Vector3 localScale;

    public void ScheduleReturn(Transform originalParent_, Vector3 localPosition_, Quaternion localRotation_, Vector3 localScale_, float delay)
    {
        originalParent = originalParent_;
        localPosition = localPosition_;
        localRotation = localRotation_;
        localScale = localScale_;

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
        }

        returnRoutine = StartCoroutine(ReturnRoutine(Mathf.Max(0.0f, delay)));
    }

    public void ReturnNow()
    {
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        transform.SetParent(originalParent, false);
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        transform.localScale = localScale;
    }

    private IEnumerator ReturnRoutine(float delay)
    {
        if (delay <= 0.0f)
        {
            yield return null;
        }
        else
        {
            float elapsedTime = 0.0f;
            while (elapsedTime < delay)
            {
                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }

        returnRoutine = null;
        ReturnNow();
    }
}
