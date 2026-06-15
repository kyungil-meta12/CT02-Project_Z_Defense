using UnityEngine;

public static class BillboardUtil
{
    public static void SetBillboard(ref RectTransform rt, ref Camera mainCam)
    {
        rt.rotation = mainCam.transform.rotation;
    }
}
