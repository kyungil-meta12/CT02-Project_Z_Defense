using UnityEngine;

public class DropItemIndicator : MonoBehaviour
{   
    [Header("화살표 왕복 거리")] public float tripScale;
    [Header("화살표 왕복 속도")] public float tripSpeed;

    private Canvas worldCanvas;
    private RectTransform rt;
    private Camera cam;
    private Vector3 originPos;
    private float sinValue;
    
    void Awake()
    {
        worldCanvas = GetComponent<Canvas>();
        rt = worldCanvas.GetComponent<RectTransform>();
        cam = Camera.main;
        originPos = rt.localPosition;
    }

    void Update()
    {
        // 위 아래로 왕복한다.
        BillboardUtil.SetBillboard(rt, cam);
        sinValue += Time.deltaTime * tripSpeed;
        rt.localPosition = originPos + new Vector3(Mathf.Sin(-sinValue) * tripScale, Mathf.Sin(sinValue) * tripScale, 0f);
    }
}
