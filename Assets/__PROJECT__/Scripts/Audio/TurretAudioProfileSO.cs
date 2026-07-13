using UnityEngine;

namespace ProjectZDefense.Audio
{
    /// <summary>
    /// 터렛별 공격, 배치, 상태이상, 진화 사운드 이벤트와 오디오 큐 연결을 보관한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Z Defense/Audio/Turret Audio Profile")]
    public class TurretAudioProfileSO : ScriptableObject
    {
        [System.Serializable]
        private struct TurretAudioEntry
        {
            [Header("이벤트")]
            public TurretAudioEvent audioEvent;
            public AudioCueSO cue;
            [Range(0f, 1f)] public float volumeScale;
            public bool followEmitter;

            [Header("지연 재생")]
            [Tooltip("이 엔트리를 직접 호출하지 않고 특정 이벤트 이후에 예약 재생합니다.")]
            public bool playAfterTriggerEvent;
            [Tooltip("이 이벤트가 재생된 뒤 현재 엔트리를 예약합니다.")]
            public TurretAudioEvent triggerEvent;
            [Tooltip("트리거 이벤트 이후 현재 엔트리를 재생하기까지 기다릴 시간입니다.")]
            [Min(0f)] public float delayAfterTrigger;
            [Tooltip("같은 이벤트의 이전 예약을 취소하고 가장 최근 예약만 유지합니다.")]
            public bool cancelPreviousDelayedSameEvent;
            [Tooltip("고정 시간이 아니라 트리거 이벤트 간격 비율로 지연 시간을 계산합니다.")]
            public bool useTriggerIntervalDelay;
            [Tooltip("트리거 이벤트 간격 중 어느 비율 지점에서 재생할지 정합니다.")]
            [Min(0f)] public float triggerIntervalDelayRatio;
            [Tooltip("비율 기반 지연 시간의 최소값입니다.")]
            [Min(0f)] public float minDelayAfterTrigger;
            [Tooltip("0보다 크면 비율 기반 지연 시간의 최대값으로 사용합니다.")]
            [Min(0f)] public float maxDelayAfterTrigger;

            [Header("지속 루프")]
            [Tooltip("트리거 이벤트가 반복되는 동안 현재 루프 이벤트를 켜두고 유지합니다.")]
            public bool sustainWhileTriggerContinues;
            [Tooltip("마지막 트리거 이벤트 이후 이 시간이 지나면 현재 루프 이벤트를 정지합니다.")]
            [Min(0f)] public float stopDelayAfterLastTrigger;
            [Tooltip("지속 루프가 멈출 때 종료 이벤트를 한 번 재생합니다.")]
            public bool playEndEventWhenSustainStops;
            [Tooltip("지속 루프가 멈출 때 재생할 종료 이벤트입니다.")]
            public TurretAudioEvent sustainEndEvent;
        }

        [Header("터렛 사운드")]
        [SerializeField] private TurretAudioEntry[] entries;

        // 지정 이벤트에 연결된 사운드 큐를 찾는다
        public bool TryGetCue(TurretAudioEvent audioEvent, out AudioCueSO cue, out float volumeScale, out bool followEmitter)
        {
            cue = null;
            volumeScale = 1f;
            followEmitter = false;

            if (entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].audioEvent != audioEvent)
                {
                    continue;
                }

                cue = entries[i].cue;
                volumeScale = entries[i].volumeScale <= 0f ? 1f : entries[i].volumeScale;
                followEmitter = entries[i].followEmitter;
                return cue != null;
            }

            return false;
        }

        // 지정 이벤트 이후 예약할 지연 사운드 엔트리를 가져온다
        public bool TryGetDelayedCue(int index, TurretAudioEvent triggerEvent_, out TurretAudioEvent audioEvent, out AudioCueSO cue, out float volumeScale, out bool followEmitter, out float delayAfterTrigger, out bool cancelPreviousDelayedSameEvent, out bool useTriggerIntervalDelay, out float triggerIntervalDelayRatio, out float minDelayAfterTrigger, out float maxDelayAfterTrigger)
        {
            audioEvent = default;
            cue = null;
            volumeScale = 1f;
            followEmitter = false;
            delayAfterTrigger = 0f;
            cancelPreviousDelayedSameEvent = true;
            useTriggerIntervalDelay = false;
            triggerIntervalDelayRatio = 0f;
            minDelayAfterTrigger = 0f;
            maxDelayAfterTrigger = 0f;

            if (entries == null || index < 0 || index >= entries.Length)
            {
                return false;
            }

            TurretAudioEntry entry = entries[index];
            if (!entry.playAfterTriggerEvent || entry.triggerEvent != triggerEvent_)
            {
                return false;
            }

            audioEvent = entry.audioEvent;
            cue = entry.cue;
            volumeScale = entry.volumeScale <= 0f ? 1f : entry.volumeScale;
            followEmitter = entry.followEmitter;
            delayAfterTrigger = Mathf.Max(0f, entry.delayAfterTrigger);
            cancelPreviousDelayedSameEvent = entry.cancelPreviousDelayedSameEvent;
            useTriggerIntervalDelay = entry.useTriggerIntervalDelay;
            triggerIntervalDelayRatio = Mathf.Max(0f, entry.triggerIntervalDelayRatio);
            minDelayAfterTrigger = Mathf.Max(0f, entry.minDelayAfterTrigger);
            maxDelayAfterTrigger = Mathf.Max(0f, entry.maxDelayAfterTrigger);
            return cue != null;
        }

        // 지연 재생 후보 엔트리 수를 반환한다
        public int GetEntryCount()
        {
            return entries == null ? 0 : entries.Length;
        }

        // 지정 이벤트가 이어지는 동안 유지할 루프 사운드 엔트리를 가져온다
        public bool TryGetSustainedCue(int index, TurretAudioEvent triggerEvent_, out TurretAudioEvent audioEvent, out AudioCueSO cue, out float volumeScale, out bool followEmitter, out float stopDelayAfterLastTrigger, out bool playEndEventWhenSustainStops, out TurretAudioEvent sustainEndEvent)
        {
            audioEvent = default;
            cue = null;
            volumeScale = 1f;
            followEmitter = false;
            stopDelayAfterLastTrigger = 0f;
            playEndEventWhenSustainStops = false;
            sustainEndEvent = default;

            if (entries == null || index < 0 || index >= entries.Length)
            {
                return false;
            }

            TurretAudioEntry entry = entries[index];
            if (!entry.sustainWhileTriggerContinues || entry.triggerEvent != triggerEvent_)
            {
                return false;
            }

            audioEvent = entry.audioEvent;
            cue = entry.cue;
            volumeScale = entry.volumeScale <= 0f ? 1f : entry.volumeScale;
            followEmitter = entry.followEmitter;
            stopDelayAfterLastTrigger = Mathf.Max(0f, entry.stopDelayAfterLastTrigger);
            playEndEventWhenSustainStops = entry.playEndEventWhenSustainStops;
            sustainEndEvent = entry.sustainEndEvent;
            return cue != null;
        }
    }
}
