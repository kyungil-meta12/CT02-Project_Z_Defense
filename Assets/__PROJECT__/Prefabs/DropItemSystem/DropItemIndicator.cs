using UnityEngine;
using UnityEngine.UI;

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
        // 항상 카메라를 바라본다
        BillboardUtil.SetBillboard(ref rt, ref cam);

        // 위 아래로 왕복한다.
        sinValue += Time.deltaTime * tripSpeed;
        rt.localPosition = new Vector3(originPos.x, originPos.y + Mathf.Sin(sinValue) * tripScale, originPos.z);
    }
}
