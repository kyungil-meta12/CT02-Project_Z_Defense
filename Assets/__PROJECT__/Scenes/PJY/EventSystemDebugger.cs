using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class EventSystemDebugger : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = Input.mousePosition;

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            if (results.Count > 0)
            {
                foreach (var result in results)
                {
                    Debug.Log($"[RaycastAll 성공] 감지된 오브젝트: {result.gameObject.name}, 레이어: {LayerMask.LayerToName(result.gameObject.layer)}");
                }
            }
            else
            {
                Debug.Log("[RaycastAll 실패] 마우스 아래에 아무것도 감지되지 않음.");
            }
        }
    }
}