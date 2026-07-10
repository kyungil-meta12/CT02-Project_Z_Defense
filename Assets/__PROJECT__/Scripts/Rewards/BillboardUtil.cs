using UnityEngine;

public static class BillboardUtil
{
    public static void SetBillboard(RectTransform rt, Camera mainCam)
    {
        var camRot = -mainCam.transform.rotation.eulerAngles;
        rt.rotation = Quaternion.Euler(camRot);
    }

    public static void SetBillboardQuad(Transform t, Camera mainCam)
    {
        var camRot = -mainCam.transform.rotation.eulerAngles;
        t.rotation = Quaternion.Euler(camRot);
    }
}
