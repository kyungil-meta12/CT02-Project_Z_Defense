using UnityEngine;

[CreateAssetMenu(menuName = "Project Z Defense/Turret Stat Profile")]
public class TurretStatProfileSO : ScriptableObject
{
    public static event System.Action<TurretStatProfileSO> ProfileChanged;

    [Header("Combat")]
    [Tooltip("공통 직접 데미지 또는 상태이상 계산의 소스값입니다. Ignition_Turret처럼 최대체력 비례 틱데미지를 쓰는 터렛은 이 값이 실질 DPS를 대표하지 않습니다.")]
    public float damage = 1.0f;
    [Tooltip("TargetFinder 선택 반경입니다. Ignition_Turret의 실제 화염 원뿔 범위는 IgnitionConeDetector.range가 담당합니다.")]
    public float range = 10.0f;
    [Tooltip("공통 발사 요청 주기입니다. Ignition_Turret의 화상 틱 주기는 Ignition_Status_Profile_SO.tickInterval이 담당합니다.")]
    public float fireInterval = 0.5f;

    [Header("Projectile")]
    public float projectileSpeed = 20.0f;
    public int projectileCount = 1;
    public int pierceCount = 0;

    private void OnValidate()
    {
        damage = Mathf.Max(0.0f, damage);
        range = Mathf.Max(0.0f, range);
        fireInterval = Mathf.Max(0.01f, fireInterval);
        projectileSpeed = Mathf.Max(0.0f, projectileSpeed);
        projectileCount = Mathf.Max(1, projectileCount);
        pierceCount = Mathf.Max(0, pierceCount);

        ProfileChanged?.Invoke(this);
    }
}
