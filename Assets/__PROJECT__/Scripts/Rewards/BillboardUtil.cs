using UnityEngine;

public static class BillboardUtil
{
    public static void SetBillboard(RectTransform rt, Camera mainCam)
    {
        rt.rotation = mainCam.transform.rotation;
    }
}
