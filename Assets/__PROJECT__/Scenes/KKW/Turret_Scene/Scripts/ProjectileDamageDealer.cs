using System.Collections.Generic;
using UnityEngine;
using ProjectZDefense.StatusEffects;

/// <summary>
/// 투사체가 충돌한 대상에게 데미지를 적용하고 관통 횟수와 중복 피격을 관리한다.
/// </summary>
[DisallowMultipleComponent]
public class ProjectileDamageDealer : MonoBehaviour
{
    private const int DEFAULT_DAMAGE_LAYER_MASK = (1 << 7) | (1 << 10);

    [SerializeField] private float damage = 1.0f;
    [SerializeField] private int pierceCount = 0;
    [SerializeField] private LayerMask damageLayerMask = DEFAULT_DAMAGE_LAYER_MASK;
    [SerializeField] private bool logDamage;

    private readonly List<IDamageable> hitDamageables = new List<IDamageable>(4);
    private IDamageable trackedTargetDamageable;
    private ProjectileHitDetector hitDetector;
    private PoisonStatusPayload poisonStatusPayload;
    private ElectroStatusPayload electroStatusPayload;
    private TurretDamagePolishProfileSO damagePolishProfile;
    private TurretDamageMeterSource damageMeterSource;
    private ProjectZDefense.Audio.ITurretAudioEventPlayer audioEventPlayer;
    private bool hasPlayedImpactAudio;

    public bool HasReachedPierceLimit
    {
        get
        {
            return hitDamageables.Count > pierceCount;
        }
    }

    public LayerMask DamageLayerMask
    {
        get
        {
            return damageLayerMask;
        }
    }

    // 데미지와 관통 수를 초기화한다
    public void Init(float damage_, int pierceCount_)
    {
        Init(damage_, pierceCount_, false);
    }

    // 데미지, 관통 수, 로그 옵션을 초기화한다
    public void Init(float damage_, int pierceCount_, bool logDamage_)
    {
        Init(damage_, pierceCount_, logDamage_, null);
    }

    // 데미지 처리 상태와 추적 타겟 정보를 초기화한다
    public void Init(float damage_, int pierceCount_, bool logDamage_, GameObject target)
    {
        Init(damage_, pierceCount_, logDamage_, target, default);
    }

    // 데미지 처리 상태, 추적 타겟, Poison 상태 정보를 초기화한다
    public void Init(float damage_, int pierceCount_, bool logDamage_, GameObject target, PoisonStatusPayload poisonStatusPayload_)
    {
        Init(damage_, pierceCount_, logDamage_, target, poisonStatusPayload_, default);
    }

    // 데미지 처리 상태, 추적 타겟, 상태이상 정보를 초기화한다
    public void Init(float damage_, int pierceCount_, bool logDamage_, GameObject target, PoisonStatusPayload poisonStatusPayload_, ElectroStatusPayload electroStatusPayload_)
    {
        Init(damage_, pierceCount_, logDamage_, target, poisonStatusPayload_, electroStatusPayload_, null);
    }

    // 데미지 처리 상태, 추적 타겟, 상태이상, 데미지 폴리싱 정보를 초기화한다
    public void Init(float damage_, int pierceCount_, bool logDamage_, GameObject target, PoisonStatusPayload poisonStatusPayload_, ElectroStatusPayload electroStatusPayload_, TurretDamagePolishProfileSO damagePolishProfile_)
    {
        Init(damage_, pierceCount_, logDamage_, target, poisonStatusPayload_, electroStatusPayload_, damagePolishProfile_, null);
    }

    // 데미지 처리 상태, 추적 타겟, 상태이상, 데미지 폴리싱 정보, 딜 미터기 출처를 초기화한다
    public void Init(float damage_, int pierceCount_, bool logDamage_, GameObject target, PoisonStatusPayload poisonStatusPayload_, ElectroStatusPayload electroStatusPayload_, TurretDamagePolishProfileSO damagePolishProfile_, TurretDamageMeterSource damageMeterSource_)
    {
        damage = Mathf.Max(0.0f, damage_);
        pierceCount = Mathf.Max(0, pierceCount_);
        logDamage = logDamage_;
        poisonStatusPayload = poisonStatusPayload_;
        electroStatusPayload = electroStatusPayload_;
        damagePolishProfile = damagePolishProfile_;
        damageMeterSource = damageMeterSource_;
        audioEventPlayer = ResolveAudioEventPlayer(damageMeterSource);
        hasPlayedImpactAudio = false;
        poisonStatusPayload.damageSource = damageMeterSource;
        electroStatusPayload.damageSource = damageMeterSource;
        hitDamageables.Clear();
        trackedTargetDamageable = ResolveDamageable(target);
        enabled = true;

        InitHitDetector(target);
    }

    // 투사체가 최종 충돌했을 때 Impact 사운드를 한 번 재생한다
    public void PlayImpactAudio(Transform emitter)
    {
        if (hasPlayedImpactAudio || audioEventPlayer == null)
        {
            return;
        }

        hasPlayedImpactAudio = true;
        audioEventPlayer.Play(ProjectZDefense.Audio.TurretAudioEvent.Impact, emitter);
    }

    // 데미지 출처 오브젝트에서 터렛 오디오 이벤트 플레이어를 찾는다
    private static ProjectZDefense.Audio.ITurretAudioEventPlayer ResolveAudioEventPlayer(TurretDamageMeterSource damageMeterSource)
    {
        if (damageMeterSource == null)
        {
            return null;
        }

        return damageMeterSource.GetComponent<ProjectZDefense.Audio.TurretAudioController>();
    }

