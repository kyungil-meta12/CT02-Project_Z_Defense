using System.Collections.Generic;
using UnityEngine;

namespace ProjectZDefense.Audio
{
    /// <summary>
    /// 터렛 인스턴스에서 발생한 사운드 이벤트를 TurretAudioProfileSO와 ProjectAudioManager로 전달한다.
    /// </summary>
    public class TurretAudioController : MonoBehaviour, ITurretAudioEventPlayer
    {
        [Header("터렛 사운드")]
        [SerializeField] private TurretAudioProfileSO audioProfile;
        [SerializeField] private Transform defaultEmitter;
        [SerializeField, Min(0.01f)] private float fireTriggerInterval = 0.1f;

        private ProjectAudioHandle beamLoopHandle;
        private ProjectAudioHandle projectileLoopHandle;
        private ProjectAudioHandle chargeLoopHandle;
        private ProjectAudioHandle fireLoopHandle;
        private ProjectAudioHandle reloadLoopHandle;
        private readonly List<PendingDelayedAudioEvent> pendingDelayedEvents = new(4);
        private readonly List<SustainedAudioEvent> sustainedEvents = new(4);

        private struct PendingDelayedAudioEvent
        {
            public TurretAudioEvent AudioEvent;
            public AudioCueSO Cue;
            public Transform Emitter;
            public float VolumeScale;
            public bool FollowEmitter;
            public float PlayTime;
        }

        private struct SustainedAudioEvent
        {
            public TurretAudioEvent AudioEvent;
            public TurretAudioEvent EndEvent;
            public Transform Emitter;
            public float StopTime;
            public bool PlayEndEvent;
        }

        // 예약된 지연 사운드의 재생 시점을 확인한다
        private void Update()
        {
            UpdateDelayedEvents();
            UpdateSustainedEvents();
        }

        // 비활성화 시 루프 사운드를 정리한다
        private void OnDisable()
        {
            ClearDelayedEvents();
            ClearSustainedEvents();
            StopLoopingSounds();
        }

        // 제거 시 루프 사운드를 정리한다
        private void OnDestroy()
        {
            ClearDelayedEvents();
            ClearSustainedEvents();
            StopLoopingSounds();
        }

        // 터렛 오디오 프로필을 교체한다
        public void SetAudioProfile(TurretAudioProfileSO audioProfile_)
        {
            audioProfile = audioProfile_;
            ClearDelayedEvents();
            ClearSustainedEvents();
            StopLoopingSounds();
        }

        // 기본 사운드 발생 위치를 교체한다
        public void SetDefaultEmitter(Transform emitter)
        {
            defaultEmitter = emitter;
        }

        // 트리거 이벤트의 반복 간격을 설정한다
        public void SetTriggerInterval(TurretAudioEvent triggerEvent, float interval)
        {
            if (triggerEvent != TurretAudioEvent.Fire)
            {
                return;
            }

            fireTriggerInterval = Mathf.Max(0.01f, interval);
        }

        // 기본 발생 위치에서 터렛 사운드 이벤트를 재생한다
        public ProjectAudioHandle Play(TurretAudioEvent audioEvent)
        {
            return Play(audioEvent, defaultEmitter);
        }

        // 지정 발생 위치에서 터렛 사운드 이벤트를 재생한다
        public ProjectAudioHandle Play(TurretAudioEvent audioEvent, Transform emitter)
        {
            if (audioProfile == null)
            {
                return default;
            }

            Transform targetEmitter = emitter != null ? emitter : defaultEmitter;
            RefreshSustainedEvents(audioEvent, targetEmitter);
            QueueDelayedEvents(audioEvent, targetEmitter);

            if (!audioProfile.TryGetCue(audioEvent, out AudioCueSO cue, out float volumeScale, out bool followEmitter))
            {
                return default;
            }

            if (IsLoopEvent(audioEvent) && IsLoopPlaying(audioEvent))
            {
                return GetLoopHandle(audioEvent);
            }

            ProjectAudioHandle handle = PlayCue(cue, targetEmitter, volumeScale, followEmitter);
            StoreLoopHandle(audioEvent, handle);
            return handle;
        }

