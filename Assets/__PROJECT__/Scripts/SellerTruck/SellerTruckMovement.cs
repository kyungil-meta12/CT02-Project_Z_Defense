using System.Collections.Generic;
using ProjectZDefense.Audio;
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
    public GameObject survivors;
    public LayerMask groundMask;
    public GameObject[] particleObjects;
    [Header("나타나는 웨이브 단위")] public int[] appearWaveUnits; 
    [Header("테스트 모드")] public bool testMode;

    public AudioSource engineSound;
    public AudioClip doorSound;
    public AudioSource defaultAudioSource;

    private float originMoveSpeed;
    private float originLookAtSpeed;
    private WaypointsTraveler traveler;
    private bool isLeaving = false;
    private float stayTime = 0f;
    private bool isRunning = false;

    private List<ParticleSystem> particles = new();

    void Awake()
    {
        traveler = GetComponent<WaypointsTraveler>();
        originMoveSpeed = traveler.MoveSpeed;
        originLookAtSpeed = traveler.LookAtSpeed;
        survivors.SetActive(false);

        // 모든 파티클 오브젝트를 리스트에 저장
        foreach(var o in particleObjects)
        {
            var particleComp = o.GetComponentInChildren<ParticleSystem>();
            if(particleComp)
            {
                particles.Add(particleComp);
            }
        }
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

        defaultAudioSource.volume = ProjectAudioManager.Inst.GetEffectiveVolume(ProjectAudioBus.Sfx);
        engineSound.volume = ProjectAudioManager.Inst.GetEffectiveVolume(ProjectAudioBus.Sfx);;
        ProjectAudioManager.Inst.OnVolumeChanged += OnVolumeChanged;
    }

    void OnDestroy()
    {
        if(GameManager.Inst)
        {
            GameManager.Inst.OnWaveIncrease -= OnWaveIncrease;
        }
        if(ProjectAudioManager.Inst)
        {
            ProjectAudioManager.Inst.OnVolumeChanged += OnVolumeChanged;
        }
    }

    public void OnVolumeChanged(ProjectAudioBus bus, float volume)
    {
        if(bus == ProjectAudioBus.Sfx)
        {
            defaultAudioSource.volume = volume;
            engineSound.volume = volume;
        }
    }

    public void OnWaveIncrease(int wave)

    {
        if(wave >= appearWaveUnits[0]) // 특정 웨이브 단위마다 나타난다.
        {
            foreach(var unit in appearWaveUnits)
            {
                if(wave % unit == 0)
                {
                    Reset();
                    return;
                }
            }
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
                    survivors.SetActive(true);
                    survivors.transform.position = transform.position + transform.forward * 10f;

                    var childCount = survivors.transform.childCount;

                    // 땅 높이에 맞춘다
                    for(int i = 0; i < childCount; i ++)
                    {
                        var child = survivors.transform.GetChild(i);
                        var childPos = child.transform.position;
                        childPos.y = 5f;
                        child.transform.position = childPos;
                        var raycast = Physics.RaycastAll(child.transform.position, Vector3.down, 10f, groundMask);
                        if(raycast.Length > 0)
                        {
                            childPos.y = raycast[0].point.y;
                            child.transform.position = childPos;
                        }
                    }

                    // 팝업 실행
                    popup.Init();

                    defaultAudioSource.PlayOneShot(doorSound);
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
                    survivors.SetActive(false);
                    defaultAudioSource.PlayOneShot(doorSound);
                }
            }
        }

        // 도착 지점 -> 출발 지점 복귀
        // 출발 지점으로 복귀하면 다음 라운드 단위가 올 때까지 대기한다.
        else if(traveler.IsMoving)
        {
            traveler.MoveSpeed = Mathf.Lerp(traveler.MoveSpeed, originMoveSpeed, Time.deltaTime);
            traveler.LookAtSpeed = Mathf.Lerp(traveler.LookAtSpeed, originLookAtSpeed, Time.deltaTime);
            if(traveler.positionIndex == traveler.Waypoints.waypoints.Count - 1)
            {
                StopReset();
            }
        }

        // 속도에 따라 엔진 사운드의 높낮이 조절
        if(engineSound.isPlaying)
        {
            engineSound.pitch = 1f + traveler.MoveSpeed * 0.1f;
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
        engineSound.Play();
        foreach(var particle in particles)
        {
            particle.Play();
        }
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
        foreach(var particle in particles)
        {
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        engineSound.Stop();
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
