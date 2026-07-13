namespace ProjectZDefense.Audio
{
    /// <summary>
    /// 터렛 런타임에서 발생할 수 있는 사운드 이벤트 종류를 정의한다.
    /// </summary>
    public enum TurretAudioEvent
    {
        Fire = 0,
        Muzzle = 1,
        Impact = 2,
        BeamStart = 3,
        BeamLoop = 4,
        BeamStop = 5,
        ProjectileLoop = 6,
        StatusApply = 7,
        StatusBurst = 8,
        Skill = 9,
        Evolution = 10,
        Placement = 11,
        AimStart = 12,
        ChargeStart = 13,
        ChargeLoop = 14,
        ChargeRelease = 15,
        FireLoop = 16,
        FireEnd = 17,
        ReloadStart = 18,
        ReloadLoop = 19,
        ReloadEnd = 20,
        Empty = 21,
        PlacementAvailable = 22
    }
}
