using UnityEngine;

namespace ProjectZDefense.Audio
{
    /// <summary>
    /// 터렛별 공격, 빔, 상태이상, 진화 사운드 이벤트와 오디오 큐 연결을 보관한다.
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
    }
}
