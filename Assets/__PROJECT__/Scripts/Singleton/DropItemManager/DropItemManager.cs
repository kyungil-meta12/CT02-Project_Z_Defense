using System.Collections.Generic;
using UnityEngine;

public class DropItemManager : MonoBehaviour
{
    public static DropItemManager Inst;
    private HashSet<DropItem> dropHash = new();

    void Awake()
    {
        if(Inst && Inst != this)
        {
            DestroyImmediate(gameObject);
        }
        Inst = this;
    }

    void Start()
    {
        GameManager.Inst.OnWaveIncrease += OnWaveChanged;
    }

    void OnDestroy()
    {
        if(GameManager.Inst)
        {
            GameManager.Inst.OnWaveIncrease -= OnWaveChanged;
        }
        Inst = null;
    }

    // 웨이브 상승 시 드롭되어있던 모든 아이템들이 일괄 회수되고 목록을 비운다.
    public void OnWaveChanged(int val)
    {
        foreach(var i in dropHash)
        {
            i.GetItem();
        }
        dropHash.Clear();
    }

    /// <summary>
    /// 드롭된 아이템 목록에 아이템을 추가한다.
    /// </summary>
    /// <param name="item"></param>
    public void AddItem(DropItem item)
    {
        dropHash.Add(item);
    }

    /// <summary>
    /// 드롭된 아이템 목록에서 아이템을 제거한다.
    /// </summary>
    /// <param name="item"></param>
    public void RemoveItem(DropItem item)
    {
        dropHash.Remove(item);
    }
}