        // 지정 월드 위치에서 터렛 사운드 이벤트를 재생한다
        public ProjectAudioHandle PlayAt(TurretAudioEvent audioEvent, Vector3 position)
        {
            if (audioProfile == null)
            {
                return default;
            }

            if (!audioProfile.TryGetCue(audioEvent, out AudioCueSO cue, out float volumeScale, out _))
            {
                return default;
            }

            ProjectAudioHandle handle = ProjectAudioManager.PlayCueAt(cue, position, volumeScale);
            StoreLoopHandle(audioEvent, handle);
            return handle;
        }

        // 지정 이벤트에 연결된 가장 긴 클립 재생 시간을 반환한다
        public float GetMaxClipLength(TurretAudioEvent audioEvent)
        {
            if (audioProfile == null)
            {
                return 0f;
            }

            if (!audioProfile.TryGetCue(audioEvent, out AudioCueSO cue, out _, out _))
            {
                return 0f;
            }

            return cue.GetMaxClipLength();
        }

        // 빔 루프 사운드를 명시적으로 정지한다
        public void StopBeamLoop()
        {
            beamLoopHandle.Stop();
            beamLoopHandle = default;
        }

        // 투사체 루프 사운드를 명시적으로 정지한다
        public void StopProjectileLoop()
        {
            projectileLoopHandle.Stop();
            projectileLoopHandle = default;
        }

        // 차징 루프 사운드를 명시적으로 정지한다
        public void StopChargeLoop()
        {
            chargeLoopHandle.Stop();
            chargeLoopHandle = default;
        }

        // 연사 루프 사운드를 명시적으로 정지한다
        public void StopFireLoop()
        {
            fireLoopHandle.Stop();
            fireLoopHandle = default;
        }

        // 재장전 루프 사운드를 명시적으로 정지한다
        public void StopReloadLoop()
        {
            reloadLoopHandle.Stop();
            reloadLoopHandle = default;
        }

        // 지정한 루프 이벤트에 해당하는 사운드를 정지한다
        public void StopLoop(TurretAudioEvent audioEvent)
        {
            switch (audioEvent)
            {
                case TurretAudioEvent.BeamLoop:
                    StopBeamLoop();
                    break;
                case TurretAudioEvent.ProjectileLoop:
                    StopProjectileLoop();
                    break;
                case TurretAudioEvent.ChargeLoop:
                    StopChargeLoop();
                    break;
                case TurretAudioEvent.FireLoop:
                    StopFireLoop();
                    break;
                case TurretAudioEvent.ReloadLoop:
                    StopReloadLoop();
                    break;
            }
        }

        // 모든 루프 사운드를 외부에서 명시적으로 정지한다
        public void StopAllLoops()
        {
            StopLoopingSounds();
        }

        // 재생 이벤트에 맞는 방식으로 큐를 재생한다
        private static ProjectAudioHandle PlayCue(AudioCueSO cue, Transform emitter, float volumeScale, bool followEmitter)
        {
            if (emitter == null)
            {
                return ProjectAudioManager.PlayCue(cue, volumeScale);
            }

            if (followEmitter)
            {
                return ProjectAudioManager.PlayCueFollow(cue, emitter, volumeScale);
            }

            return ProjectAudioManager.PlayCueAt(cue, emitter.position, volumeScale);
        }

        // 예약된 지연 사운드의 재생 시점을 처리한다
        private void UpdateDelayedEvents()
        {
            for (int i = pendingDelayedEvents.Count - 1; i >= 0; i--)
            {
                PendingDelayedAudioEvent pendingEvent = pendingDelayedEvents[i];
                if (Time.time < pendingEvent.PlayTime)
                {
                    continue;
                }

                pendingDelayedEvents.RemoveAt(i);
                ProjectAudioHandle handle = PlayCue(pendingEvent.Cue, pendingEvent.Emitter, pendingEvent.VolumeScale, pendingEvent.FollowEmitter);
                StoreLoopHandle(pendingEvent.AudioEvent, handle);
            }
        }

        // 지속 루프 사운드의 종료 시점을 처리한다
        private void UpdateSustainedEvents()
        {
            for (int i = sustainedEvents.Count - 1; i >= 0; i--)
            {
                SustainedAudioEvent sustainedEvent = sustainedEvents[i];
                if (Time.time < sustainedEvent.StopTime)
                {
                    continue;
                }

                sustainedEvents.RemoveAt(i);
                StopLoop(sustainedEvent.AudioEvent);
                if (sustainedEvent.PlayEndEvent)
                {
                    PlayDirectEvent(sustainedEvent.EndEvent, sustainedEvent.Emitter);
                }
            }
        }

