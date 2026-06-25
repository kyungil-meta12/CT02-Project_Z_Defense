using System;
using UnityEngine;
using UnityEngine.Events;

public class WorldUIHandler : MonoBehaviour
{
    public UnityEvent OnTouchEvent;
    private Collider rayCollider;

    public void OnTouch(RaycastHit hit)
    {
        if(hit.collider == rayCollider)
        {
            OnTouchEvent?.Invoke();
        }
    }
    
    void Awake()
    {
        rayCollider = GetComponentInChildren<Collider>();
    }
    void Start()
    {
        CameraTouchHandler.Inst.OnCameraTargetTouchEvent += OnTouch;
    }
    void OnDestroy()
    {
        if(CameraTouchHandler.Inst)
        {
            CameraTouchHandler.Inst.OnCameraTargetTouchEvent -= OnTouch;
        }
    }
}
