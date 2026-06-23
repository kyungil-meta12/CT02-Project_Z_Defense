using ProjectZima.PolygonModularTurretsPack;
using UnityEngine;
using ProjectZDefense.StatusEffects;

/// <summary>
/// 터렛 Definition을 런타임 터렛 오브젝트에 적용하고 레벨업, 진화, 비용 소모를 중계한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TurretStatProfileApplier))]
public class TurretDefinitionRuntimeController : MonoBehaviour
{
    [SerializeField] private TurretDefinitionSO turretDefinition;
    [SerializeField, Min(1)] private int level = 1;
    [SerializeField, Min(1)] private int totalLevel = 1;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool applyOnInspectorChange = true;
    [SerializeField] private bool applyStatsToTurret = true;
    [SerializeField] private bool applyVFXToTurret = true;
    [SerializeField] private bool logRuntimeStat = true;
    [SerializeField] private bool logProjectileDamage = true;
    [SerializeField] private TurretStatProfileApplier statProfileApplier;
    [SerializeField] private Turret targetTurret;
    [SerializeField] private FiringEvent targetFiringEvent;
    [SerializeField] private string currentTurretName;
    [SerializeField] private string availableEvolutionNames;

    public TurretDefinitionSO CurrentTurretDefinition
    {
        get
        {
            return turretDefinition;
        }
    }

    public int CurrentLevel
    {
        get
        {
            return level;
        }
    }

    public int CurrentTierLevel
    {
        get
        {
            return level;
        }
    }

    public int CurrentTotalLevel
    {
        get
        {
            return totalLevel;
        }
    }

    public int CurrentMaxTierLevel
    {
        get
        {
            if (turretDefinition == null)
            {
                return 0;
            }

            return Mathf.Max(0, turretDefinition.maxLevel);
        }
    }

    public bool IsMaxTierLevelReached
    {
        get
        {
            int maxTierLevel = CurrentMaxTierLevel;
            return maxTierLevel > 0 && level >= maxTierLevel;
        }
    }

    public string CurrentTurretName
    {
        get
        {
            return currentTurretName;
        }
    }

    public string AvailableEvolutionNames
    {
        get
        {
            return availableEvolutionNames;
        }
    }

    // 컴포넌트 추가 시 필요한 참조를 자동으로 수집한다
    private void Reset()
    {
        RefreshReferences();
    }

    // 인스펙터 값 변경 시 레벨을 정규화하고 자동 적용 설정을 확인한다
    private void OnValidate()
    {
        level = Mathf.Max(1, level);
        totalLevel = Mathf.Max(1, totalLevel, level);

        if (Application.isPlaying && applyOnInspectorChange && turretDefinition != null)
        {
            Apply();
        }
    }

    // 시작 시 총 레벨을 보정하고 터렛 정의를 적용한다
    private void Start()
    {
        totalLevel = Mathf.Max(totalLevel, level);

        if (applyOnStart)
        {
            Apply();
        }
    }

    [ContextMenu("Apply Turret Definition")]
    // 현재 터렛 정의와 레벨을 실제 터렛 스탯과 VFX에 적용한다
    public void Apply()
    {
        RefreshReferences();

        if (turretDefinition == null)
        {
            Debug.LogWarning("[TurretDefinitionRuntimeController] Turret definition is missing.", this);
            return;
        }

        TurretRuntimeStat runtimeStat = TurretStatCalculator.Calculate(turretDefinition.baseStatProfile, turretDefinition.statGrowthProfile, level);
        RefreshRuntimeNames();

        if (applyStatsToTurret && statProfileApplier != null)
        {
            statProfileApplier.Apply(runtimeStat, logProjectileDamage);
            statProfileApplier.SetStatusProfile(turretDefinition.ignitionStatusProfile, level, turretDefinition.statGrowthProfile);
        }

        if (applyVFXToTurret)
        {
            ApplyVFX(runtimeStat);
        }

        if (logRuntimeStat)
        {
            //Debug.Log(
            //    $"[TurretDefinitionRuntimeController] {turretDefinition.displayName} Tier Lv.{level} Total Lv.{totalLevel} " +
            //    $"Damage:{runtimeStat.damage:0.###}, Range:{runtimeStat.range:0.###}, FireInterval:{runtimeStat.fireInterval:0.###}, " +
            //    $"ProjectileSpeed:{runtimeStat.projectileSpeed:0.###}, ProjectileCount:{runtimeStat.projectileCount}, PierceCount:{runtimeStat.pierceCount}",
            //    this);
        }
    }

