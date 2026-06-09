using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class HelicopterPropellerAnimator : MonoBehaviour
{
    [Header("Settings")]
    [Min(0f)] [SerializeField] private float rotationSpeed = 1440f;
    [SerializeField] private Vector3 localRotationAxis = Vector3.up;
    [SerializeField] private HelicopterPropellerRotationElement[] propellerElements;
    [SerializeField] private HelicopterPropellerPrefabBinding[] prefabBindings;

    private bool warnedMissingPropeller;

    // 시작 시 연결된 프로펠러가 없으면 자동 검색한다.
    private void Awake()
    {
        AutoFindPropellersIfNeeded();
    }

    // 프로펠러 회전을 매 프레임 적용한다.
    private void Update()
    {
        RotatePropellers();
    }

    // 런타임 생성 시 프로펠러 회전 설정을 주입한다.
    public void Configure(float rotationSpeed_, Vector3 localRotationAxis_, HelicopterPropellerPrefabBinding[] prefabBindings_)
    {
        rotationSpeed = Mathf.Max(0f, rotationSpeed_);
        localRotationAxis = localRotationAxis_.sqrMagnitude > 0.0001f ? localRotationAxis_.normalized : Vector3.up;

        if (prefabBindings == prefabBindings_ && HasValidPropellerElements())
        {
            return;
        }

        prefabBindings = prefabBindings_;
        warnedMissingPropeller = false;
        ClearPropellerElements();
        AutoFindPropellersIfNeeded();
    }

    // 인스펙터 컨텍스트 메뉴에서 자식 Transform 경로를 콘솔에 출력한다.
    [ContextMenu("Log Child Transform Paths")]
    private void LogChildTransformPaths()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        StringBuilder builder = new StringBuilder(512);
        builder.AppendLine("[헬기 스킬] 프로펠러 경로 확인용 Transform 목록");

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == transform)
            {
                continue;
            }

            builder.AppendLine(BuildRelativePathFromThisRoot(child));
        }

        Debug.Log(builder.ToString(), this);
    }

    // 직접 연결된 프리팹 Transform 또는 경로 기준으로 프로펠러 Transform을 찾는다.
    private void AutoFindPropellersIfNeeded()
    {
        if (HasValidPropellerElements())
        {
            return;
        }

        if (TryBuildElementsFromPrefabBindings())
        {
            return;
        }

        if (!warnedMissingPropeller)
        {
            Debug.LogWarning("[헬기 스킬] 프로펠러 바인딩이 비어 있거나 인스턴스에서 찾을 수 없습니다. SO의 Propeller Bindings를 확인해주세요.", this);
            warnedMissingPropeller = true;
        }
    }

    // SO에서 직접 연결한 프리팹 Transform을 인스턴스 Transform으로 변환한다.
    private bool TryBuildElementsFromPrefabBindings()
    {
        if (prefabBindings == null || prefabBindings.Length == 0)
        {
            return false;
        }

        EnsurePropellerElementCapacity(prefabBindings.Length);
        int count = 0;

        for (int i = 0; i < prefabBindings.Length; i++)
        {
            HelicopterPropellerPrefabBinding binding = prefabBindings[i];
            if (binding == null || binding.PropellerPrefabTransform == null)
            {
                if (string.IsNullOrEmpty(binding?.PropellerPath))
                {
                    continue;
                }
            }

            Transform instanceTransform = FindInstanceTransform(binding);
            if (instanceTransform == null)
            {
                Debug.LogWarning($"[헬기 스킬] 연결된 프로펠러를 인스턴스에서 찾지 못했습니다: {GetBindingLabel(binding)}", this);
                continue;
            }

            propellerElements[count].Configure(instanceTransform, binding.LocalRotationAxis, binding.RotationSpeedMultiplier);
            count++;
        }

        if (count <= 0)
        {
            ClearPropellerElements();
            return false;
        }

        ClearUnusedPropellerElements(count);
        return true;
    }

    // 프로펠러 요소 캐시가 현재 재사용 가능한지 확인한다.
    private bool HasValidPropellerElements()
    {
        if (propellerElements == null || propellerElements.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < propellerElements.Length; i++)
        {
            HelicopterPropellerRotationElement element = propellerElements[i];
            if (element != null && element.Propeller != null)
            {
                return true;
            }
        }

        return false;
    }

    // 필요한 수만큼 프로펠러 요소 캐시를 준비한다.
    private void EnsurePropellerElementCapacity(int capacity)
    {
        if (propellerElements != null && propellerElements.Length >= capacity)
        {
            for (int i = 0; i < capacity; i++)
            {
                if (propellerElements[i] == null)
                {
                    propellerElements[i] = new HelicopterPropellerRotationElement();
                }
            }

            return;
        }

        HelicopterPropellerRotationElement[] newElements = new HelicopterPropellerRotationElement[capacity];
        if (propellerElements != null)
        {
            int copyCount = Mathf.Min(propellerElements.Length, newElements.Length);
            for (int i = 0; i < copyCount; i++)
            {
                newElements[i] = propellerElements[i];
            }
        }

        for (int i = 0; i < newElements.Length; i++)
        {
            if (newElements[i] == null)
            {
                newElements[i] = new HelicopterPropellerRotationElement();
            }
        }

        propellerElements = newElements;
    }

    // 사용하지 않는 프로펠러 요소를 비워 회전 대상에서 제외한다.
    private void ClearUnusedPropellerElements(int usedCount)
    {
        if (propellerElements == null)
        {
            return;
        }

        for (int i = usedCount; i < propellerElements.Length; i++)
        {
            if (propellerElements[i] != null)
            {
                propellerElements[i].Clear();
            }
        }
    }

    // 프로펠러 요소 캐시의 참조만 비운다.
    private void ClearPropellerElements()
    {
        if (propellerElements == null)
        {
            return;
        }

        for (int i = 0; i < propellerElements.Length; i++)
        {
            if (propellerElements[i] != null)
            {
                propellerElements[i].Clear();
            }
        }
    }

    // SO 바인딩 정보로 생성된 헬기 인스턴스의 프로펠러 Transform을 찾는다.
    private Transform FindInstanceTransform(HelicopterPropellerPrefabBinding binding)
    {
        if (!string.IsNullOrEmpty(binding.PropellerPath))
        {
            return transform.Find(binding.PropellerPath);
        }

        if (binding.PropellerPrefabTransform == null)
        {
            return null;
        }

        return FindInstanceTransformByPrefabTransform(binding.PropellerPrefabTransform);
    }

    // 프리팹 자식 Transform의 상대 경로로 생성된 헬기 인스턴스의 Transform을 찾는다.
    private Transform FindInstanceTransformByPrefabTransform(Transform prefabTransform)
    {
        string path = BuildRelativePathFromPrefabRoot(prefabTransform);
        if (string.IsNullOrEmpty(path))
        {
            return transform;
        }

        return transform.Find(path);
    }

    // 로그에 표시할 바인딩 식별자를 반환한다.
    private string GetBindingLabel(HelicopterPropellerPrefabBinding binding)
    {
        if (binding == null)
        {
            return "비어 있는 바인딩";
        }

        if (!string.IsNullOrEmpty(binding.PropellerPath))
        {
            return binding.PropellerPath;
        }

        return binding.PropellerPrefabTransform != null ? binding.PropellerPrefabTransform.name : "비어 있는 프로펠러";
    }

    // 프리팹 루트부터 대상 Transform까지의 상대 경로를 만든다.
    private string BuildRelativePathFromPrefabRoot(Transform targetTransform)
    {
        if (targetTransform.parent == null)
        {
            return string.Empty;
        }

        string path = targetTransform.name;
        Transform current = targetTransform.parent;

        while (current != null && current.parent != null)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }

        return path;
    }

    // 현재 컴포넌트가 붙은 루트 기준의 자식 경로를 만든다.
    private string BuildRelativePathFromThisRoot(Transform targetTransform)
    {
        string path = targetTransform.name;
        Transform current = targetTransform.parent;

        while (current != null && current != transform)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }

        return path;
    }

    // 연결된 프로펠러 Transform들을 각 Element 회전축 기준으로 회전시킨다.
    private void RotatePropellers()
    {
        if (propellerElements == null || propellerElements.Length == 0 || rotationSpeed <= 0f)
        {
            return;
        }

        for (int i = 0; i < propellerElements.Length; i++)
        {
            HelicopterPropellerRotationElement element = propellerElements[i];
            if (element == null || element.Propeller == null)
            {
                continue;
            }

            Vector3 axis = ResolveRotationAxis(element.LocalRotationAxis);
            float angle = rotationSpeed * element.RotationSpeedMultiplier * Time.deltaTime;
            element.Propeller.Rotate(axis, angle, Space.Self);
        }
    }

    // Element 축이 비어 있을 때 공통 축으로 대체한다.
    private Vector3 ResolveRotationAxis(Vector3 elementAxis)
    {
        if (elementAxis.sqrMagnitude > 0.0001f)
        {
            return elementAxis.normalized;
        }

        if (localRotationAxis.sqrMagnitude > 0.0001f)
        {
            return localRotationAxis.normalized;
        }

        return Vector3.up;
    }
}

