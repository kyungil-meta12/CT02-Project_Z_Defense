using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton object pool module.
/// Instances are disposed with the scene (no DontDestroyOnLoad).
/// </summary>
public class MemoryPool : MonoBehaviour
{
    public static MemoryPool Inst;
    private readonly Dictionary<PoolObject, Stack<PoolObject>> memDict = new();

    private void Awake()
    {
        if (Inst && Inst != this)
        {
            Destroy(gameObject);
            return;
        }

        Inst = this;
    }

    private void OnDestroy()
    {
        if (Inst == this)
        {
            Inst = null;
        }
    }

    /// <summary>
    /// <para>인스턴스의 Ty_ 타입의 컴포넌트를 리턴한다. 인스턴스가 없을 경우 새로 생성한다.</para>
    /// <para>인스턴스를 사용하기 위해서는 프리펩이 PoolObject를 상속하여야 한다.</para>
    /// <para>인스턴스 리턴 시 PoolObject의 OnSpawn() 가상 메서드가 호출된다.</para>
    /// <para>예: var enemy = MemoryPool.Inst.GetInstance&lt;Enemy&gt;(enemyPrefab);</para>
    /// </summary>
    /// <param name="prefab"></param>
    /// <returns></returns>
    public Ty_ GetInstance<Ty_>(PoolObject prefab) where Ty_ : Component
    {
        if (prefab == null)
        {
            Debug.LogError("[MemoryPool] GetInstance called with null prefab.");
            return null;
        }

        if (!memDict.TryGetValue(prefab, out var memStack) || memStack == null)
        {
            memStack = new Stack<PoolObject>();
            memDict[prefab] = memStack;
        }

        PoolObject instance;
        if (memStack.Count == 0)
        {
            instance = Instantiate(prefab);
            instance.SetStack(memStack);
        }
        else
        {
            instance = memStack.Pop();
            instance.gameObject.SetActive(true);
        }

        instance.OnSpawn();

        var comp = instance.GetComponent<Ty_>();
        if (comp == null)
        {
            Debug.LogError($"[MemoryPool] Instance does not contain component {typeof(Ty_).Name}. Prefab: {prefab.name}");
        }

        return comp;
    }

    /// <summary>
    /// <para>인스턴스를 생성하지만 컴포넌트를 리턴하지 않는다. 인스턴스가 없을 경우 새로 생성한다.</para>
    /// <para>프리펩이 PoolObject를 상속하여야 한다.</para>
    /// <para>인스턴스 리턴 시 PoolObject의 OnSpawn() 가상 메서드가 호출된다.</para>
    /// </summary>
    /// <param name="prefab"></param>
    public void CreateInstance(PoolObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("[MemoryPool] CreateInstance called with null prefab.");
            return;
        }

        if (!memDict.TryGetValue(prefab, out var memStack) || memStack == null)
        {
            memStack = new Stack<PoolObject>();
            memDict[prefab] = memStack;
        }

        if (memStack.Count == 0)
        {
            var newInst = Instantiate(prefab);
            newInst.SetStack(memStack);
            newInst.OnSpawn();
            return;
        }

        var retInst = memStack.Pop();
        retInst.gameObject.SetActive(true);
        retInst.OnSpawn();
    }

    /// <summary>
    /// Pre-creates inactive instances and stores them in the pool.
    /// </summary>
    public void Prewarm(PoolObject prefab, int count)
    {
        if (prefab == null)
        {
            Debug.LogError("[MemoryPool] Prewarm called with null prefab.");
            return;
        }

        if (count <= 0)
        {
            return;
        }

        if (!memDict.TryGetValue(prefab, out var memStack) || memStack == null)
        {
            memStack = new Stack<PoolObject>(count);
            memDict[prefab] = memStack;
        }

        for (int i = 0; i < count; i++)
        {
            var inst = Instantiate(prefab);
            inst.SetStack(memStack);
            inst.OnDespawn();
            inst.gameObject.SetActive(false);
            memStack.Push(inst);
        }
    }

    /// <summary>
    /// Returns current available (inactive) count.
    /// </summary>
    public int GetCount(PoolObject prefab)
    {
        if (prefab == null)
        {
            return 0;
        }

        if (!memDict.TryGetValue(prefab, out var memStack) || memStack == null)
        {
            return 0;
        }

        return memStack.Count;
    }
}
