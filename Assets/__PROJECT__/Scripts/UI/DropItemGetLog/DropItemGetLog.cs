using System.Collections.Generic;
using IncrementalLib;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DropItemGetLog : MonoBehaviour
{
    public PoolObject textPrefab;
    public bool testMode;

    void Start()
    {
        InventorySystem.Inst.OnItemCountChange += OnGetItem;
    }

    void OnDestroy()
    {
        if(InventorySystem.Inst)
        {
            InventorySystem.Inst.OnItemCountChange -= OnGetItem;
        }
    }

    void Update()
    {
        if(testMode)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                var randomType = (RewardCurrencyType)Random.Range(0, InventorySystem.Inst.Types.Length);
                var randomCount = Random.Range(1, 100);
                InventorySystem.Inst.AddItem(randomType, randomCount);
            }
        }
    }

    /// <summary>
    /// 아이템 획득 시 vertical layout에 얻은 아이템들을 표시한다.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="diff"></param>
    public void OnGetItem(ItemData data, Incremental diff)
    {
        if(diff <= 0)
        {
            return;
        }

         // 현재 Vertical Layout Group의 자식이 10개 이상일 경우 가장 오래된 텍스트를 풀로 반환
        if (transform.childCount >= 10)
        {
            var oldest = transform.GetChild(0).GetComponent<PoolObject>();
            if (oldest != null)
            {
                oldest.ReturnToPool();
            }
        }
        var text = MemoryPool.Inst.GetInstance<TextMeshProUGUI>(textPrefab);
        text.transform.SetParent(gameObject.transform);
        text.transform.SetAsLastSibling();
        text.text = $"+ {data.Name} x {diff.ToString()}";
    }
}
