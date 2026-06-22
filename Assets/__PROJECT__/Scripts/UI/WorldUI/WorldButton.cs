using UnityEngine;
using UnityEngine.Events;

public class WorldButton : MonoBehaviour
{
    public UnityEvent OnTouchEvent;

    public void OnTouch(RaycastHit hit)
    {
        if(hit.collider.gameObject == gameObject)
        {
            OnTouchEvent?.Invoke();
        }
    }

    void Start()
    {
        CameraTouchHandler.Inst.OnCameraTargetTouchEvent += OnTouch;
    }

    void OnDestroy()
    {
        if( CameraTouchHandler.Inst)
        {
            CameraTouchHandler.Inst.OnCameraTargetTouchEvent -= OnTouch;
        }
    }
}
