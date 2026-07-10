namespace ProjectZDefense.Audio
{
    /// <summary>
    /// 루프 사운드나 장시간 사운드를 호출자가 안전하게 정지할 수 있도록 재생 인스턴스 참조를 감싼다.
    /// </summary>
    public readonly struct ProjectAudioHandle
    {
        private readonly PooledAudioSource source;
        private readonly int version;

        public bool IsValid => source != null && source.Version == version && source.IsPlaying;

        // 오디오 핸들을 생성한다
        internal ProjectAudioHandle(PooledAudioSource source_, int version_)
        {
            source = source_;
            version = version_;
        }

        // 현재 핸들이 가리키는 사운드를 정지한다
        public void Stop()
        {
            if (!IsValid)
            {
                return;
            }

            source.StopAndReturn();
        }

        // 현재 핸들이 가리키는 사운드의 볼륨 배율을 바꾼다
        public void SetVolumeScale(float volumeScale)
        {
            if (!IsValid)
            {
                return;
            }

            source.SetRuntimeVolumeScale(volumeScale);
        }
    }
}
