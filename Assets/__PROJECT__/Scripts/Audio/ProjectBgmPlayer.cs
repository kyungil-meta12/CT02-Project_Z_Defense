using UnityEngine;

namespace ProjectZDefense.Audio
{
    /// <summary>
    /// 씬 시작 시 지정한 BGM 큐를 재생하고 오브젝트 비활성화 시 정지한다.
    /// </summary>
    public class ProjectBgmPlayer : MonoBehaviour
    {
        [Header("배경음")]
        [SerializeField] private AudioCueSO bgmCue;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool stopOnDisable = true;
        [SerializeField, Range(0f, 1f)] private float volumeScale = 1f;

        private ProjectAudioHandle bgmHandle;

        // 활성화 시 배경음을 재생한다
        private void OnEnable()
        {
            if (playOnEnable)
            {
                Play();
            }
        }

        // 비활성화 시 필요하면 배경음을 정지한다
        private void OnDisable()
        {
            if (stopOnDisable)
            {
                Stop();
            }
        }

        // 배경음을 재생한다
        public void Play()
        {
            Stop();
            bgmHandle = ProjectAudioManager.PlayCue(bgmCue, volumeScale);
        }

        // 재생 중인 배경음을 정지한다
        public void Stop()
        {
            bgmHandle.Stop();
            bgmHandle = default;
        }
    }
}