        // 루프 이벤트 핸들을 보관한다
        private void StoreLoopHandle(TurretAudioEvent audioEvent, ProjectAudioHandle handle)
        {
            switch (audioEvent)
            {
                case TurretAudioEvent.BeamLoop:
                    beamLoopHandle.Stop();
                    beamLoopHandle = handle;
                    break;
                case TurretAudioEvent.ProjectileLoop:
                    projectileLoopHandle.Stop();
                    projectileLoopHandle = handle;
                    break;
                case TurretAudioEvent.ChargeLoop:
                    chargeLoopHandle.Stop();
                    chargeLoopHandle = handle;
                    break;
                case TurretAudioEvent.FireLoop:
                    fireLoopHandle.Stop();
                    fireLoopHandle = handle;
                    break;
                case TurretAudioEvent.ReloadLoop:
                    reloadLoopHandle.Stop();
                    reloadLoopHandle = handle;
                    break;
            }
        }

        // 트리거 이벤트가 이어지는 동안 유지할 루프 사운드를 갱신한다
        private void RefreshSustainedEvents(TurretAudioEvent triggerEvent, Transform emitter)
        {
            if (audioProfile == null)
            {
                return;
            }

            int entryCount = audioProfile.GetEntryCount();
            for (int i = 0; i < entryCount; i++)
            {
                if (!audioProfile.TryGetSustainedCue(i, triggerEvent, out TurretAudioEvent audioEvent, out AudioCueSO cue, out float volumeScale, out bool followEmitter, out float stopDelayAfterLastTrigger, out bool playEndEventWhenSustainStops, out TurretAudioEvent sustainEndEvent))
                {
                    continue;
                }

                if (!IsLoopEvent(audioEvent))
                {
                    continue;
                }

                if (!IsLoopPlaying(audioEvent))
                {
                    ProjectAudioHandle handle = PlayCue(cue, emitter, volumeScale, followEmitter);
                    StoreLoopHandle(audioEvent, handle);
                }

                UpsertSustainedEvent(audioEvent, sustainEndEvent, emitter, stopDelayAfterLastTrigger, playEndEventWhenSustainStops);
            }
        }

        // 지속 루프 사운드의 종료 예약 시간을 추가하거나 갱신한다
        private void UpsertSustainedEvent(TurretAudioEvent audioEvent, TurretAudioEvent endEvent, Transform emitter, float stopDelayAfterLastTrigger, bool playEndEvent)
        {
            float stopTime = Time.time + stopDelayAfterLastTrigger;
            for (int i = 0; i < sustainedEvents.Count; i++)
            {
                SustainedAudioEvent sustainedEvent = sustainedEvents[i];
                if (sustainedEvent.AudioEvent != audioEvent)
                {
                    continue;
                }

                sustainedEvent.EndEvent = endEvent;
                sustainedEvent.Emitter = emitter;
                sustainedEvent.StopTime = stopTime;
                sustainedEvent.PlayEndEvent = playEndEvent;
                sustainedEvents[i] = sustainedEvent;
                return;
            }

            sustainedEvents.Add(new SustainedAudioEvent
            {
                AudioEvent = audioEvent,
                EndEvent = endEvent,
                Emitter = emitter,
                StopTime = stopTime,
                PlayEndEvent = playEndEvent
            });
        }

        // 트리거 이벤트에 연결된 지연 사운드를 예약한다
        private void QueueDelayedEvents(TurretAudioEvent triggerEvent, Transform emitter)
        {
            if (audioProfile == null)
            {
                return;
            }

            int entryCount = audioProfile.GetEntryCount();
            for (int i = 0; i < entryCount; i++)
            {
                if (!audioProfile.TryGetDelayedCue(i, triggerEvent, out TurretAudioEvent audioEvent, out AudioCueSO cue, out float volumeScale, out bool followEmitter, out float delayAfterTrigger, out bool cancelPreviousDelayedSameEvent, out bool useTriggerIntervalDelay, out float triggerIntervalDelayRatio, out float minDelayAfterTrigger, out float maxDelayAfterTrigger))
                {
                    continue;
                }

                if (cancelPreviousDelayedSameEvent)
                {
                    RemoveDelayedEvents(audioEvent);
                }

                PendingDelayedAudioEvent pendingEvent = new PendingDelayedAudioEvent
                {
                    AudioEvent = audioEvent,
                    Cue = cue,
                    Emitter = emitter,
                    VolumeScale = volumeScale,
                    FollowEmitter = followEmitter,
                    PlayTime = Time.time + CalculateDelayedEventDelay(triggerEvent, delayAfterTrigger, useTriggerIntervalDelay, triggerIntervalDelayRatio, minDelayAfterTrigger, maxDelayAfterTrigger)
                };
                pendingDelayedEvents.Add(pendingEvent);
            }
        }

