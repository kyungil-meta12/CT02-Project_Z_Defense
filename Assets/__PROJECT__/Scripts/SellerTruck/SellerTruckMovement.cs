using System.Collections.Generic;
using UnityEngine;
using WaypointsFree;

public class SellerTruckMovement : MonoBehaviour
{
    public int stopPointIndex;

    private float originMoveSpeed;
    private float originLookAtSpeed;
    private WaypointsTraveler traveler;
    private bool isLeaving = false;
    private float leaveTime = 0f;

    void Awake()
    {
        traveler = GetComponent<WaypointsTraveler>();
        originMoveSpeed = traveler.MoveSpeed;
        originLookAtSpeed = traveler.LookAtSpeed;
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
                }
            }

            // 도착 지점 대기
            else if (!traveler.IsMoving)
            {
                leaveTime += Time.deltaTime;
                if (leaveTime >= 5f)
                {
                    leaveTime = 0f;
                    traveler.Move(true);
                    isLeaving = true;
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
}