    // 현재 레벨에서 진화 후보가 하나 이상 있는지 확인한다
    public bool CanEvolve()
    {
        return turretDefinition != null &&
               turretDefinition.evolutionProgressionProfile != null &&
               turretDefinition.evolutionProgressionProfile.CanEvolve(level);
    }

    // 현재 레벨에서 표시 가능한 진화 후보 수를 반환한다
    public int GetAvailableEvolutionCount()
    {
        if (turretDefinition == null || turretDefinition.evolutionProgressionProfile == null)
        {
            return 0;
        }

        return turretDefinition.evolutionProgressionProfile.GetAvailableEvolutionCount(level);
    }

    // 현재 레벨에서 지정한 표시 인덱스의 진화 후보를 반환한다
    public TurretEvolutionEntry GetAvailableEvolution(int availableIndex)
    {
        if (turretDefinition == null || turretDefinition.evolutionProgressionProfile == null)
        {
            return null;
        }

        return turretDefinition.evolutionProgressionProfile.GetAvailableEvolution(level, availableIndex);
    }

    // 지정한 레벨업 수량에 필요한 비용을 반환한다
    public ResourceCost[] GetUpgradeCosts(int levelAmount)
    {
        if (turretDefinition == null || turretDefinition.upgradeCostProfile == null || levelAmount <= 0)
        {
            return System.Array.Empty<ResourceCost>();
        }

        int targetLevel = GetClampedLevelForProgression(level + levelAmount, level);
        return turretDefinition.upgradeCostProfile.GetCosts(level, targetLevel);
    }

    // 지정한 레벨업을 비용까지 포함해 수행할 수 있는지 확인한다
    public bool CanUpgrade(int levelAmount)
    {
        if (levelAmount <= 0 || GetClampedLevelForProgression(level + levelAmount, level) <= level)
        {
            return false;
        }

        return CanSpendCosts(GetUpgradeCosts(levelAmount));
    }

    // 지정한 진화 후보의 진화 비용을 반환한다
    public ResourceCost[] GetEvolutionCosts(int availableIndex)
    {
        TurretEvolutionEntry evolutionEntry = GetAvailableEvolution(availableIndex);
        return evolutionEntry == null ? System.Array.Empty<ResourceCost>() : evolutionEntry.evolutionCosts;
    }

    // 지정한 진화 후보로 비용까지 포함해 진화할 수 있는지 확인한다
    public bool CanEvolve(int availableIndex)
    {
        TurretEvolutionEntry evolutionEntry = GetAvailableEvolution(availableIndex);
        return evolutionEntry != null &&
               evolutionEntry.targetDefinition != null &&
               CanSpendCosts(evolutionEntry.evolutionCosts);
    }

    // 비용 없이 지정한 진화 후보로 현재 터렛 정의를 교체한다
    public bool Evolve(int availableIndex)
    {
        TurretEvolutionEntry evolutionEntry = GetAvailableEvolution(availableIndex);
        if (evolutionEntry == null || evolutionEntry.targetDefinition == null)
        {
            return false;
        }

        PlayEvolutionEffect(evolutionEntry);
        turretDefinition = evolutionEntry.targetDefinition;
        level = 1;
        Apply();
        return true;
    }

    // 진화 비용을 소모한 뒤 지정한 진화 후보로 현재 터렛 정의를 교체한다
    public bool TryEvolve(int availableIndex)
    {
        TurretEvolutionEntry evolutionEntry = GetAvailableEvolution(availableIndex);
        if (evolutionEntry == null || evolutionEntry.targetDefinition == null)
        {
            TurretEconomyLogUtility.LogResult("진화", GetCurrentTurretLogName(), null, false, this, "선택 가능한 진화 후보가 없습니다.");
            return false;
        }

        if (!TrySpendCosts(evolutionEntry.evolutionCosts))
        {
            TurretEconomyLogUtility.LogResult("진화", GetEvolutionName(evolutionEntry), evolutionEntry.evolutionCosts, false, this, "재화가 부족하거나 ItemManager가 없습니다.");
            return false;
        }

        if (Evolve(availableIndex))
        {
            TurretEconomyLogUtility.LogResult("진화", GetEvolutionName(evolutionEntry), evolutionEntry.evolutionCosts, true, this);
            return true;
        }

        RefundCosts(evolutionEntry.evolutionCosts);
        TurretEconomyLogUtility.LogResult("진화", GetEvolutionName(evolutionEntry), evolutionEntry.evolutionCosts, false, this, "진화 적용에 실패해 비용을 환불했습니다.");
        return false;
    }