        // 지연 이벤트의 실제 예약 지연 시간을 계산한다
        private float CalculateDelayedEventDelay(TurretAudioEvent triggerEvent, float delayAfterTrigger, bool useTriggerIntervalDelay, float triggerIntervalDelayRatio, float minDelayAfterTrigger, float maxDelayAfterTrigger)
        {
            if (!useTriggerIntervalDelay)
            {
                return delayAfterTrigger;
            }

            float delay = GetTriggerInterval(triggerEvent) * triggerIntervalDelayRatio;
            delay = Mathf.Max(delay, minDelayAfterTrigger);
            if (maxDelayAfterTrigger > 0f)
            {
                delay = Mathf.Min(delay, maxDelayAfterTrigger);
            }

            return delay;
        }

        // 트리거 이벤트의 현재 반복 간격을 반환한다
        private float GetTriggerInterval(TurretAudioEvent triggerEvent)
        {
            if (triggerEvent == TurretAudioEvent.Fire)
            {
                return fireTriggerInterval;
            }

            return 0f;
        }

        // 지정 이벤트를 후속 정책 없이 직접 재생한다
        private ProjectAudioHandle PlayDirectEvent(TurretAudioEvent audioEvent, Transform emitter)
        {
            if (audioProfile == null)
            {
                return default;
            }

            if (!audioProfile.TryGetCue(audioEvent, out AudioCueSO cue, out float volumeScale, out bool followEmitter))
            {
                return default;
            }

            ProjectAudioHandle handle = PlayCue(cue, emitter, volumeScale, followEmitter);
            StoreLoopHandle(audioEvent, handle);
            return handle;
        }

        // 지정 이벤트가 루프 핸들을 보관하는 이벤트인지 확인한다
        private static bool IsLoopEvent(TurretAudioEvent audioEvent)
        {
            return audioEvent == TurretAudioEvent.BeamLoop ||
                   audioEvent == TurretAudioEvent.ProjectileLoop ||
                   audioEvent == TurretAudioEvent.ChargeLoop ||
                   audioEvent == TurretAudioEvent.FireLoop ||
                   audioEvent == TurretAudioEvent.ReloadLoop;
        }

        // 지정 루프 이벤트가 현재 재생 중인지 확인한다
        private bool IsLoopPlaying(TurretAudioEvent audioEvent)
        {
            return GetLoopHandle(audioEvent).IsValid;
        }

        // 지정 루프 이벤트의 핸들을 반환한다
        private ProjectAudioHandle GetLoopHandle(TurretAudioEvent audioEvent)
        {
            switch (audioEvent)
            {
                case TurretAudioEvent.BeamLoop:
                    return beamLoopHandle;
                case TurretAudioEvent.ProjectileLoop:
                    return projectileLoopHandle;
                case TurretAudioEvent.ChargeLoop:
                    return chargeLoopHandle;
                case TurretAudioEvent.FireLoop:
                    return fireLoopHandle;
                case TurretAudioEvent.ReloadLoop:
                    return reloadLoopHandle;
                default:
                    return default;
            }
        }

        // 지정 이벤트의 예약된 지연 사운드를 제거한다
        private void RemoveDelayedEvents(TurretAudioEvent audioEvent)
        {
            for (int i = pendingDelayedEvents.Count - 1; i >= 0; i--)
            {
                if (pendingDelayedEvents[i].AudioEvent == audioEvent)
                {
                    pendingDelayedEvents.RemoveAt(i);
                }
            }
        }

        // 예약된 지연 사운드를 모두 제거한다
        private void ClearDelayedEvents()
        {
            pendingDelayedEvents.Clear();
        }

        // 지속 루프 종료 예약을 모두 제거한다
        private void ClearSustainedEvents()
        {
            sustainedEvents.Clear();
        }

        // 터렛에 남아 있는 루프 사운드를 모두 정지한다
        private void StopLoopingSounds()
        {
            StopBeamLoop();
            StopProjectileLoop();
            StopChargeLoop();
            StopFireLoop();
            StopReloadLoop();
        }
    }
}
