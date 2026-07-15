using System.Collections;
using MoreMountains.Feedbacks;
using ProjectZDefense.Audio;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class WavePopup : MonoBehaviour
{
    public UIAnimationValue animValue;
    public TextMeshProUGUI text;
    public AudioClip feedbackSound;

    private RectTransform rt;
    private Vector2 originScale;
    private float sinNum;
    private float popinDelayTime;
    private bool popOutCompleted = false;

    private AudioSource aSource;

    void Awake()
    {
        rt = text.GetComponent<RectTransform>();
        originScale = rt.localScale;
        rt.localScale = Vector2.zero;
        aSource = GetComponent<AudioSource>();
        gameObject.SetActive(false);
    }

    void Start()
    {
        aSource.volume = ProjectAudioManager.Inst.GetEffectiveVolume(ProjectAudioBus.Ui);
        ProjectAudioManager.Inst.OnVolumeChanged += OnVolumeChanged;
    }

    void OnDestroy()
    {
        if(ProjectAudioManager.Inst)
        {
            ProjectAudioManager.Inst.OnVolumeChanged -= OnVolumeChanged;
        }
    }

    public void OnVolumeChanged(ProjectAudioBus bus, float volume)
    {
        if(bus == ProjectAudioBus.Ui)
        {
            aSource.volume = volume;
        }
    }

    public void Init(int waveVal)
    {
        sinNum = 0f;
        popinDelayTime = 0f;
        popOutCompleted = false;
        rt.localScale = Vector2.zero;
        text.text = "WAVE " + waveVal.ToString();
        gameObject.SetActive(true);
        aSource.PlayOneShot(feedbackSound);
    }

    void Update()
    {
        // 팝업이 커지면서 나타난 후 일정 딜레이가 지나고 다시 작아지며 사라진다.
        if(!popOutCompleted)
        {
            sinNum += Time.deltaTime * animValue.PopOutSpeed;
            if(sinNum >= (Mathf.PI * 0.5f + Mathf.PI) * 0.5f)
            {
                popOutCompleted = true;
            }
        }
        else {
            popinDelayTime += Time.deltaTime;
            if(popinDelayTime >= animValue.PopInDelay)
            {
                sinNum -= Time.deltaTime * animValue.PopOutSpeed;
                if(sinNum <= 0f)
                {
                    gameObject.SetActive(false);
                }
            }
        }

        rt.localScale = Mathf.Sin(sinNum) * originScale;
    }
}
