using UnityEngine;

public class BillboardCanvas : MonoBehaviour
{
    private Canvas canvas;
    private RectTransform rt;
    private Camera cam;

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        rt = canvas.GetComponent<RectTransform>();
        cam = Camera.main;
    }

    void Update()
    {
        BillboardUtil.SetBillboard(rt, cam);
    }
}
