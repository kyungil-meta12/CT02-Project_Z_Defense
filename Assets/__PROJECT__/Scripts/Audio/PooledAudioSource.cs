using UnityEngine;

namespace ProjectZDefense.Audio
{
    /// <summary>
    /// ProjectAudioManager가 재사용하는 AudioSource 래퍼로 재생 완료와 반환 처리를 담당한다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PooledAudioSource : MonoBehaviour
    {
        private const float MAX_EFFECTIVE_VOLUME = 3f;

        private ProjectAudioManager owner;
        private AudioSource audioSource;
        private Transform followTarget;
        private AudioCueSO cue;
        private ProjectAudioBus bus;
        private float baseVolume;
        private float runtimeVolumeScale = 1f;
        private float outputGain = 1f;
        private bool returnWhenFinished;
        private bool isReturning;
        private bool isFading;
        private bool stopAfterFade;
        private float fadeStartVolumeScale;
        private float fadeTargetVolumeScale;
        private float fadeDuration;
        private float fadeElapsed;
        private int version;

        public AudioCueSO Cue => cue;
        public ProjectAudioBus Bus => bus;
        public int Priority => audioSource != null ? audioSource.priority : 128;
        public int Version => version;
        public bool IsPlaying => audioSource != null && audioSource.isPlaying && !isReturning;

        // 컴포넌트 참조를 초기화한다
        private void Awake()
        {
            CacheAudioSource();
        }

        // 추적 대상 위치와 재생 완료 상태를 갱신한다
        private void Update()
        {
            UpdateFade(Time.unscaledDeltaTime);

            if (followTarget != null)
            {
                transform.position = followTarget.position;
            }

            if (returnWhenFinished && audioSource != null && !audioSource.loop && !audioSource.isPlaying)
            {
                StopAndReturn();
            }
        }

        // 풀 소유자와 AudioSource 참조를 설정한다
        public void Initialize(ProjectAudioManager owner_)
        {
            owner = owner_;
            CacheAudioSource();
            ResetSource();
        }

        // 큐 설정을 반영해 사운드를 재생한다
        public ProjectAudioHandle Play(AudioCueSO cue_, AudioClip clip, Vector3 position, Transform followTarget_, float volumeScale)
        {
            CacheAudioSource();

            version++;
            cue = cue_;
            bus = cue_.Bus;
            followTarget = followTarget_;
            baseVolume = cue_.GetVolume();
            runtimeVolumeScale = Mathf.Max(0f, volumeScale);
            returnWhenFinished = true;
            isReturning = false;

            transform.position = followTarget_ != null ? followTarget_.position : position;
            audioSource.clip = clip;
            audioSource.outputAudioMixerGroup = cue_.MixerGroup;
            audioSource.loop = cue_.PlaybackMode == ProjectAudioPlaybackMode.Loop;
            audioSource.priority = cue_.Priority;
            audioSource.spatialBlend = cue_.SpatialBlend;
            audioSource.minDistance = Mathf.Max(0.01f, cue_.MinDistance);
            audioSource.maxDistance = Mathf.Max(audioSource.minDistance, cue_.MaxDistance);
            audioSource.pitch = cue_.GetPitch();
            RefreshVolume();
            audioSource.Play();

            return new ProjectAudioHandle(this, version);
        }

        // 버스 볼륨 변경을 현재 재생 중인 소스에 반영한다
        public void RefreshVolume()
        {
            if (audioSource == null || owner == null)
            {
                return;
            }

            float effectiveVolume = Mathf.Clamp(baseVolume * runtimeVolumeScale * owner.GetEffectiveVolume(bus), 0f, MAX_EFFECTIVE_VOLUME);
            audioSource.volume = Mathf.Min(1f, effectiveVolume);
            outputGain = effectiveVolume > 1f ? effectiveVolume : 1f;
        }

        // 재생 중 볼륨 배율을 변경한다
        public void SetRuntimeVolumeScale(float volumeScale)
        {
            isFading = false;
            stopAfterFade = false;
            runtimeVolumeScale = Mathf.Max(0f, volumeScale);
            RefreshVolume();
        }

        // 재생 중 볼륨 배율을 지정 시간 동안 보간하고 필요하면 정지한다
        public void FadeToRuntimeVolumeScale(float volumeScale, float duration, bool stopAfterFade_)
        {
            float targetScale = Mathf.Max(0f, volumeScale);
            float safeDuration = Mathf.Max(0f, duration);
            if (safeDuration <= 0f)
            {
                SetRuntimeVolumeScale(targetScale);
                if (stopAfterFade_)
                {
                    StopAndReturn();
                }

                return;
            }

            fadeStartVolumeScale = runtimeVolumeScale;
            fadeTargetVolumeScale = targetScale;
            fadeDuration = safeDuration;
            fadeElapsed = 0f;
            stopAfterFade = stopAfterFade_;
            isFading = true;
        }

        // 현재 사운드를 멈추고 풀로 반환한다
        public void StopAndReturn()
        {
            if (isReturning)
            {
                return;
            }

            isReturning = true;

            if (audioSource != null)
            {
                audioSource.Stop();
            }

            ResetSource();

            if (owner != null)
            {
                owner.ReturnSource(this);
            }
        }

        // AudioSource 볼륨 한계를 넘는 큐 볼륨을 샘플 단위 게인으로 보정한다
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (outputGain <= 1f)
            {
                return;
            }

            for (int i = 0; i < data.Length; i++)
            {
                data[i] *= outputGain;
            }
        }

        // AudioSource 컴포넌트를 캐싱한다
        private void CacheAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        // 풀 반환 전 변경 가능한 상태를 초기화한다
        private void ResetSource()
        {
            followTarget = null;
            cue = null;
            baseVolume = 0f;
            runtimeVolumeScale = 1f;
            outputGain = 1f;
            returnWhenFinished = false;
            isFading = false;
            stopAfterFade = false;
            fadeStartVolumeScale = 0f;
            fadeTargetVolumeScale = 0f;
            fadeDuration = 0f;
            fadeElapsed = 0f;

            if (audioSource == null)
            {
                return;
            }

            audioSource.clip = null;
            audioSource.loop = false;
            audioSource.playOnAwake = false;
        }

        // 진행 중인 볼륨 페이드를 갱신한다
        private void UpdateFade(float deltaTime)
        {
            if (!isFading)
            {
                return;
            }

            fadeElapsed = Mathf.Min(fadeDuration, fadeElapsed + Mathf.Max(0f, deltaTime));
            float ratio = fadeDuration > 0f ? fadeElapsed / fadeDuration : 1f;
            runtimeVolumeScale = Mathf.Lerp(fadeStartVolumeScale, fadeTargetVolumeScale, ratio);
            RefreshVolume();

            if (fadeElapsed < fadeDuration)
            {
                return;
            }

            bool shouldStop = stopAfterFade;
            isFading = false;
            stopAfterFade = false;
            if (shouldStop)
            {
                StopAndReturn();
            }
        }
    }
}
