using UnityEngine;

/// <summary>
/// 딜 미터기가 터렛 인스턴스별 피해량을 구분할 때 사용하는 런타임 출처 컴포넌트다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TurretDamageMeterSource : MonoBehaviour
{
    private TurretDefinitionRuntimeController runtimeController;
    private int totalKillCount;

    public TurretDefinitionRuntimeController RuntimeController
    {
        get
        {
            return runtimeController;
        }
    }

    public TurretDefinitionSO CurrentDefinition
    {
        get
        {
            return runtimeController == null ? null : runtimeController.CurrentTurretDefinition;
        }
    }

    public string DisplayName
    {
        get
        {
            TurretDefinitionSO definition = CurrentDefinition;
            if (definition == null)
            {
                return gameObject.name;
            }

            return string.IsNullOrEmpty(definition.displayName) ? definition.name : definition.displayName;
        }
    }

    public Sprite Icon
    {
        get
        {
            TurretDefinitionSO definition = CurrentDefinition;
            return definition == null ? null : definition.uiIcon;
        }
    }

    public string TurretId
    {
        get
        {
            TurretDefinitionSO definition = CurrentDefinition;
            return definition == null ? string.Empty : definition.turretId;
        }
    }

    public int TotalKillCount
    {
        get
        {
            return totalKillCount;
        }
    }

    // 컴포넌트가 생성될 때 터렛 런타임 컨트롤러를 캐시한다
    private void Awake()
    {
        CacheReferences();
    }

    // 활성화될 때 딜 미터기 매니저에 현재 터렛을 등록한다
    private void OnEnable()
    {
        CacheReferences();
        TurretDamageMeterManager.RegisterSource(this);
    }

    // 비활성화될 때 딜 미터기 매니저에서 현재 터렛을 제거한다
    private void OnDisable()
    {
        TurretDamageMeterManager.UnregisterSource(this);
    }

    // 이 터렛 출처의 누적 처치 수를 1 증가시킨다
    public void AddKill()
    {
        totalKillCount++;
    }

    // 진화로 새 터렛 인스턴스가 생성될 때 누적 처치 수를 이어받는다
    public void CopyLifetimeStatsFrom(TurretDamageMeterSource source)
    {
        if (source == null || source == this)
        {
            return;
        }

        totalKillCount = Mathf.Max(0, source.totalKillCount);
    }

    // 데미지 출처가 참조할 터렛 런타임 컨트롤러를 수집한다
    private void CacheReferences()
    {
        if (runtimeController == null)
        {
            runtimeController = GetComponent<TurretDefinitionRuntimeController>();
        }
    }
}