    // 비용 없이 지정한 진화 후보의 프리팹 인스턴스를 생성한다
    public TurretDefinitionRuntimeController CreateEvolvedInstance(int availableIndex)
    {
        TurretEvolutionEntry evolutionEntry = GetAvailableEvolution(availableIndex);
        if (evolutionEntry == null || evolutionEntry.targetDefinition == null)
        {
            return null;
        }

        if (evolutionEntry.targetDefinition.basePrefab == null)
        {
            return Evolve(availableIndex) ? this : null;
        }

        Transform currentTransform = transform;
        Transform parentTransform = currentTransform.parent;
        Vector3 localPosition = currentTransform.localPosition;
        Quaternion localRotation = currentTransform.localRotation;
        Vector3 localScale = ResolveEvolutionLocalScale(evolutionEntry.targetDefinition, parentTransform);

        PlayEvolutionEffect(evolutionEntry);
        GameObject evolvedObject = Instantiate(evolutionEntry.targetDefinition.basePrefab, parentTransform);
        Transform evolvedTransform = evolvedObject.transform;
        evolvedTransform.localPosition = localPosition;
        evolvedTransform.localRotation = localRotation;
        evolvedTransform.localScale = localScale;

        TurretDefinitionRuntimeController evolvedRuntimeController = evolvedObject.GetComponent<TurretDefinitionRuntimeController>();
        if (evolvedRuntimeController == null)
        {
            evolvedRuntimeController = evolvedObject.AddComponent<TurretDefinitionRuntimeController>();
        }

        evolvedRuntimeController.SetDefinition(evolutionEntry.targetDefinition, totalLevel, 1);
        Destroy(gameObject);
        return evolvedRuntimeController;
    }

    // 진화 대상 프리팹이 슬롯 부모 스케일을 중복 상속하지 않도록 로컬 스케일을 계산한다
    private static Vector3 ResolveEvolutionLocalScale(TurretDefinitionSO targetDefinition, Transform parentTransform)
    {
        GameObject targetPrefab = targetDefinition == null ? null : targetDefinition.basePrefab;
        Vector3 prefabLocalScale = targetPrefab == null ? Vector3.one : targetPrefab.transform.localScale;
        if (targetDefinition == null || targetDefinition.evolutionProgressionProfile != null || parentTransform == null)
        {
            return prefabLocalScale;
        }

        return DivideScale(prefabLocalScale, parentTransform.lossyScale);
    }

    // 축별 부모 스케일을 나누되 0 또는 음수 스케일 입력을 안전하게 보정한다
    private static Vector3 DivideScale(Vector3 scale, Vector3 divisor)
    {
        return new Vector3(
            DivideScaleAxis(scale.x, divisor.x),
            DivideScaleAxis(scale.y, divisor.y),
            DivideScaleAxis(scale.z, divisor.z));
    }

    // 단일 축 스케일을 안전한 양수 기준으로 나눈다
    private static float DivideScaleAxis(float value, float divisor)
    {
        float safeDivisor = Mathf.Abs(divisor);
        if (safeDivisor < 0.01f)
        {
            return value;
        }

        return value / safeDivisor;
    }

