using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공용 경고 팝업을 메모리 풀에서 가져와 화면에 표시한다.
/// </summary>
public class WarningPopupManager : MonoBehaviour
{
    private const string RUNTIME_SYSTEMS_CONTAINER_NAME = "RuntimeSystems";

    public static WarningPopupManager Inst;

    [Header("팝업 참조")] [SerializeField] private GameObject popupPrefab;
    [SerializeField] private Transform popupRoot;

    [Header("아이콘 목록")] [SerializeField] private List<Sprite> popupIconSprites = new();

    [Header("표시 설정")] [SerializeField] private float defaultDuration = 2f;
    [SerializeField] private int initialPoolSize = 4;
    [SerializeField] private int maxVisibleCount = 3;
    [SerializeField] private float popupSpacing = 72f;

    private readonly List<WarningPopup> activePopups = new();
    private WarningPopup popupPrefabComponent;
    private bool hasLoggedMissingPrefab;
    private bool hasLoggedInvalidPrefab;
    private bool hasLoggedMissingRoot;

    // 싱글톤 참조와 팝업 풀을 초기화한다
    private void Awake()
    {
        if (Inst != null && Inst != this)
        {
            Destroy(gameObject);
            return;
        }

        Inst = this;
        EnsurePopupPrefabComponent();
        Prewarm();
    }

    // 싱글톤 참조와 활성 팝업 목록을 정리한다
    private void OnDestroy()
    {
        if (Inst == this)
        {
            Inst = null;
        }

        activePopups.Clear();
    }

    // 활성 팝업 목록에서 만료된 항목을 제거하고 배치를 갱신한다
    private void Update()
    {
        if (activePopups.Count == 0)
        {
            return;
        }

        if (RemoveInactivePopups())
        {
            ApplyPopupLayout();
        }
    }

    // 인스펙터에서 입력한 표시 값을 유효 범위로 보정한다
    private void OnValidate()
    {
        defaultDuration = Mathf.Max(0.01f, defaultDuration);
        initialPoolSize = Mathf.Max(0, initialPoolSize);
        maxVisibleCount = Mathf.Max(1, maxVisibleCount);
        popupSpacing = Mathf.Max(0f, popupSpacing);
    }

    //테스트 팝업
    [ContextMenu("테스트 팝업")]
    public void ShowTestPopup()
    {
        ShowWarning("Test PopUp");
    }

    /// <summary>
    /// 기본 아이콘 없이 경고 메시지를 표시한다.
    /// </summary>
    /// <param name="message"></param>
    public static void ShowWarning(string message)
    {
        ShowWarning(message, -1, -1f);
    }

    /// <summary>
    /// 지정한 아이콘 인덱스로 경고 메시지를 표시한다.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="iconIndex"></param>
    public static void ShowWarning(string message, int iconIndex)
    {
        ShowWarning(message, iconIndex, -1f);
    }

    /// <summary>
    /// 지정한 아이콘 인덱스와 표시 시간으로 경고 메시지를 표시한다.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="iconIndex"></param>
    /// <param name="duration"></param>
    public static void ShowWarning(string message, int iconIndex, float duration)
    {
        WarningPopupManager manager = GetOrCreateInstance();
        if (manager == null)
        {
            return;
        }

        manager.Show(message, iconIndex, duration);
    }

