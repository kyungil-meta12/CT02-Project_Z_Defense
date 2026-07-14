using UnityEngine;
using UnityEngine.Audio;

namespace ProjectZDefense.Audio
{
    /// <summary>
    /// 하나의 사운드 이벤트에 필요한 클립, 볼륨, 피치, 동시 재생 제한 정책을 보관한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Z Defense/Audio/Audio Cue")]
    public class AudioCueSO : ScriptableObject
    {
        private const float MAX_VOLUME_MULTIPLIER = 3f;

        [Header("기본 재생")]
        [SerializeField] private ProjectAudioBus bus = ProjectAudioBus.Sfx;
        [SerializeField] private ProjectAudioPlaybackMode playbackMode = ProjectAudioPlaybackMode.OneShot;
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private AudioMixerGroup mixerGroup;

        [Header("볼륨과 피치")]
        [SerializeField, Range(0f, MAX_VOLUME_MULTIPLIER)] private float volume = 1f;
        [SerializeField, Range(0f, MAX_VOLUME_MULTIPLIER)] private float volumeRandomRange = 0f;
        [SerializeField] private float pitch = 1f;
        [SerializeField] private float pitchRandomRange = 0f;

        [Header("3D 공간음")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField] private float minDistance = 4f;
        [SerializeField] private float maxDistance = 28f;

        [Header("중복 제한")]
        [Tooltip("Unity AudioSource 우선순위와 동일하게 0에 가까울수록 중요도가 높습니다.")]
        [SerializeField] private int priority = 128;
        [SerializeField] private float minInterval = 0f;
        [SerializeField] private int maxSimultaneous = 4;
        [SerializeField] private bool replaceOldestWhenLimited = false;

        public ProjectAudioBus Bus => bus;
        public ProjectAudioPlaybackMode PlaybackMode => playbackMode;
        public AudioMixerGroup MixerGroup => mixerGroup;
        public float SpatialBlend => spatialBlend;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
        public int Priority => priority;
        public float MinInterval => minInterval;
        public int MaxSimultaneous => maxSimultaneous;
        public bool ReplaceOldestWhenLimited => replaceOldestWhenLimited;

        // 재생 가능한 클립이 있는지 확인한다
        public bool HasClip()
        {
            return clips != null && clips.Length > 0;
        }

        // 큐에 등록된 클립 중 하나를 선택한다
        public AudioClip GetClip()
        {
            if (!HasClip())
            {
                return null;
            }

            if (clips.Length == 1)
            {
                return clips[0];
            }

            return clips[Random.Range(0, clips.Length)];
        }

        // 큐에 등록된 클립 중 가장 긴 재생 시간을 반환한다
        public float GetMaxClipLength()
        {
            if (!HasClip())
            {
                return 0f;
            }

            float maxLength = 0f;
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[i];
                if (clip == null)
                {
                    continue;
                }

                maxLength = Mathf.Max(maxLength, clip.length);
            }

            return maxLength;
        }

        // 랜덤 범위를 반영한 재생 볼륨을 계산한다
        public float GetVolume()
        {
            if (volumeRandomRange <= 0f)
            {
                return Mathf.Clamp(volume, 0f, MAX_VOLUME_MULTIPLIER);
            }

            float min = Mathf.Max(0f, volume - volumeRandomRange);
            float max = Mathf.Min(MAX_VOLUME_MULTIPLIER, volume + volumeRandomRange);
            return Random.Range(min, max);
        }

        // 랜덤 범위를 반영한 재생 피치를 계산한다
        public float GetPitch()
        {
            if (pitchRandomRange <= 0f)
            {
                return pitch;
            }

            float min = Mathf.Max(0.01f, pitch - pitchRandomRange);
            float max = Mathf.Max(min, pitch + pitchRandomRange);
            return Random.Range(min, max);
        }
    }
}
