using UnityEngine;
using UnityEngine.UI;

namespace ProjectZDefense.Audio
{
    /// <summary>
    /// Unity UI Slider 값을 ProjectAudioManager의 마스터, SFX, BGM, UI 볼륨에 연결한다.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class ProjectAudioVolumeSlider : MonoBehaviour
    {
        [Header("볼륨 설정")]
        [SerializeField] private ProjectAudioVolumeTarget target = ProjectAudioVolumeTarget.Sfx;
        [SerializeField] private bool saveValue = true;

        private Slider slider;
        private bool isRefreshing;

        // 슬라이더 참조를 초기화한다
        private void Awake()
        {
            slider = GetComponent<Slider>();
        }

        // 활성화 시 현재 볼륨을 슬라이더에 반영하고 이벤트를 연결한다
        private void OnEnable()
        {
            RefreshSlider();

            if (slider != null)
            {
                slider.onValueChanged.AddListener(OnSliderValueChanged);
            }
        }

        // 비활성화 시 슬라이더 이벤트를 해제한다
        private void OnDisable()
        {
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(OnSliderValueChanged);
            }
        }

        // 슬라이더 변경값을 오디오 매니저에 전달한다
        private void OnSliderValueChanged(float value)
        {
            if (isRefreshing)
            {
                return;
            }

            ProjectAudioManager manager = ProjectAudioManager.GetOrCreate();
            switch (target)
            {
                case ProjectAudioVolumeTarget.Master:
                    manager.SetMasterVolume(value, saveValue);
                    break;
                case ProjectAudioVolumeTarget.Bgm:
                    manager.SetBgmVolume(value, saveValue);
                    break;
                case ProjectAudioVolumeTarget.Ui:
                    manager.SetUiVolume(value, saveValue);
                    break;
                default:
                    manager.SetSfxVolume(value, saveValue);
                    break;
            }
        }

        // 현재 오디오 볼륨 값을 슬라이더에 표시한다
        private void RefreshSlider()
        {
            if (slider == null)
            {
                slider = GetComponent<Slider>();
            }

            if (slider == null)
            {
                return;
            }

            ProjectAudioManager manager = ProjectAudioManager.GetOrCreate();
            isRefreshing = true;
            slider.SetValueWithoutNotify(GetVolume(manager));
            isRefreshing = false;
        }

        // 대상 볼륨 값을 가져온다
        private float GetVolume(ProjectAudioManager manager)
        {
            switch (target)
            {
                case ProjectAudioVolumeTarget.Master:
                    return manager.MasterVolume;
                case ProjectAudioVolumeTarget.Bgm:
                    return manager.BgmVolume;
                case ProjectAudioVolumeTarget.Ui:
                    return manager.UiVolume;
                default:
                    return manager.SfxVolume;
            }
        }
    }
}
