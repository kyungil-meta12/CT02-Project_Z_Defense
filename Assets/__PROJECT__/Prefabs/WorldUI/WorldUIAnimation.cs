using UnityEngine;

public class WorldUIAnimation : MonoBehaviour
{
    [Header("화살표 왕복 거리")] public float tripScale;
    [Header("화살표 왕복 속도")] public float tripSpeed;
    private Camera cam;
    private Vector3 originLocalPos;
    private float sinValue;
    
    void Awake()
    {
        cam = Camera.main;
        transform.rotation = Quaternion.identity;
        originLocalPos = transform.localPosition;
    }

    void Update()
    {
        if(transform.parent)
        {
            var camRot = -cam.transform.rotation.eulerAngles;
            transform.rotation = Quaternion.Euler(camRot);
            sinValue += Time.deltaTime * tripSpeed;
            float movement = Mathf.Sin(sinValue) * tripScale;
            Vector3 movementOffset = new Vector3(0f, movement, 0f);
            Vector3 worldOffset = transform.TransformDirection(movementOffset);
            transform.position = transform.parent.TransformPoint(originLocalPos) + worldOffset;
        }
    }
}
