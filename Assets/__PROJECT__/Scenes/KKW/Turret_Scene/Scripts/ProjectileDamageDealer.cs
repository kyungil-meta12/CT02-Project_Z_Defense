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
        damage = Mathf.Max(0.0f, damage_);
        pierceCount = Mathf.Max(0, pierceCount_);
        logDamage = logDamage_;
        poisonStatusPayload = poisonStatusPayload_;
        hitDamageables.Clear();
        trackedTargetDamageable = ResolveDamageable(target);
        enabled = true;

        InitHitDetector(target);
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

        damageable.TakeDamage(damage);
        hitDamageables.Add(damageable);
        ApplyPoisonStatus(damageable);

        if (logDamage)
        {
            //Debug.Log($"[ProjectileDamageDealer] 데미지:{damage:0.###}, 대상 체력:{damageable.CurrHp:0.###}/{damageable.TotalHp:0.###}", this);
        }

        return true;
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