    // 충돌한 콜라이더에서 데미지 대상 컴포넌트를 찾아 데미지를 적용한다
    public bool TryApplyDamage(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return false;
        }

        IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
        if (damageable == null || !IsAllowedDamageCollider(hitCollider, damageable) || !damageable.IsAlive || hitDamageables.Contains(damageable))
        {
            return false;
        }

        TurretDamagePolishResult damageResult = RollDamage();
        NotifyNonElectroDamageReceived(damageable, damageResult.Damage);
        DamagePopupPolicy popupPolicy = DamagePopupPolicyResolver.ResolveDirectHit(damageResult.PopupType);
        damageable.TakeDamage(new DamageInfo(damageResult.Damage, damageResult.PopupType, popupPolicy, damageMeterSource));
        hitDamageables.Add(damageable);
        ApplyPoisonStatus(damageable);
        ApplyElectroStatus(hitCollider, damageable, 0, damageResult.Damage);
        ElectroChainLightningUtility.ApplyChain(electroStatusPayload, damageable, hitCollider, ResolveChainStartPosition(hitCollider, damageable), damageResult.Damage);

        if (logDamage)
        {
            //Debug.Log($"[ProjectileDamageDealer] 데미지:{damageResult.Damage:0.###}, 대상 체력:{damageable.CurrHp:0.###}/{damageable.TotalHp:0.###}", this);
        }

        return true;
    }

    // 현재 데미지 폴리싱 프로필에 따라 실제 적용할 데미지 결과를 계산한다
    private TurretDamagePolishResult RollDamage()
    {
        if (damagePolishProfile == null)
        {
            return new TurretDamagePolishResult(damage, DamagePopupType.Normal);
        }

        return damagePolishProfile.RollDamage(damage);
    }

    // 데미지가 적용된 대상에게 Poison 상태 효과를 전달한다
    private void ApplyPoisonStatus(IDamageable damageable)
    {
        if (!poisonStatusPayload.hasPoisonStatus || damageable == null || !damageable.IsAlive)
        {
            return;
        }

        IPoisonStatusEffectReceiver poisonReceiver = damageable as IPoisonStatusEffectReceiver;
        if (poisonReceiver == null)
        {
            return;
        }

        poisonReceiver.ApplyPoisonStatus(poisonStatusPayload);
    }

    // Electro가 아닌 투사체 피해가 적용되기 전 대상에게 Overload 발동 검사를 요청한다
    private void NotifyNonElectroDamageReceived(IDamageable damageable, float appliedDamage)
    {
        if (electroStatusPayload.hasElectroStatus || damageable == null || !damageable.IsAlive)
        {
            return;
        }

        IElectroOverloadTriggerReceiver overloadReceiver = damageable as IElectroOverloadTriggerReceiver;
        if (overloadReceiver == null)
        {
            return;
        }

        overloadReceiver.NotifyNonElectroDamageReceived(appliedDamage);
    }

    // 데미지가 적용된 대상에게 Electro 상태 효과를 전달한다
    private void ApplyElectroStatus(Collider hitCollider, IDamageable damageable, int chainIndex, float appliedDamage)
    {
        if (!electroStatusPayload.hasElectroStatus || damageable == null || !damageable.IsAlive)
        {
            return;
        }

        IElectroStatusEffectReceiver electroReceiver = damageable as IElectroStatusEffectReceiver;
        if (electroReceiver == null)
        {
            return;
        }

        electroReceiver.ApplyElectroStatus(electroStatusPayload, chainIndex, appliedDamage);
    }

    // 체인 탐색 시작 위치를 피격 콜라이더 또는 대상 컴포넌트 기준으로 계산한다
    private static Vector3 ResolveChainStartPosition(Collider hitCollider, IDamageable damageable)
    {
        if (hitCollider != null)
        {
            return hitCollider.bounds.center;
        }

        if (damageable is Component damageableComponent)
        {
            return damageableComponent.transform.position;
        }

        return Vector3.zero;
    }

    // 지정 레이어가 데미지 레이어 마스크에 포함되는지 확인한다
    private bool IsDamageLayer(int layer)
    {
        return (damageLayerMask.value & (1 << layer)) != 0;
    }

    // 충돌 콜라이더가 데미지를 줄 수 있는 레이어이거나 추적 타겟의 하위 콜라이더인지 확인한다
    private bool IsAllowedDamageCollider(Collider hitCollider, IDamageable damageable)
    {
        if (hitCollider != null && IsDamageLayer(hitCollider.gameObject.layer))
        {
            return true;
        }

        if (damageable == trackedTargetDamageable)
        {
            return true;
        }

        if (damageable is Component damageableComponent)
        {
            return IsDamageLayer(damageableComponent.gameObject.layer);
        }

        return false;
    }

    // 발사 시 전달받은 타겟 오브젝트에서 데미지 대상 컴포넌트를 찾는다
    private static IDamageable ResolveDamageable(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            return damageable;
        }

        return target.GetComponentInChildren<IDamageable>();
    }

    // 보정 피격 감지 컴포넌트를 준비하고 현재 타겟을 전달한다
    private void InitHitDetector(GameObject target)
    {
        if (hitDetector == null)
        {
            hitDetector = GetComponent<ProjectileHitDetector>();
            if (hitDetector == null)
            {
                hitDetector = gameObject.AddComponent<ProjectileHitDetector>();
            }
        }

        hitDetector.Init(this, target);
    }
}