    // 경고 메시지를 풀에서 꺼낸 팝업에 적용해 표시한다
    private void Show(string message, int iconIndex, float duration)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!EnsurePopupPrefabComponent() || !EnsurePopupRoot())
        {
            return;
        }

        RemoveInactivePopups();
        TrimVisiblePopups();

        WarningPopup popup = GetOrCreateMemoryPool().GetInstance<WarningPopup>(popupPrefabComponent);
        if (popup == null)
        {
            return;
        }

        Transform popupTransform = popup.transform;
        popupTransform.SetParent(popupRoot, false);
        popupTransform.SetAsLastSibling();

        activePopups.Add(popup);
        ApplyPopupLayout();

        float resolvedDuration = duration > 0f ? duration : defaultDuration;
        popup.Init(message, ResolveIcon(iconIndex), resolvedDuration);
    }

    // 씬 인스턴스가 없으면 런타임 컨테이너 아래에 매니저를 생성한다
    private static WarningPopupManager GetOrCreateInstance()
    {
        if (Inst != null)
        {
            return Inst;
        }

        GameObject managerObject = new GameObject("WarningPopupManager");
        managerObject.transform.SetParent(GetOrCreateRuntimeSystemsContainer());
        Debug.LogWarning("[WarningPopupManager] 씬 인스턴스가 없어 런타임 WarningPopupManager를 생성했습니다. 프리팹 참조를 설정해야 경고 팝업이 표시됩니다.");
        return managerObject.AddComponent<WarningPopupManager>();
    }

    // 메모리 풀이 없으면 런타임 컨테이너 아래에 생성한다
    private static MemoryPool GetOrCreateMemoryPool()
    {
        if (MemoryPool.Inst != null)
        {
            return MemoryPool.Inst;
        }

        GameObject memoryPoolObject = new GameObject("MemoryPool");
        memoryPoolObject.transform.SetParent(GetOrCreateRuntimeSystemsContainer());
        return memoryPoolObject.AddComponent<MemoryPool>();
    }

    // 런타임 시스템을 묶을 컨테이너 트랜스폼을 반환한다
    private static Transform GetOrCreateRuntimeSystemsContainer()
    {
        GameObject containerObject = GameObject.Find(RUNTIME_SYSTEMS_CONTAINER_NAME);
        if (containerObject != null)
        {
            return containerObject.transform;
        }

        return new GameObject(RUNTIME_SYSTEMS_CONTAINER_NAME).transform;
    }

    // 설정된 초기 개수만큼 경고 팝업을 풀에 미리 생성한다
    private void Prewarm()
    {
        if (!EnsurePopupPrefabComponent() || initialPoolSize <= 0)
        {
            return;
        }

        GetOrCreateMemoryPool().Prewarm(popupPrefabComponent, initialPoolSize);
    }

    // 팝업 프리팹에서 WarningPopup 컴포넌트를 확보한다
    private bool EnsurePopupPrefabComponent()
    {
        if (popupPrefabComponent != null)
        {
            return true;
        }

        if (popupPrefab == null)
        {
            LogMissingPrefabOnce();
            return false;
        }

        popupPrefabComponent = popupPrefab.GetComponent<WarningPopup>();
        if (popupPrefabComponent == null)
        {
            LogInvalidPrefabOnce();
            return false;
        }

        return true;
    }

    // 팝업을 표시할 루트를 확보한다
    private bool EnsurePopupRoot()
    {
        if (popupRoot != null)
        {
            return true;
        }

        if (!hasLoggedMissingRoot)
        {
            Debug.LogWarning("[WarningPopupManager] 경고 팝업 표시 루트가 없어 팝업을 표시할 수 없습니다.", this);
            hasLoggedMissingRoot = true;
        }

        return false;
    }

    // 아이콘 인덱스에 해당하는 스프라이트를 반환한다
    private Sprite ResolveIcon(int iconIndex)
    {
        if (popupIconSprites == null || iconIndex < 0 || iconIndex >= popupIconSprites.Count)
        {
            return null;
        }

        return popupIconSprites[iconIndex];
    }

    // 비활성화된 팝업 참조를 활성 목록에서 제거한다
    private bool RemoveInactivePopups()
    {
        bool hasRemoved = false;
        for (int i = activePopups.Count - 1; i >= 0; i--)
        {
            WarningPopup popup = activePopups[i];
            if (popup == null || !popup.gameObject.activeSelf)
            {
                activePopups.RemoveAt(i);
                hasRemoved = true;
            }
        }

        return hasRemoved;
    }

    // 최대 표시 개수를 넘는 오래된 팝업을 풀로 반환한다
    private void TrimVisiblePopups()
    {
        while (activePopups.Count >= maxVisibleCount)
        {
            WarningPopup oldestPopup = activePopups[0];
            activePopups.RemoveAt(0);

            if (oldestPopup != null)
            {
                oldestPopup.ForceReturnToPool();
            }
        }
    }

    // 활성 팝업을 순서대로 배치한다
    private void ApplyPopupLayout()
    {
        for (int i = 0; i < activePopups.Count; i++)
        {
            WarningPopup popup = activePopups[i];
            if (popup == null)
            {
                continue;
            }

            RectTransform popupRect = popup.transform as RectTransform;
            if (popupRect == null)
            {
                continue;
            }

            popupRect.anchoredPosition = new Vector2(0f, -popupSpacing * i);
        }
    }

    // 프리팹 누락 로그를 한 번만 출력한다
    private void LogMissingPrefabOnce()
    {
        if (hasLoggedMissingPrefab)
        {
            return;
        }

        Debug.LogWarning("[WarningPopupManager] 경고 팝업 프리팹이 설정되지 않아 팝업을 표시할 수 없습니다.", this);
        hasLoggedMissingPrefab = true;
    }

    // 잘못된 프리팹 구성 로그를 한 번만 출력한다
    private void LogInvalidPrefabOnce()
    {
        if (hasLoggedInvalidPrefab)
        {
            return;
        }

        Debug.LogWarning("[WarningPopupManager] 경고 팝업 프리팹에 WarningPopup 컴포넌트가 없습니다.", this);
        hasLoggedInvalidPrefab = true;
    }
}
