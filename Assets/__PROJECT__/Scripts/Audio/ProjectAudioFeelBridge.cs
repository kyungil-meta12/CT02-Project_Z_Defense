using MoreMountains.Tools;
using UnityEngine;

namespace ProjectZDefense.Audio
{
    /// <summary>
    /// ProjectAudioManager의 볼륨 버스를 MoreMountains Feel MMSoundManager 트랙 볼륨에 동기화한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProjectAudioFeelBridge : MonoBehaviour
    {
        [System.Serializable]
        private struct FeelTrackVolumeMapping
        {
            [Header("볼륨 버스")]
            public ProjectAudioBus projectBus;
            public MMSoundManager.MMSoundManagerTracks feelTrack;
            [Range(0f, 2f)] public float volumeScale;
        }

        [Header("동기화 설정")]
        [SerializeField] private bool syncOnEnable = true;
        [SerializeField] private bool forceFeelMasterTrackToFullVolume = true;

        [Header("버스 매핑")]
        [SerializeField] private FeelTrackVolumeMapping[] mappings =
        {
            new FeelTrackVolumeMapping
            {
                projectBus = ProjectAudioBus.Sfx,
                feelTrack = MMSoundManager.MMSoundManagerTracks.Sfx,
                volumeScale = 1f
            },
            new FeelTrackVolumeMapping
            {
                projectBus = ProjectAudioBus.Bgm,
                feelTrack = MMSoundManager.MMSoundManagerTracks.Music,
                volumeScale = 1f
            },
            new FeelTrackVolumeMapping
            {
                projectBus = ProjectAudioBus.Ui,
                feelTrack = MMSoundManager.MMSoundManagerTracks.UI,
                volumeScale = 1f
            }
        };

        private ProjectAudioManager audioManager;
        private bool hasStarted;

        // 활성화 시 프로젝트 오디오 볼륨 변경 이벤트를 구독한다
        private void OnEnable()
        {
            Subscribe();

            if (hasStarted && syncOnEnable)
            {
                ApplyAllVolumes();
            }
        }

        // 씬의 다른 사운드 매니저가 활성화된 뒤 현재 볼륨을 한 번 적용한다
        private void Start()
        {
            hasStarted = true;

            if (syncOnEnable)
            {
                ApplyAllVolumes();
            }
        }

        // 비활성화 시 프로젝트 오디오 볼륨 변경 이벤트를 해제한다
        private void OnDisable()
        {
            Unsubscribe();
        }

        // 인스펙터 메뉴에서 Feel 트랙 볼륨을 현재 프로젝트 볼륨으로 즉시 맞춘다
        [ContextMenu("현재 프로젝트 오디오 볼륨 적용")]
        public void ApplyAllVolumes()
        {
            ProjectAudioManager manager = ResolveAudioManager();
            if (manager == null)
            {
                return;
            }

            if (forceFeelMasterTrackToFullVolume)
            {
                SetFeelTrackVolume(MMSoundManager.MMSoundManagerTracks.Master, 1f);
            }

            if (mappings == null)
            {
                return;
            }

            for (int i = 0; i < mappings.Length; i++)
            {
                ApplyMapping(manager, mappings[i]);
            }
        }

        // 프로젝트 오디오 매니저를 찾고 볼륨 변경 이벤트를 연결한다
        private void Subscribe()
        {
            audioManager = ResolveAudioManager();
            if (audioManager == null)
            {
                return;
            }

            audioManager.OnVolumeChanged -= HandleProjectAudioVolumeChanged;
            audioManager.OnVolumeChanged += HandleProjectAudioVolumeChanged;
        }

        // 프로젝트 오디오 매니저의 볼륨 변경 이벤트를 해제한다
        private void Unsubscribe()
        {
            if (audioManager == null)
            {
                return;
            }

            audioManager.OnVolumeChanged -= HandleProjectAudioVolumeChanged;
            audioManager = null;
        }

        // 프로젝트 오디오 버스 변경을 해당 Feel 트랙 볼륨에 반영한다
        private void HandleProjectAudioVolumeChanged(ProjectAudioBus changedBus, float effectiveVolume)
        {
            if (forceFeelMasterTrackToFullVolume)
            {
                SetFeelTrackVolume(MMSoundManager.MMSoundManagerTracks.Master, 1f);
            }

            if (mappings == null)
            {
                return;
            }

            for (int i = 0; i < mappings.Length; i++)
            {
                FeelTrackVolumeMapping mapping = mappings[i];
                if (mapping.projectBus != changedBus)
                {
                    continue;
                }

                SetFeelTrackVolume(mapping.feelTrack, effectiveVolume * Mathf.Max(0f, mapping.volumeScale));
            }
        }

        // 지정 매핑의 현재 프로젝트 볼륨을 Feel 트랙에 적용한다
        private void ApplyMapping(ProjectAudioManager manager, FeelTrackVolumeMapping mapping)
        {
            float effectiveVolume = manager.GetEffectiveVolume(mapping.projectBus);
            SetFeelTrackVolume(mapping.feelTrack, effectiveVolume * Mathf.Max(0f, mapping.volumeScale));
        }

        // 프로젝트 오디오 매니저를 가져오거나 생성한다
        private static ProjectAudioManager ResolveAudioManager()
        {
            return ProjectAudioManager.GetOrCreate();
        }

        // Feel 사운드 매니저 트랙 볼륨 변경 이벤트를 발생시킨다
        private static void SetFeelTrackVolume(MMSoundManager.MMSoundManagerTracks track, float volume)
        {
            float safeVolume = Mathf.Clamp01(volume);
            MMSoundManagerTrackEvent.Trigger(MMSoundManagerTrackEventTypes.SetVolumeTrack, track, safeVolume);
        }
    }
}
