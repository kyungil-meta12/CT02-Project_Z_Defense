using UnityEngine;

public class WorldUIAnimation : MonoBehaviour
{
    [Header("화살표 왕복 거리")] public float tripScale;
    [Header("화살표 왕복 속도")] public float tripSpeed;
    private Camera cam;
    private Vector3 originPos;
    private float sinValue;
    
    void Awake()
    {
        cam = Camera.main;
        originPos = transform.localPosition;
    }

    void Update()
    {
        // 위 아래로 왕복한다.
        BillboardUtil.SetBillboardQuad(transform, cam);
        sinValue += Time.deltaTime * tripSpeed;
        transform.localPosition = originPos + new Vector3(Mathf.Sin(-sinValue) * tripScale, Mathf.Sin(sinValue) * tripScale, 0f);
    }
}