    // 진화 비용을 소모한 뒤 지정한 진화 후보의 프리팹 인스턴스를 생성한다
    public TurretDefinitionRuntimeController TryCreateEvolvedInstance(int availableIndex)
    {
        TurretEvolutionEntry evolutionEntry = GetAvailableEvolution(availableIndex);
        if (evolutionEntry == null || evolutionEntry.targetDefinition == null)
        {
            TurretEconomyLogUtility.LogResult("진화", GetCurrentTurretLogName(), null, false, this, "선택 가능한 진화 후보가 없습니다.");
            return null;
        }

        if (!TrySpendCosts(evolutionEntry.evolutionCosts))
        {
            TurretEconomyLogUtility.LogResult("진화", GetEvolutionName(evolutionEntry), evolutionEntry.evolutionCosts, false, this, "재화가 부족하거나 ItemManager가 없습니다.");
            return null;
        }

        TurretDefinitionRuntimeController evolvedRuntimeController = CreateEvolvedInstance(availableIndex);
        if (evolvedRuntimeController == null)
        {
            RefundCosts(evolutionEntry.evolutionCosts);
            TurretEconomyLogUtility.LogResult("진화", GetEvolutionName(evolutionEntry), evolutionEntry.evolutionCosts, false, this, "진화 프리팹 생성에 실패해 비용을 환불했습니다.");
            return null;
        }

        TurretEconomyLogUtility.LogResult("진화", GetEvolutionName(evolutionEntry), evolutionEntry.evolutionCosts, true, this);
        return evolvedRuntimeController;
    }

    // 비용 없이 현재 티어 레벨을 지정한 값으로 설정한다
    public void SetLevel(int level_)
    {
        int previousLevel = level;
        int nextLevel = GetClampedLevelForProgression(level_, level);
        if (nextLevel == level)
        {
            return;
        }

        level = nextLevel;
        totalLevel += Mathf.Max(0, level - previousLevel);
        Apply();
    }

    // 비용 없이 현재 티어 레벨을 지정한 수량만큼 증가시킨다
    public void AddLevel(int levelAmount)
    {
        if (levelAmount <= 0)
        {
            return;
        }

        SetLevel(level + levelAmount);
    }

    // 레벨업 비용을 소모한 뒤 현재 티어 레벨을 지정한 수량만큼 증가시킨다
    public bool TryUpgrade(int levelAmount)
    {
        if (levelAmount <= 0)
        {
            TurretEconomyLogUtility.LogResult("업그레이드", GetCurrentTurretLogName(), null, false, this, "업그레이드 수량이 유효하지 않습니다.");
            return false;
        }

        int targetLevel = GetClampedLevelForProgression(level + levelAmount, level);
        if (targetLevel <= level)
        {
            TurretEconomyLogUtility.LogResult("업그레이드", GetCurrentTurretLogName(), null, false, this, "현재 레벨에서 더 이상 업그레이드할 수 없습니다.");
            return false;
        }

        ResourceCost[] costs = GetUpgradeCosts(levelAmount);
        if (!TrySpendCosts(costs))
        {
            TurretEconomyLogUtility.LogResult("업그레이드", GetCurrentTurretLogName(), costs, false, this, "재화가 부족하거나 ItemManager가 없습니다.");
            return false;
        }

        SetLevel(level + levelAmount);
        TurretEconomyLogUtility.LogResult("업그레이드", GetCurrentTurretLogName(), costs, true, this);
        return true;
    }

    // 총 레벨을 유지하면서 터렛 정의와 티어 레벨을 설정한다
    public void SetDefinition(TurretDefinitionSO turretDefinition_, int level_)
    {
        SetDefinition(turretDefinition_, Mathf.Max(totalLevel, level_), level_);
    }

    // 터렛 정의, 총 레벨, 티어 레벨을 설정한 뒤 적용한다
    public void SetDefinition(TurretDefinitionSO turretDefinition_, int totalLevel_, int tierLevel_)
    {
        turretDefinition = turretDefinition_;
        totalLevel = Mathf.Max(1, totalLevel_);
        level = GetClampedLevelForProgression(tierLevel_, 1);
        Apply();
    }

    [ContextMenu("Evolve To First Available")]
    // 컨텍스트 메뉴에서 첫 번째 진화 후보로 비용 없이 진화한다
    private void EvolveToFirstAvailable()
    {
        if (!Evolve(0))
        {
            Debug.LogWarning("[TurretDefinitionRuntimeController] No available evolution for the current level.", this);
        }
    }

