using UnityEngine;

namespace ProjectZDefense.Audio
{
    /// <summary>
    /// 터렛 오디오 이벤트를 재생할 수 있는 런타임 플레이어 계약이다.
    /// </summary>
    public interface ITurretAudioEventPlayer
    {
        // 지정 발생 위치에서 터렛 오디오 이벤트를 재생한다
        ProjectAudioHandle Play(TurretAudioEvent audioEvent, Transform emitter);
    }
}