[System.Serializable]
public class HelicopterPropellerRotationElement
{
    [SerializeField] private Transform propeller;
    [SerializeField] private Vector3 localRotationAxis = Vector3.up;
    [SerializeField] private float rotationSpeedMultiplier = 1f;

    public Transform Propeller => propeller;
    public Vector3 LocalRotationAxis => localRotationAxis;
    public float RotationSpeedMultiplier => rotationSpeedMultiplier;

    // Unity 직렬화를 위한 기본 생성자다.
    public HelicopterPropellerRotationElement()
    {
    }

    // 자동 검색된 프로펠러의 기본 회전 설정을 만든다.
    public HelicopterPropellerRotationElement(Transform propeller_, Vector3 localRotationAxis_, float rotationSpeedMultiplier_)
    {
        Configure(propeller_, localRotationAxis_, rotationSpeedMultiplier_);
    }

    // 프로펠러 회전 대상을 재사용 가능한 요소에 설정한다.
    public void Configure(Transform propeller_, Vector3 localRotationAxis_, float rotationSpeedMultiplier_)
    {
        propeller = propeller_;
        localRotationAxis = localRotationAxis_;
        rotationSpeedMultiplier = rotationSpeedMultiplier_;
    }

    // 풀 재사용 또는 바인딩 재구성 시 이전 Transform 참조를 비운다.
    public void Clear()
    {
        propeller = null;
        localRotationAxis = Vector3.up;
        rotationSpeedMultiplier = 1f;
    }
}