    // 런타임 스탯에 맞는 VFX와 투사체 프리팹을 적용한다
    private void ApplyVFX(TurretRuntimeStat runtimeStat)
    {
        if (turretDefinition.vfxProgressionProfile == null)
        {
            return;
        }

        TurretVFXProfileSO vfxProfile = turretDefinition.vfxProgressionProfile.GetProfileForLevel(level);
        if (vfxProfile == null)
        {
            return;
        }

        if (targetTurret != null && vfxProfile.attackVfxType == TurretAttackVfxType.Projectile && vfxProfile.projectilePrefab != null)
        {
            targetTurret.SetProjectilePrefab(vfxProfile.projectilePrefab, runtimeStat.projectileSpeed);
            targetTurret.SetProjectileScale(GetProjectileScale());
            targetTurret.SetPoisonStatusPayload(CreatePoisonStatusPayload());
            targetTurret.SetElectroStatusPayload(CreateElectroStatusPayload());
        }
        else if (targetTurret != null && vfxProfile.attackVfxType == TurretAttackVfxType.Beam && vfxProfile.beamPrefab != null)
        {
            targetTurret.SetProjectilePrefab(vfxProfile.beamPrefab, runtimeStat.projectileSpeed);
            targetTurret.SetProjectileScale(GetProjectileScale());
            targetTurret.SetPoisonStatusPayload(default);
            targetTurret.SetElectroStatusPayload(default);
        }

        if (targetFiringEvent != null)
        {
            targetFiringEvent.muzzleVFX = vfxProfile.muzzleVFX;
            targetFiringEvent.muzzleVFXDuration = vfxProfile.muzzleVFXDuration;
        }

        BeamFiringEvent beamFiringEvent = targetFiringEvent as BeamFiringEvent;
        if (beamFiringEvent != null)
        {
            bool isBeamProfile = vfxProfile.attackVfxType == TurretAttackVfxType.Beam;
            beamFiringEvent.SetBeamPrefab(isBeamProfile ? vfxProfile.beamPrefab : null);
            beamFiringEvent.SetAttackProfile(isBeamProfile ? vfxProfile.beamAttackProfile : null);
            beamFiringEvent.SetFrostStatusProfile(isBeamProfile ? turretDefinition.frostStatusProfile : null, level);
            beamFiringEvent.SetProjectileScale(GetProjectileScale());
        }
    }

    // 현재 레벨에 맞는 투사체 스케일을 반환한다
    private float GetProjectileScale()
    {
        if (turretDefinition.projectileScaleProgressionProfile == null)
        {
            return 1.0f;
        }

        return turretDefinition.projectileScaleProgressionProfile.GetScaleForLevel(level);
    }

    // 현재 터렛 정의와 레벨에 맞는 Poison 상태 payload를 생성한다
    private PoisonStatusPayload CreatePoisonStatusPayload()
    {
        if (turretDefinition == null || turretDefinition.poisonStatusProfile == null)
        {
            return default;
        }

        PoisonStatusPayload payload = turretDefinition.poisonStatusProfile.CreatePayload(level, turretDefinition.statGrowthProfile);
        return payload.hasPoisonStatus ? payload : default;
    }

    // 현재 터렛 정의와 레벨에 맞는 Electro 상태 payload를 생성한다
    private ElectroStatusPayload CreateElectroStatusPayload()
    {
        if (turretDefinition == null || turretDefinition.electroStatusProfile == null)
        {
            return default;
        }

        ElectroStatusPayload payload = turretDefinition.electroStatusProfile.CreatePayload(level, turretDefinition.statGrowthProfile);
        return payload.hasElectroStatus ? payload : default;
    }

    // 진화 엔트리에 설정된 연출 효과를 재생한다
    private void PlayEvolutionEffect(TurretEvolutionEntry evolutionEntry)
    {
        if (evolutionEntry == null || evolutionEntry.evolutionEffectPrefab == null)
        {
            return;
        }

        Vector3 effectPosition = transform.TransformPoint(evolutionEntry.evolutionEffectLocalOffset);
        float effectDuration = Mathf.Max(0.0f, evolutionEntry.evolutionEffectDuration);
        PooledObjectUtility.SpawnEffect(evolutionEntry.evolutionEffectPrefab, effectPosition, transform.rotation, effectDuration);
    }

    // 비용 배열을 현재 재화 상태로 지불할 수 있는지 확인한다
    private bool CanSpendCosts(ResourceCost[] costs)
    {
        if (!HasPayableCosts(costs))
        {
            return true;
        }

        if (InventorySystem.Inst == null)
        {
            return false;
        }

        return InventorySystem.Inst.CanAfford(costs);
    }

