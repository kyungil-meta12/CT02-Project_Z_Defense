using UnityEngine;
using UnityEngine.UI;

public class HpUIController : MonoBehaviour
{
    private Canvas worldCanvas;
    private RectTransform rt;
    private Transform camTransform;

    void Awake()
    {
        worldCanvas = GetComponent<Canvas>();
        rt = worldCanvas.GetComponent<RectTransform>();
        camTransform = Camera.main.transform;
    }

    // 항상 카메라 정면을 바라보도록 한다
    void LateUpdate()
    {
        rt.rotation = camTransform.rotation;
    }
}
