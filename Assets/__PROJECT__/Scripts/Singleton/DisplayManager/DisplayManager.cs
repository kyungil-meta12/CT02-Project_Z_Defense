using UnityEngine;

public class DisplayManager : MonoBehaviour
{
    public static DisplayManager Inst;

    [Header("절전 모드 활성화 시 사용할 카메라")] public Camera powerSavingCamera;

    [Header("위로 올 수록 우선순위 높음")]
    [Header("시작 시 절전 모드 상태")] public bool startPowerSavingState;
    [Header("시작 시 Vsync 상태")] public bool startVsyncState;
    [Header("시작 시 적용할 프레임 제한")] public int startFramerateLimit;

 

    public bool VsyncState{ get; private set; } = false;
    public int Framerate{ get; private set; }
    public bool PowerSavingState{ get; private set; }

    private Camera mainCamera;
    private int DeviceFramerate;
    private int latestFramerate;
    private bool latestVsyncState;

    void Awake()
    {
        if(Inst && Inst != this)
        {
            DestroyImmediate(gameObject);
            return;
        }
        mainCamera = Camera.main;
        Inst = this;
    }

    void Start()
    {
        // 기기 화면 주사율 얻기
        DeviceFramerate = (int)Screen.currentResolution.refreshRateRatio.value;
        print($"[DisplayManager] 디바이스 디스플레이 주사율: {DeviceFramerate}Hz");

        // 시작 절전 모드 상태가 true라면 메인 카메라 렌더링을 중단하고 절전 모드 카메라 렌더링을 시작한다.
        if(startPowerSavingState)
        {
            SetPowerSavingMode(true);
        }
        else
        {
            // 시작 Vsync가 true 라면 프레임 제한을 설정하지 않는다. (안드로이드에서는 Vsync가 우선권을 가지므로 Vsync가 활성화 되어있다면 프레임 제한이 동작하지 않는다.)
            SetVsync(startVsyncState);
        if(!startVsyncState)
        {
            SetFramerateLimit(startFramerateLimit);
        }
        }
       
    }

    /// <summary>
    /// Vsync 사용을 활성화/비활성화 한다. 활성화 시 프레임 제한이 해제된다.<para/>
    /// 절전 모드 상태에서는 동작하지 않는다.
    /// </summary>
    /// <param name="useFlag"></param>
    public void SetVsync(bool useFlag)
    {
        if(PowerSavingState)
        {
            return;
        }

        if(useFlag != VsyncState)
        {
            print(useFlag ? "[DisplayManager] 디바이스 Vsync 활성화 됨" : "[DisplayManager] 디바이스 Vsync 비활성화 됨");
        }

        if(useFlag)
        {
            SetFramerateLimit(0);
            QualitySettings.vSyncCount = 1;
        }
        else
        {
            QualitySettings.vSyncCount = 0;
        }

        VsyncState = useFlag;
    }

    /// <summary>
    /// 프레임 제한을 설정한다. 설정할 경우 Vsync가 비활성화 된다.<para/>
    /// 0으로 설정하면 최대 프레임으로 설정된다.<para/>
    /// 절전 모드 상태 또는 Vsync 활성화 상태에서는 동작하지 않는다.
    /// </summary>
    /// <param name="framerate"></param>
    public void SetFramerateLimit(int framerate)
    {
        if(PowerSavingState || VsyncState)
        {
            return;
        }

        if(framerate != Framerate)
        {
            print($"[DisplayManager] 디바이스 프레임 제한 {framerate}Fps 적용됨");
        }
        
        SetVsync(false);
        Application.targetFrameRate = framerate;
        Framerate = framerate;
    }
    
    /// <summary>
    /// 절전 모드를 설정한다. <para/>
    /// 활성화 시 24프레임으로 제한되고 Vsync가 비활성화 되고 절전모드 전용 카메라로 변경된다. <para/>
    /// 비활성화 시 마지막으로 설정했던 설정 값으로 복원된다.
    /// </summary>
    /// <param name="useFlag"></param>
    public void SetPowerSavingMode(bool useFlag)
    {
        if(useFlag) // 최근 설정을 저장
        {
            latestFramerate = Framerate;
            latestVsyncState = VsyncState;
            SetVsync(false);
            SetFramerateLimit(24);
        }
        else
        {
            SetVsync(latestVsyncState);
            SetFramerateLimit(latestFramerate);
        }
       
        // 절전모드 활성화 시 메인 카메라의 컬링마스크를 0으로 변경하여 아무것도 렌더링 되지 않도록 함 (메인 카메라를 참조하는 타 컴포넌트의 null exception 문제를 방지하기 위함)
        // 비활성화 시 다시 컬링마스크를  Everything으로 변경하여 모두 렌더링되도록 함
        mainCamera.cullingMask = useFlag ? 0 : -1;

        // 절전 모드 전용 카메라 활성화 또는 비활성화
        powerSavingCamera.gameObject.SetActive(useFlag);
        PowerSavingState = useFlag;
    }
}
