using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace ProjectZDefense.Audio
{
    /// <summary>
    /// 프로젝트의 SFX, BGM, UI 사운드 재생과 볼륨, 풀링, 동시 재생 제한을 중앙에서 관리한다.
    /// </summary>
    public class ProjectAudioManager : MonoBehaviour
    {
        private const string MasterVolumeKey = "ProjectAudio.MasterVolume";
        private const string SfxVolumeKey = "ProjectAudio.SfxVolume";
        private const string BgmVolumeKey = "ProjectAudio.BgmVolume";
        private const string UiVolumeKey = "ProjectAudio.UiVolume";

        public static ProjectAudioManager Inst { get; private set; }

        [Header("풀링")]
        [SerializeField] private int prewarmSourceCount = 24;
        [SerializeField] private int maxSourceCount = 48;

        [Header("동시 재생 제한")]
        [SerializeField] private int maxTotalVoices = 40;
        [SerializeField] private int maxSfxVoices = 30;
        [SerializeField] private int maxBgmVoices = 2;
        [SerializeField] private int maxUiVoices = 8;

        [Header("기본 볼륨")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float uiVolume = 1f;
        [SerializeField] private bool loadSavedVolumesOnAwake = true;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private readonly Stack<PooledAudioSource> inactiveSources = new();
        private readonly List<PooledAudioSource> activeSources = new();
        private readonly Dictionary<int, float> lastPlayTimes = new();
        private Transform sourceContainer;
        private List<ProjectAudioBus> busTypes = new();

        public float MasterVolume => masterVolume;
        public float SfxVolume => sfxVolume;
        public float BgmVolume => bgmVolume;
        public float UiVolume => uiVolume;

        // 볼륨 변경 시 발생하는 이벤트
        public Action<ProjectAudioBus, float> OnVolumeChanged;

        // 싱글톤과 풀을 초기화한다
        private void Awake()
        {
            if (Inst != null && Inst != this)
            {
                Destroy(gameObject);
                return;
            }

            Inst = this;

            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (loadSavedVolumesOnAwake)
            {
                LoadVolumes();
            }

            EnsureContainer();
            PrewarmSources();

            foreach(ProjectAudioBus busType in Enum.GetValues(typeof(ProjectAudioBus)))
            {
                busTypes.Add(busType);
            }
        }

        // 싱글톤 참조를 정리한다
        private void OnDestroy()
        {
            if (Inst == this)
            {
                Inst = null;
            }
        }

        // 2D 사운드 큐를 재생한다
        public static ProjectAudioHandle PlayCue(AudioCueSO cue, float volumeScale = 1f)
        {
            return EnsureInstance().Play(cue, Vector3.zero, null, volumeScale);
        }

        // 현재 매니저를 가져오거나 없으면 새로 생성한다
        public static ProjectAudioManager GetOrCreate()
        {
            return EnsureInstance();
        }

        // 지정 위치에서 사운드 큐를 재생한다
        public static ProjectAudioHandle PlayCueAt(AudioCueSO cue, Vector3 position, float volumeScale = 1f)
        {
            return EnsureInstance().Play(cue, position, null, volumeScale);
        }

        // 지정 Transform을 따라가는 사운드 큐를 재생한다
        public static ProjectAudioHandle PlayCueFollow(AudioCueSO cue, Transform followTarget, float volumeScale = 1f)
        {
            Vector3 position = followTarget != null ? followTarget.position : Vector3.zero;
            return EnsureInstance().Play(cue, position, followTarget, volumeScale);
        }

        // 사운드 큐를 실제 오디오 소스에 할당해 재생한다
        public ProjectAudioHandle Play(AudioCueSO cue, Vector3 position, Transform followTarget, float volumeScale = 1f)
        {
            if (cue == null || !cue.HasClip())
            {
                return default;
            }

            if (!CanPassCooldown(cue))
            {
                return default;
            }

            if (!TryReserveVoice(cue))
            {
                return default;
            }

            PooledAudioSource source = GetSource();
            if (source == null)
            {
                return default;
            }

            AudioClip clip = cue.GetClip();
            if (clip == null)
            {
                ReturnSource(source);
                return default;
            }

            activeSources.Add(source);
            source.gameObject.SetActive(true);
            lastPlayTimes[cue.GetInstanceID()] = Time.unscaledTime;
            return source.Play(cue, clip, position, followTarget, volumeScale);
        }

        // 버스별 최종 볼륨을 계산한다
        public float GetEffectiveVolume(ProjectAudioBus bus)
        {
            switch (bus)
            {
                case ProjectAudioBus.Bgm:
                    return masterVolume * bgmVolume;
                case ProjectAudioBus.Ui:
                    return masterVolume * uiVolume;
                default:
                    return masterVolume * sfxVolume;
            }
        }

        // 마스터 볼륨을 설정하고 재생 중인 소스에 반영한다
        public void SetMasterVolume(float volume, bool save = true)
        {
            masterVolume = Mathf.Clamp01(volume);
            SaveVolumeIfNeeded(MasterVolumeKey, masterVolume, save);
            RefreshActiveVolumes();
            foreach(var type in busTypes)
            {
                OnVolumeChanged?.Invoke(type, GetEffectiveVolume(type));
            }
        }

        // SFX 볼륨을 설정하고 재생 중인 소스에 반영한다
        public void SetSfxVolume(float volume, bool save = true)
        {
            sfxVolume = Mathf.Clamp01(volume);
            SaveVolumeIfNeeded(SfxVolumeKey, sfxVolume, save);
            RefreshActiveVolumes();
            OnVolumeChanged?.Invoke(ProjectAudioBus.Sfx, GetEffectiveVolume(ProjectAudioBus.Sfx));
        }

        // BGM 볼륨을 설정하고 재생 중인 소스에 반영한다
        public void SetBgmVolume(float volume, bool save = true)
        {
            bgmVolume = Mathf.Clamp01(volume);
            SaveVolumeIfNeeded(BgmVolumeKey, bgmVolume, save);
            RefreshActiveVolumes();
            OnVolumeChanged?.Invoke(ProjectAudioBus.Bgm, GetEffectiveVolume(ProjectAudioBus.Bgm));
        }

        // UI 볼륨을 설정하고 재생 중인 소스에 반영한다
        public void SetUiVolume(float volume, bool save = true)
        {
            uiVolume = Mathf.Clamp01(volume);
            SaveVolumeIfNeeded(UiVolumeKey, uiVolume, save);
            RefreshActiveVolumes();
            OnVolumeChanged?.Invoke(ProjectAudioBus.Ui, GetEffectiveVolume(ProjectAudioBus.Ui));
        }

        // 재생이 끝난 오디오 소스를 풀로 되돌린다
        internal void ReturnSource(PooledAudioSource source)
        {
            if (source == null)
            {
                return;
            }

            activeSources.Remove(source);
            source.gameObject.SetActive(false);
            inactiveSources.Push(source);
        }

        // 현재 씬에 매니저가 없으면 런타임 매니저를 생성한다
        private static ProjectAudioManager EnsureInstance()
        {
            if (Inst != null)
            {
                return Inst;
            }

            ProjectAudioManager manager = FindAnyObjectByType<ProjectAudioManager>();
            if (manager != null)
            {
                Inst = manager;
                return manager;
            }

            GameObject managerObject = new GameObject("ProjectAudioManager");
            return managerObject.AddComponent<ProjectAudioManager>();
        }

        // 저장된 볼륨 값을 불러온다
        private void LoadVolumes()
        {
            masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, masterVolume);
            sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, sfxVolume);
            bgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, bgmVolume);
            uiVolume = PlayerPrefs.GetFloat(UiVolumeKey, uiVolume);
        }

        // 필요한 수만큼 오디오 소스를 미리 생성한다
        private void PrewarmSources()
        {
            int count = Mathf.Max(0, prewarmSourceCount);
            if (maxSourceCount > 0)
            {
                count = Mathf.Min(count, maxSourceCount);
            }

            for (int i = 0; i < count; i++)
            {
                inactiveSources.Push(CreateSource());
            }
        }

        // 사용할 수 있는 오디오 소스를 가져온다
        private PooledAudioSource GetSource()
        {
            if (inactiveSources.Count > 0)
            {
                return inactiveSources.Pop();
            }

            if (maxSourceCount > 0 && activeSources.Count + inactiveSources.Count >= maxSourceCount)
            {
                return null;
            }

            return CreateSource();
        }

        // 새 풀 오디오 소스를 생성한다
        private PooledAudioSource CreateSource()
        {
            EnsureContainer();

            GameObject sourceObject = new GameObject("PooledAudioSource");
            sourceObject.transform.SetParent(sourceContainer);
            AudioSource unitySource = sourceObject.AddComponent<AudioSource>();
            unitySource.playOnAwake = false;
            PooledAudioSource pooledSource = sourceObject.AddComponent<PooledAudioSource>();
            pooledSource.Initialize(this);
            sourceObject.SetActive(false);
            return pooledSource;
        }

        // 오디오 소스 컨테이너를 준비한다
        private void EnsureContainer()
        {
            if (sourceContainer != null)
            {
                return;
            }

            GameObject containerObject = new GameObject("AudioSources");
            sourceContainer = containerObject.transform;
            sourceContainer.SetParent(transform);
        }

        // 큐별 최소 재생 간격을 통과하는지 확인한다
        private bool CanPassCooldown(AudioCueSO cue)
        {
            if (cue.MinInterval <= 0f)
            {
                return true;
            }

            int cueId = cue.GetInstanceID();
            if (!lastPlayTimes.TryGetValue(cueId, out float lastTime))
            {
                return true;
            }

            return Time.unscaledTime - lastTime >= cue.MinInterval;
        }

        // 전체, 버스, 큐 단위 제한을 확인하고 필요하면 낮은 우선순위 소스를 정리한다
        private bool TryReserveVoice(AudioCueSO cue)
        {
            if (!ReserveTotalVoice(cue.Priority))
            {
                return false;
            }

            if (!ReserveBusVoice(cue.Bus, cue.Priority))
            {
                return false;
            }

            int maxSimultaneous = cue.MaxSimultaneous;
            if (maxSimultaneous <= 0)
            {
                return true;
            }

            int count = GetActiveCueCount(cue);
            if (count < maxSimultaneous)
            {
                return true;
            }

            if (!cue.ReplaceOldestWhenLimited)
            {
                return false;
            }

            PooledAudioSource oldest = FindOldestCueSource(cue);
            if (oldest == null)
            {
                return false;
            }

            oldest.StopAndReturn();
            return true;
        }

        // 전체 동시 재생 제한을 예약한다
        private bool ReserveTotalVoice(int priority)
        {
            if (maxTotalVoices <= 0 || activeSources.Count < maxTotalVoices)
            {
                return true;
            }

            PooledAudioSource source = FindLowestPrioritySource(priority);
            if (source == null)
            {
                return false;
            }

            source.StopAndReturn();
            return true;
        }

        // 버스별 동시 재생 제한을 예약한다
        private bool ReserveBusVoice(ProjectAudioBus bus, int priority)
        {
            int limit = GetBusVoiceLimit(bus);
            if (limit <= 0 || GetActiveBusCount(bus) < limit)
            {
                return true;
            }

            PooledAudioSource source = FindLowestPrioritySource(priority, bus);
            if (source == null)
            {
                return false;
            }

            source.StopAndReturn();
            return true;
        }

        // 버스별 동시 재생 제한값을 가져온다
        private int GetBusVoiceLimit(ProjectAudioBus bus)
        {
            switch (bus)
            {
                case ProjectAudioBus.Bgm:
                    return maxBgmVoices;
                case ProjectAudioBus.Ui:
                    return maxUiVoices;
                default:
                    return maxSfxVoices;
            }
        }

        // 특정 큐의 현재 활성 소스 수를 센다
        private int GetActiveCueCount(AudioCueSO cue)
        {
            int count = 0;
            for (int i = 0; i < activeSources.Count; i++)
            {
                if (activeSources[i] != null && activeSources[i].Cue == cue)
                {
                    count++;
                }
            }

            return count;
        }

        // 특정 버스의 현재 활성 소스 수를 센다
        private int GetActiveBusCount(ProjectAudioBus bus)
        {
            int count = 0;
            for (int i = 0; i < activeSources.Count; i++)
            {
                if (activeSources[i] != null && activeSources[i].Bus == bus)
                {
                    count++;
                }
            }

            return count;
        }

        // 특정 큐에서 가장 오래 재생된 소스를 찾는다
        private PooledAudioSource FindOldestCueSource(AudioCueSO cue)
        {
            for (int i = 0; i < activeSources.Count; i++)
            {
                PooledAudioSource source = activeSources[i];
                if (source != null && source.Cue == cue)
                {
                    return source;
                }
            }

            return null;
        }

        // 지정 우선순위 이하의 가장 낮은 우선순위 소스를 찾는다
        private PooledAudioSource FindLowestPrioritySource(int priority)
        {
            return FindLowestPrioritySource(priority, null);
        }

        // 지정 버스에서 우선순위가 낮은 소스를 찾는다
        private PooledAudioSource FindLowestPrioritySource(int priority, ProjectAudioBus? bus)
        {
            PooledAudioSource selected = null;
            int selectedPriority = int.MinValue;

            for (int i = 0; i < activeSources.Count; i++)
            {
                PooledAudioSource source = activeSources[i];
                if (source == null || source.Priority <= priority)
                {
                    continue;
                }

                if (bus.HasValue && source.Bus != bus.Value)
                {
                    continue;
                }

                if (selected == null || source.Priority > selectedPriority)
                {
                    selected = source;
                    selectedPriority = source.Priority;
                }
            }

            return selected;
        }

        // 모든 활성 소스의 볼륨을 현재 설정에 맞게 갱신한다
        private void RefreshActiveVolumes()
        {
            for (int i = 0; i < activeSources.Count; i++)
            {
                if (activeSources[i] != null)
                {
                    activeSources[i].RefreshVolume();
                }
            }
        }

        // 요청된 경우 볼륨 값을 PlayerPrefs에 저장한다
        private static void SaveVolumeIfNeeded(string key, float value, bool save)
        {
            if (!save)
            {
                return;
            }

            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }
    }
}
