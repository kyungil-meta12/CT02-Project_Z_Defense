using UnityEngine;

public class PowerSavingButton : MonoBehaviour
{
    public void OnButtonClick()
    {
        DisplayManager.Inst.SetPowerSavingMode(!DisplayManager.Inst.PowerSavingState);
    }
}
