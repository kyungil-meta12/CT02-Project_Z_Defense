using ProjectZima.PolygonModularTurretsPack;
using UnityEngine;

/// <summary>
/// 터렛 UI 팝업들이 공유하는 현재 선택 터렛 정보를 담는다.
/// </summary>
public struct TurretSelectionContext
{
    public readonly TurretDefinitionRuntimeController Turret;
    public readonly TurretBaseSlot Slot;

    public bool IsValid
    {
        get
        {
            return Turret != null;
        }
    }

    public TurretDefinitionSO Definition
    {
        get
        {
            return Turret == null ? null : Turret.CurrentTurretDefinition;
        }
    }

    // 선택된 터렛과 설치 슬롯을 저장한다
    public TurretSelectionContext(TurretDefinitionRuntimeController turret, TurretBaseSlot slot)
    {
        Turret = turret;
        Slot = slot;
    }

    // 실제 타겟 탐색 기준 위치를 우선 사용해 사거리 표시 중심을 반환한다
    public Vector3 GetRangeCenter()
    {
        TargetFinder targetFinder = GetTargetFinder();
        if (targetFinder != null)
        {
            return targetFinder.pivotObject != null ? targetFinder.pivotObject.transform.position : targetFinder.transform.position;
        }

        if (Slot != null && Slot.BuildPoint != null)
        {
            return Slot.BuildPoint.position;
        }

        return Turret == null ? Vector3.zero : Turret.transform.position;
    }

    // 실제 타겟 탐색 반경을 우선 사용해 현재 사거리 표시 반경을 반환한다
    public float GetRangeRadius()
    {
        TargetFinder targetFinder = GetTargetFinder();
        if (targetFinder != null)
        {
            return Mathf.Max(0.0f, targetFinder.radius);
        }

        return Mathf.Max(0.0f, CalculateCurrentStat().range);
    }

    // 터렛 정의에서 표시 이름을 안전하게 가져온다
    public string GetDisplayName()
    {
        TurretDefinitionSO definition = Definition;
        if (definition == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(definition.displayName))
        {
            return definition.displayName;
        }

        return definition.name;
    }

    // 현재 티어 레벨과 누적 레벨을 UI 표시 문자열로 변환한다
    public string GetLevelText()
    {
        if (Turret == null)
        {
            return string.Empty;
        }

        if (Turret.CurrentTierLevel == Turret.CurrentTotalLevel)
        {
            return $"Lv. {Turret.CurrentTierLevel}";
        }

        return $"Tier Lv. {Turret.CurrentTierLevel} / Total Lv. {Turret.CurrentTotalLevel}";
    }

    // 터렛 정의에 입력된 선택 팝업용 짧은 설명을 반환한다
    public string GetShortDescription()
    {
        TurretDefinitionSO definition = Definition;
        return definition == null ? string.Empty : definition.shortDescription;
    }

    // 현재 터렛 정의와 티어 레벨로 런타임 스탯을 계산한다
    public TurretRuntimeStat CalculateCurrentStat()
    {
        if (Turret == null)
        {
            return new TurretRuntimeStat();
        }

        return TurretStatCalculator.Calculate(Turret.CurrentTurretDefinition, Turret.CurrentTierLevel);
    }

    // 현재 선택 터렛의 TargetFinder 컴포넌트를 반환한다
    private TargetFinder GetTargetFinder()
    {
        return Turret == null ? null : Turret.GetComponent<TargetFinder>();
    }
}
