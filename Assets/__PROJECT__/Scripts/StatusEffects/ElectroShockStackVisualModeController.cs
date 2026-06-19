using UnityEngine;

/// <summary>
/// Electro Shock 스택 비주얼 인스턴스의 충전 단계별 자식 오브젝트 표시를 제어한다.
/// </summary>
public sealed class ElectroShockStackVisualModeController : MonoBehaviour
{
    private GameObject[] subtleModeDisabledObjects;
    private bool isInitialized;
    private bool isChargedMode = true;

    // 약한 전하 모드에서 숨길 자식 오브젝트를 이름으로 캐시한다
    public void Initialize(string[] subtleModeDisabledChildNames)
    {
        if (isInitialized)
        {
            return;
        }

        subtleModeDisabledObjects = ResolveChildObjects(subtleModeDisabledChildNames);
        isInitialized = true;
        ApplyChargedMode(isChargedMode);
    }

    // 완전 충전 여부에 따라 반짝임 파츠를 켜거나 끈다
    public void ApplyChargedMode(bool shouldUseChargedMode)
    {
        isChargedMode = shouldUseChargedMode;
        if (!isInitialized || subtleModeDisabledObjects == null)
        {
            return;
        }

        for (int i = 0; i < subtleModeDisabledObjects.Length; i++)
        {
            GameObject targetObject = subtleModeDisabledObjects[i];
            if (targetObject == null || targetObject.activeSelf == shouldUseChargedMode)
            {
                continue;
            }

            targetObject.SetActive(shouldUseChargedMode);
        }
    }

    // 이름 목록에 해당하는 자식 오브젝트를 찾아 배열로 만든다
    private GameObject[] ResolveChildObjects(string[] childNames)
    {
        if (childNames == null || childNames.Length <= 0)
        {
            return new GameObject[0];
        }

        GameObject[] resolvedObjects = new GameObject[childNames.Length];
        for (int i = 0; i < childNames.Length; i++)
        {
            string childName = childNames[i];
            if (string.IsNullOrWhiteSpace(childName))
            {
                continue;
            }

            Transform childTransform = FindChildByName(transform, childName);
            resolvedObjects[i] = childTransform != null ? childTransform.gameObject : null;
        }

        return resolvedObjects;
    }

    // 계층 전체에서 지정한 이름의 자식 트랜스폼을 찾는다
    private static Transform FindChildByName(Transform rootTransform, string childName)
    {
        if (rootTransform == null)
        {
            return null;
        }

        for (int i = 0; i < rootTransform.childCount; i++)
        {
            Transform childTransform = rootTransform.GetChild(i);
            if (childTransform.name == childName)
            {
                return childTransform;
            }

            Transform nestedResult = FindChildByName(childTransform, childName);
            if (nestedResult != null)
            {
                return nestedResult;
            }
        }

        return null;
    }
}
