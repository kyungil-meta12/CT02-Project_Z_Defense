using System.Collections.Generic;
using CodeStage.AntiCheat.EditorCode.Processors;
using UnityEngine;
using UnityEngine.UI;
using WaypointsFree;

public class SellerTruckMovement : MonoBehaviour
{
    public int stopPointIndex;
    public float stayDuration;
    public GameObject button;
    public Image image;
    public TransactionPopup popup;
    [Header("나타나는 웨이브 단위")] public int appearWaveUnit; 
    [Header("테스트 모드")] public bool testMode;

    private float originMoveSpeed;
    private float originLookAtSpeed;
    private WaypointsTraveler traveler;
    private bool isLeaving = false;
    private float stayTime = 0f;
    private bool isRunning = false;

    void Awake()
    {
        traveler = GetComponent<WaypointsTraveler>();
        originMoveSpeed = traveler.MoveSpeed;
        originLookAtSpeed = traveler.LookAtSpeed;
    }

    void Start()
    {
        GameManager.Inst.OnWaveIncrease += OnWaveIncrease;

        if(testMode)
        {
            Reset();
        }
        else
        {
            StopReset();
        }
    }

    void OnDestroy()
    {
        if(GameManager.Inst)
        {
            GameManager.Inst.OnWaveIncrease -= OnWaveIncrease;
        }
    }

    public void OnWaveIncrease(int wave)
    {
        if(wave >= appearWaveUnit && wave % appearWaveUnit == 0) // appearWaveUnit마다 트럭이 나타난다.
        {
            Reset();
        }
    }

    void Update()
    {
        if(!isRunning)
        {
            return;
        }
        // 도착 지점에 도달하면 잠시 멈추었다가 되돌아감

        if (!isLeaving)
        {
            // 출발 지점 -> 도착 지점 이동
            if (traveler.IsMoving && traveler.positionIndex >= stopPointIndex)
            {
                traveler.MoveSpeed = Mathf.Lerp(traveler.MoveSpeed, 0f, Time.deltaTime);
                traveler.LookAtSpeed = Mathf.Lerp(traveler.LookAtSpeed, 0f, Time.deltaTime);
        
                if (traveler.MoveSpeed <= 0.1f)
                {
                    traveler.MoveSpeed = 0f;
                    traveler.LookAtSpeed = 0f;
                    traveler.Move(false);
                    button.SetActive(true);

                    // 팝업 실행
                    popup.Init();
                }
            }

            // 도착 지점 대기
            else if (!traveler.IsMoving)
            {
                stayTime -= Time.deltaTime;
                image.fillAmount = stayTime / stayDuration;
                if (stayTime <= 0f)
                {
                    image.fillAmount = 0f;
                    stayTime = stayDuration;
                    traveler.Move(true);
                    isLeaving = true;
                    button.SetActive(false);
                }
            }
        }

        // 도착 지점 -> 출발 지점 복귀
        // 출발 지점으로 복귀하면 다음 라운드 단위가 올 때까지 대기한다.
        else if(traveler.IsMoving)
        {
            traveler.MoveSpeed = Mathf.Lerp(traveler.MoveSpeed, originMoveSpeed, Time.deltaTime);
            traveler.LookAtSpeed = Mathf.Lerp(traveler.LookAtSpeed, originLookAtSpeed, Time.deltaTime);
            if(Vector3.Magnitude(transform.position - traveler.Waypoints.waypoints[traveler.Waypoints.waypoints.Count - 1].position) <= 3f)
            {
                StopReset();
            }
        }
    }

    /// <summary>
    /// 트럭을 초기화 한다.
    /// </summary>
    public void Reset()
    {
        traveler.ResetTraveler();
        traveler.Move(true);
        traveler.MoveSpeed = originMoveSpeed;
        traveler.LookAtSpeed = originLookAtSpeed;
        stayTime = stayDuration;
        button.SetActive(false);
        image.fillAmount = 0f;
        isLeaving = false;
        isRunning = true;
    }

    /// <summary>
    /// 트럭을 원래의 상태로 되돌린 후 동작을 정지한다.
    /// </summary>
    public void StopReset()
    {
        traveler.ResetTraveler();
        traveler.Move(false);
        traveler.MoveSpeed = originMoveSpeed;
        traveler.LookAtSpeed = originLookAtSpeed;
        stayTime = stayDuration;
        button.SetActive(false);
        image.fillAmount = 0f;
        isLeaving = false;
        isRunning = false;
    }

    /// <summary>
    /// 현재 남은 대기 시간 비율을 리턴한다.
    /// </summary>
    /// <returns></returns>
    public float GetNormalizedRemainTime()
    {
        return stayTime / stayDuration;
    }
    
    /// <summary>
    /// 떠나는 상태를 리턴한다.
    /// </summary>
    /// <returns></returns>
    public bool GetLeaveState()
    {
        return isLeaving;
    }
}
