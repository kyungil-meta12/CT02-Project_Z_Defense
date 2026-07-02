using UnityEngine;

/// <summary>
/// 딜 미터기에서 터렛 종류별 그래프 색상을 결정하는 표시 프로필이다.
/// </summary>
[CreateAssetMenu(fileName = "TurretDamageMeterColorProfile", menuName = "Project Z Defense/UI/Turret Damage Meter Color Profile")]
public sealed class TurretDamageMeterColorProfileSO : ScriptableObject
{
    [Header("기본 색상")]
    [SerializeField] private Color fallbackColor = new Color(0.35f, 0.65f, 1.0f, 1.0f);

    [Header("터렛별 색상")]
    [SerializeField] private TurretDamageMeterColorRule[] colorRules;

    public Color FallbackColor
    {
        get
        {
            return fallbackColor;
        }
    }

    // 터렛 정의 또는 터렛 ID에 대응하는 그래프 색상을 반환한다
    public Color ResolveColor(TurretDefinitionSO turretDefinition)
    {
        if (turretDefinition == null)
        {
            return fallbackColor;
        }

        if (TryResolveByDefinition(turretDefinition, out Color color))
        {
            return color;
        }

        if (TryResolveByTurretId(turretDefinition.turretId, out color))
        {
            return color;
        }

        return fallbackColor;
    }

    // 터렛 정의 직접 참조가 일치하는 색상 규칙을 찾는다
    private bool TryResolveByDefinition(TurretDefinitionSO turretDefinition, out Color color)
    {
        color = fallbackColor;
        if (colorRules == null || turretDefinition == null)
        {
            return false;
        }

        for (int i = 0; i < colorRules.Length; i++)
        {
            TurretDamageMeterColorRule rule = colorRules[i];
            if (rule == null || rule.TurretDefinition != turretDefinition)
            {
                continue;
            }

            color = rule.BarColor;
            return true;
        }

        return false;
    }

    // 터렛 ID가 일치하는 색상 규칙을 찾는다
    private bool TryResolveByTurretId(string turretId, out Color color)
    {
        color = fallbackColor;
        if (colorRules == null || string.IsNullOrEmpty(turretId))
        {
            return false;
        }

        for (int i = 0; i < colorRules.Length; i++)
        {
            TurretDamageMeterColorRule rule = colorRules[i];
            if (rule == null || string.IsNullOrEmpty(rule.TurretId) || rule.TurretId != turretId)
            {
                continue;
            }

            color = rule.BarColor;
            return true;
        }

        return false;
    }
}

/// <summary>
/// 딜 미터기 색상 프로필에서 터렛 하나의 색상 매핑을 표현한다.
/// </summary>
[System.Serializable]
public sealed class TurretDamageMeterColorRule
{
    [Header("대상")]
    [SerializeField] private TurretDefinitionSO turretDefinition;
    [SerializeField] private string turretId;

    [Header("색상")]
    [SerializeField] private Color barColor = Color.white;

    public TurretDefinitionSO TurretDefinition
    {
        get
        {
            return turretDefinition;
        }
    }

    public string TurretId
    {
        get
        {
            return turretId;
        }
    }

    public Color BarColor
    {
        get
        {
            return barColor;
        }
    }
}
