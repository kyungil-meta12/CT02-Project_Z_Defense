using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MemoryPool을 사용하기 위해 상속하여야하는 모듈
/// </summary>
public class PoolObject : MonoBehaviour
{
    public Stack<PoolObject> OriginStack{ get; private set; }

    /// <summary>
    /// MemoryPool에서 인스턴스 리턴 시 호출되는 메서드이므로 직접 실행할 필요가 없다.
    /// </summary>
    /// <param name="stack"></param>
    public void SetStack(Stack<PoolObject> stack)
    {
        OriginStack = stack;
    }

    /// <summary>
    /// MemoryPool에서 인스턴스 리턴 시 호출되는 가상 메서드
    /// </summary>
    public virtual void OnSpawn(){}

    /// <summary>
    /// MemoryPool로 인스턴스 반환 시 호출되는 가상 메서드
    /// </summary>
    public virtual void OnDespawn(){}

    /// <summary>
    /// 인스턴스를 다시 메모리 풀로 반환한다.
    /// 원본 스택 및 인스턴스가 PoolObject에 저장되어있으므로 그냥 이 메서드를 호출하기만 하면 된다.
    /// </summary>
    /// <param name="inst"></param>
    protected void ReturnInstance()
    {
        OnDespawn();
        gameObject.SetActive(false);
        OriginStack.Push(this);    
    }
}
