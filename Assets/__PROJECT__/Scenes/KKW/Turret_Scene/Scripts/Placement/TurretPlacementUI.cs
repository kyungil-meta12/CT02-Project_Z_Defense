using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 터렛 배치 버튼 컨테이너와 레거시 자동 생성 배치 버튼을 관리한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretPlacementUI : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private TurretPlacementController placementController;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private TurretPlacementSlotUI slotPrefab;

    [Header("자동 생성 항목")]
    [SerializeField] private TurretShopEntrySO[] shopEntries;
    [SerializeField] private bool rebuildOnStart;

    private readonly List<TurretPlacementSlotUI> spawnedSlots = new List<TurretPlacementSlotUI>();
    private TurretPlacementSlotUI templateSlot;

    // 컴포넌트를 추가할 때 기본 배치 컨트롤러 참조를 찾는다
    private void Reset()
    {
        placementController = FindFirstObjectByType<TurretPlacementController>();
    }

    // 옵션이 켜진 경우에만 레거시 자동 생성 배치 버튼을 만든다
    private void Start()
    {
        RegisterShopEntriesForSaveRestore();

        if (rebuildOnStart)
        {
            Rebuild();
        }
    }

    // 상점 항목과 터렛 진화 트리를 저장 복원 조회 목록에 등록한다
    private void RegisterShopEntriesForSaveRestore()
    {
        if (placementController == null)
        {
            placementController = FindFirstObjectByType<TurretPlacementController>();
        }

        if (shopEntries == null || GameManager.Inst == null)
        {
            return;
        }

        for (int i = 0; i < shopEntries.Length; i++)
        {
            GameManager.Inst.RegisterTurretShopEntry(shopEntries[i], placementController);
        }
    }

    [ContextMenu("Rebuild Turret Placement UI")]
    // 상점 항목 목록을 기준으로 레거시 배치 버튼을 다시 생성한다
    public void Rebuild()
    {
        if (placementController == null)
        {
            placementController = FindFirstObjectByType<TurretPlacementController>();
        }

        ResolveTemplateSlot();
        ClearSpawnedSlots();

        if (slotContainer == null || templateSlot == null || shopEntries == null)
        {
            return;
        }

        bool isSceneTemplate = templateSlot.transform.IsChildOf(slotContainer);
        if (isSceneTemplate)
        {
            templateSlot.gameObject.SetActive(false);
        }

        for (int i = 0; i < shopEntries.Length; i++)
        {
            TurretShopEntrySO shopEntry = shopEntries[i];
            if (shopEntry == null)
            {
                continue;
            }

            TurretPlacementSlotUI slot = Instantiate(templateSlot, slotContainer);
            slot.gameObject.SetActive(true);
            slot.Initialize(shopEntry, placementController);
            spawnedSlots.Add(slot);
        }
    }

    // 이전에 자동 생성한 배치 버튼들을 제거한다
    private void ClearSpawnedSlots()
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            TurretPlacementSlotUI slot = spawnedSlots[i];
            if (slot == null)
            {
                continue;
            }

            Destroy(slot.gameObject);
        }

        spawnedSlots.Clear();
    }

    // 명시 프리팹이 없으면 컨테이너 하위의 템플릿 슬롯을 찾는다
    private void ResolveTemplateSlot()
    {
        templateSlot = slotPrefab;

        if (templateSlot != null || slotContainer == null)
        {
            return;
        }

        templateSlot = slotContainer.GetComponentInChildren<TurretPlacementSlotUI>(true);
    }
}
