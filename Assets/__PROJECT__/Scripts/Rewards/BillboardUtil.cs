using UnityEngine;

public static class BillboardUtil
{
    public static void SetBillboard(RectTransform rt, Camera mainCam)
    {
        rt.rotation = mainCam.transform.rotation;
    }

    public static void SetBillboardQuad(Transform t, Camera mainCam)
    {
        var camRot = mainCam.transform.rotation.eulerAngles;
        camRot *= -1f;
        t.rotation = Quaternion.Euler(camRot);
    }
}
