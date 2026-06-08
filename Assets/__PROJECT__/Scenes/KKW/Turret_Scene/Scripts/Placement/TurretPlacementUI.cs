using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TurretPlacementUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TurretPlacementController placementController;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private TurretPlacementSlotUI slotPrefab;

    [Header("Entries")]
    [SerializeField] private TurretShopEntrySO[] shopEntries;
    [SerializeField] private bool rebuildOnStart = true;

    private readonly List<TurretPlacementSlotUI> spawnedSlots = new List<TurretPlacementSlotUI>();
    private TurretPlacementSlotUI templateSlot;

    private void Reset()
    {
        placementController = FindFirstObjectByType<TurretPlacementController>();
    }

    private void Start()
    {
        if (rebuildOnStart)
        {
            Rebuild();
        }
    }

    [ContextMenu("Rebuild Turret Placement UI")]
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

        foreach (TurretShopEntrySO shopEntry in shopEntries)
        {
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

    private void ClearSpawnedSlots()
    {
        foreach (TurretPlacementSlotUI slot in spawnedSlots)
        {
            if (slot == null)
            {
                continue;
            }

            Destroy(slot.gameObject);
        }

        spawnedSlots.Clear();
    }

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
