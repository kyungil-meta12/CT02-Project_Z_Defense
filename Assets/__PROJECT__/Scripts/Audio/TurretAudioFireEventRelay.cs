using ProjectZima.PolygonModularTurretsPack;
using UnityEngine;

namespace ProjectZDefense.Audio
{
    /// <summary>
    /// 외부 터렛 발사 이벤트를 프로젝트 터렛 오디오 이벤트로 변환한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Turret))]
    [RequireComponent(typeof(TurretAudioController))]
    public sealed class TurretAudioFireEventRelay : MonoBehaviour
    {
        private Turret turret;
        private TurretAudioController audioController;

        // 컴포넌트가 생성될 때 필요한 참조를 캐시한다
        private void Awake()
        {
            CacheReferences();
        }

        // 활성화될 때 터렛 발사 이벤트 구독을 시작한다
        private void OnEnable()
        {
            CacheReferences();
            if (turret != null)
            {
                turret.Fired += HandleTurretFired;
            }
        }

        // 비활성화될 때 터렛 발사 이벤트 구독을 해제한다
        private void OnDisable()
        {
            if (turret != null)
            {
                turret.Fired -= HandleTurretFired;
            }
        }

        // 터렛 발사 이벤트를 Fire 오디오 이벤트로 전달한다
        private void HandleTurretFired()
        {
            if (audioController == null)
            {
                return;
            }

            audioController.Play(TurretAudioEvent.Fire);
        }

        // 터렛과 오디오 컨트롤러 참조를 수집한다
        private void CacheReferences()
        {
            if (turret == null)
            {
                turret = GetComponent<Turret>();
            }

            if (audioController == null)
            {
                audioController = GetComponent<TurretAudioController>();
            }
        }
    }
}
