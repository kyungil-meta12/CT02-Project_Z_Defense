using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class TouchBackHandler : MonoBehaviour
{
    public Action OnTouchBackAction;

    /// <summary>
    /// 핸들러 업데이트
    /// </summary>
    public void UpdateTouchBackHandler()
    {
        if(Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) {
            OnTouchBackAction?.Invoke();
        }
    }
}
