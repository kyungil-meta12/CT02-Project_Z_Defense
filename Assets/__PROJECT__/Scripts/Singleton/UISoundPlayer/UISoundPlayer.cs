using System.Collections.Generic;
using ProjectZDefense.Audio;
using Unity.VisualScripting;
using UnityEngine;

public class UISoundPlayer : MonoBehaviour
{
    public static UISoundPlayer Inst;

    public AudioClip cellClickClip;
    public AudioClip defaultClickClip;
    public AudioClip autoExecuteClip;
    public AudioClip makeClip;
    public AudioClip decomposeClip;
    public AudioClip dealClip;
    public AudioClip popupClip;

    private AudioSource aSource;
    
    void Awake()
    {
        if(Inst && Inst != this)
        {
            DestroyImmediate(gameObject);
            return;
        }
        Inst = this;

        aSource = GetComponent<AudioSource>();
        aSource.volume = ProjectAudioManager.Inst.GetEffectiveVolume(ProjectAudioBus.Ui);
    }

    void Start()
    {
        ProjectAudioManager.Inst.OnVolumeChanged += OnVolumeChanged;
    }

    void OnDestroy()
    {
        if(ProjectAudioManager.Inst)
        {
            ProjectAudioManager.Inst.OnVolumeChanged -= OnVolumeChanged;
        }
        Inst = null;
    }

    void OnVolumeChanged(ProjectAudioBus bus, float volume)
    {
        if(bus == ProjectAudioBus.Ui)
        {
            aSource.volume = volume;
        }
    }

    public void PlayCellClick()
    {
        aSource.PlayOneShot(cellClickClip);
    }

    public void PlayDefaultClick()
    {
        aSource.PlayOneShot(defaultClickClip);
    }

    public void PlayAutoExecute()
    {
        aSource.PlayOneShot(autoExecuteClip);
    }

    public void PlayMake()
    {
        aSource.PlayOneShot(makeClip);
    }

    public void PlayDecompose()
    {
        aSource.PlayOneShot(decomposeClip);
    }

    public void playDeal()
    {
        aSource.PlayOneShot(dealClip);
    }

    public void PlayPopup()
    {
        aSource.PlayOneShot(popupClip);
    }
}
