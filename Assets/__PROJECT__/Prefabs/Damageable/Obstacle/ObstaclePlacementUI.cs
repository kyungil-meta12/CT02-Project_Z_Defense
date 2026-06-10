using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ObstaclePlacementUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ObstaclePlacementController placementController;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private ObstaclePlacementSlotUI slotPrefab;

    [Header("Entries")]
    [SerializeField] private ObstacleBuildEntrySO[] buildEntries;
    [SerializeField] private bool rebuildOnStart;

    private readonly List<ObstaclePlacementSlotUI> spawnedSlots = new List<ObstaclePlacementSlotUI>();
    private ObstaclePlacementSlotUI templateSlot;

    // 인스펙터에서 컴포넌트를 붙일 때 기본 컨트롤러 참조를 찾는다
    private void Reset()
    {
        placementController = FindFirstObjectByType<ObstaclePlacementController>();
    }

    // 옵션이 켜진 경우 시작 시 배치 버튼을 자동 생성한다
    private void Start()
    {
        if (rebuildOnStart)
        {
            Rebuild();
        }
    }

    [ContextMenu("Rebuild Obstacle Placement UI")]
    // 빌드 항목 목록을 기준으로 배치 버튼을 다시 생성한다
    public void Rebuild()
    {
        if (placementController == null)
        {
            placementController = FindFirstObjectByType<ObstaclePlacementController>();
        }

        ResolveTemplateSlot();
        ClearSpawnedSlots();

        if (slotContainer == null || templateSlot == null || buildEntries == null)
        {
            return;
        }

        bool isSceneTemplate = templateSlot.transform.IsChildOf(slotContainer);
        if (isSceneTemplate)
        {
            templateSlot.gameObject.SetActive(false);
        }

        for (int i = 0; i < buildEntries.Length; i++)
        {
            ObstacleBuildEntrySO buildEntry = buildEntries[i];
            if (buildEntry == null)
            {
                continue;
            }

            ObstaclePlacementSlotUI slot = Instantiate(templateSlot, slotContainer);
            slot.gameObject.SetActive(true);
            slot.Initialize(buildEntry, placementController);
            spawnedSlots.Add(slot);
        }
    }

    // 이전에 자동 생성한 배치 버튼들을 제거한다
    private void ClearSpawnedSlots()
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            ObstaclePlacementSlotUI slot = spawnedSlots[i];
            if (slot == null)
            {
                continue;
            }

            Destroy(slot.gameObject);
        }

        spawnedSlots.Clear();
    }

    // 명시 프리팹이 없으면 컨테이너 안의 템플릿 슬롯을 찾는다
    private void ResolveTemplateSlot()
    {
        templateSlot = slotPrefab;

        if (templateSlot != null || slotContainer == null)
        {
            return;
        }

        templateSlot = slotContainer.GetComponentInChildren<ObstaclePlacementSlotUI>(true);
    }
}
