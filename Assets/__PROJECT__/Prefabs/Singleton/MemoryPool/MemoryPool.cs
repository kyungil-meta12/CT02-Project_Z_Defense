using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 오브젝트 풀을 위한 메모리 풀 싱글톤 모듈 // 씬 전환 시 인스턴스 삭제 됨
/// </summary>
public class MemoryPool : MonoBehaviour
{
    public static MemoryPool Inst;
    private Dictionary<PoolObject, Stack<PoolObject>> memDict = new();

    void Awake()
    { 
        if(Inst && Inst != this)
        { 
            DestroyImmediate(gameObject); 
            return; 
        } 
        Inst = this; 
    }

    void OnDestroy()
    { 
        Inst = null; 
    }

    /// <summary>
    /// <para>인스턴스의 Ty_ 타입의 컴포넌트를 리턴한다. 인스턴스가 없을 경우 새로 생성한다.</para>
    /// <para>인스턴스를 사용하기 위해서는 프리펩이 PoolObject를 상속하여야 한다. </para>
    /// <para>인스턴스 리턴 시 PoolObject의 OnSpawn() 가상 메서드가 호출된다. </para>
    /// <para>예: var enemy = MemoryPool.Inst.GetInstance&lt;Enemy&gt;(enemyPrefab); </para>
    /// </summary>
    /// <param name="prefab"></param>
    /// <returns></returns>
    public Ty_ GetInstance<Ty_>(PoolObject prefab) where Ty_ : Component
    {
        if (!memDict.TryGetValue(prefab, out var memStack) || memStack == null) // 프리펩 인스턴스에 대한 스택이 없을 경우 생성(Key)
        {
            memStack = new Stack<PoolObject>();
            memDict[prefab] = memStack;
        }

        if(memStack.Count == 0) // 프리펩 인스턴스에 대한 스택이 비어있을 경우(Value)
        {
            var newInst = Instantiate(prefab);
            newInst.SetStack(memStack);
            newInst.OnSpawn();
            return newInst.GetComponent<Ty_>();
        }

        var retInst = memStack.Peek();
        memStack.Pop();
        retInst.gameObject.SetActive(true);
        retInst.OnSpawn();
        return retInst.GetComponent<Ty_>();
    }

    /// <summary>
    /// <para>인스턴스를 생성하지만 컴포넌트를 리턴하지 않는다. 인스턴스가 없을 경우 새로 생성한다.</para>
    /// <para>프리펩이 PoolObject를 상속하여야 한다. </para>
    /// <para>인스턴스 리턴 시 PoolObject의 OnSpawn() 가상 메서드가 호출된다. </para>
    /// </summary>
    /// <param name="prefab"></param>
    public void CreateInstance(PoolObject prefab)
    {
        if (!memDict.TryGetValue(prefab, out var memStack) || memStack == null) // 프리펩 인스턴스에 대한 스택이 없을 경우 생성(Key)
        {
            memStack = new Stack<PoolObject>();
            memDict[prefab] = memStack;
        }

        if (memStack.Count == 0) // 프리펩 인스턴스에 대한 스택이 비어있을 경우(Value)
        {
            var newInst = Instantiate(prefab);
            newInst.SetStack(memStack);
            newInst.OnSpawn();
        }
        else
        {
            var retInst = memStack.Peek();
            memStack.Pop();
            retInst.gameObject.SetActive(true);
            retInst.OnSpawn();
        }
    }
}