    // 비용 배열을 실제 재화에서 차감한다
    private bool TrySpendCosts(ResourceCost[] costs)
    {
        if (!HasPayableCosts(costs))
        {
            return true;
        }

        if (InventorySystem.Inst == null)
        {
            Debug.LogWarning("[TurretDefinitionRuntimeController] ItemManager가 없어 터렛 비용을 소모할 수 없습니다.", this);
            return false;
        }

        return InventorySystem.Inst.TrySpend(costs);
    }

    // 이미 소모한 비용 배열을 환불한다
    private void RefundCosts(ResourceCost[] costs)
    {
        if (!HasPayableCosts(costs) || InventorySystem.Inst == null)
        {
            return;
        }

        InventorySystem.Inst.Refund(costs);
    }

    // 실제 지불해야 하는 비용이 하나 이상 있는지 확인한다
    private static bool HasPayableCosts(ResourceCost[] costs)
    {
        if (costs == null)
        {
            return false;
        }

        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost != null && cost.amount > 0)
            {
                return true;
            }
        }

        return false;
    }

    // 진화 대기 레벨과 최대 레벨을 고려해 요청 레벨을 보정한다
    private int GetClampedLevelForProgression(int requestedLevel, int currentLevel)
    {
        int clampedLevel = Mathf.Max(1, requestedLevel);
        int currentLevelValue = Mathf.Max(1, currentLevel);

        if (turretDefinition == null)
        {
            return clampedLevel;
        }

        if (turretDefinition.maxLevel > 0)
        {
            clampedLevel = Mathf.Min(clampedLevel, turretDefinition.maxLevel);
        }

        if (turretDefinition.evolutionProgressionProfile == null)
        {
            return clampedLevel;
        }

        if (turretDefinition.evolutionProgressionProfile.CanEvolve(currentLevelValue))
        {
            return currentLevelValue;
        }

        int nextRequiredLevel = turretDefinition.evolutionProgressionProfile.GetNextRequiredEvolutionLevel(currentLevelValue);
        if (nextRequiredLevel <= 0)
        {
            return clampedLevel;
        }

        return Mathf.Min(clampedLevel, nextRequiredLevel);
    }

    // 필요한 터렛 관련 컴포넌트 참조를 수집한다
    private void RefreshReferences()
    {
        if (statProfileApplier == null)
        {
            statProfileApplier = GetComponent<TurretStatProfileApplier>();
        }

        if (targetTurret == null)
        {
            targetTurret = GetComponent<Turret>();
        }

        if (targetFiringEvent == null)
        {
            targetFiringEvent = GetComponent<FiringEvent>();
        }
    }

    // 현재 터렛 이름과 사용 가능한 진화 이름 문자열을 갱신한다
    private void RefreshRuntimeNames()
    {
        currentTurretName = turretDefinition == null ? string.Empty : GetDefinitionName(turretDefinition);

        if (turretDefinition == null || turretDefinition.evolutionProgressionProfile == null)
        {
            availableEvolutionNames = string.Empty;
            return;
        }

        int availableEvolutionCount = turretDefinition.evolutionProgressionProfile.GetAvailableEvolutionCount(level);
        if (availableEvolutionCount == 0)
        {
            availableEvolutionNames = string.Empty;
            return;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < availableEvolutionCount; i++)
        {
            TurretEvolutionEntry entry = turretDefinition.evolutionProgressionProfile.GetAvailableEvolution(level, i);
            if (entry == null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(GetEvolutionName(entry));
        }

        availableEvolutionNames = builder.ToString();
    }

    // 진화 엔트리의 표시 이름을 반환한다
    private string GetEvolutionName(TurretEvolutionEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(entry.displayName))
        {
            return entry.displayName;
        }

        return GetDefinitionName(entry.targetDefinition);
    }

    // 현재 터렛 정의의 로그용 표시 이름을 반환한다
    private string GetCurrentTurretLogName()
    {
        return GetDefinitionName(turretDefinition);
    }

    // 터렛 정의의 표시 이름을 반환한다
    private string GetDefinitionName(TurretDefinitionSO definition)
    {
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
}
