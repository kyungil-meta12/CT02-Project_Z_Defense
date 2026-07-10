using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WaypointsFree;

public class SellerTruckMovement : MonoBehaviour
{
    public int stopPointIndex;
    public float stayDuration;
    public GameObject button;
    public Image image;

    private float originMoveSpeed;
    private float originLookAtSpeed;
    private WaypointsTraveler traveler;
    private bool isLeaving = false;
    private float stayTime = 0f;

    void Awake()
    {
        traveler = GetComponent<WaypointsTraveler>();
        originMoveSpeed = traveler.MoveSpeed;
        originLookAtSpeed = traveler.LookAtSpeed;
        button.SetActive(false);
        stayTime = stayDuration;
        image.fillAmount = 0f;
    }

    void Update()
    {
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
        else if(traveler.IsMoving)
        {
            traveler.MoveSpeed = Mathf.Lerp(traveler.MoveSpeed, originMoveSpeed, Time.deltaTime);
            traveler.LookAtSpeed = Mathf.Lerp(traveler.LookAtSpeed, originLookAtSpeed, Time.deltaTime);
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
    }
}
