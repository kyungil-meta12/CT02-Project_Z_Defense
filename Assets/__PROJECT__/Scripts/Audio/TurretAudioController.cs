using UnityEngine;

namespace ProjectZDefense.Audio
{
    /// <summary>
    /// 터렛 인스턴스에서 발생한 사운드 이벤트를 TurretAudioProfileSO와 ProjectAudioManager로 전달한다.
    /// </summary>
    public class TurretAudioController : MonoBehaviour
    {
        [Header("터렛 사운드")]
        [SerializeField] private TurretAudioProfileSO audioProfile;
        [SerializeField] private Transform defaultEmitter;

        private ProjectAudioHandle beamLoopHandle;
        private ProjectAudioHandle projectileLoopHandle;
        private ProjectAudioHandle chargeLoopHandle;
        private ProjectAudioHandle fireLoopHandle;
        private ProjectAudioHandle reloadLoopHandle;

        // 비활성화 시 루프 사운드를 정리한다
        private void OnDisable()
        {
            StopLoopingSounds();
        }

        // 제거 시 루프 사운드를 정리한다
        private void OnDestroy()
        {
            StopLoopingSounds();
        }

        // 터렛 오디오 프로필을 교체한다
        public void SetAudioProfile(TurretAudioProfileSO audioProfile_)
        {
            audioProfile = audioProfile_;
            StopLoopingSounds();
        }

        // 기본 사운드 발생 위치를 교체한다
        public void SetDefaultEmitter(Transform emitter)
        {
            defaultEmitter = emitter;
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

            if (!audioProfile.TryGetCue(audioEvent, out AudioCueSO cue, out float volumeScale, out bool followEmitter))
            {
                return default;
            }

            Transform targetEmitter = emitter != null ? emitter : defaultEmitter;
            ProjectAudioHandle handle = PlayCue(cue, targetEmitter, volumeScale, followEmitter);
            StoreLoopHandle(audioEvent, handle);
            return handle;
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
