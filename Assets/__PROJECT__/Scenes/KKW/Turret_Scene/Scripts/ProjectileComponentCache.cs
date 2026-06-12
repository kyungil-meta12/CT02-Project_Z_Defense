using System.Collections.Generic;
using ProjectZima.PolygonModularTurretsPack;
using UnityEngine;

/// <summary>
/// 풀링된 투사체에서 반복 조회되는 컴포넌트와 콜라이더 목록을 캐시한다.
/// </summary>
[DisallowMultipleComponent]
public class ProjectileComponentCache : MonoBehaviour
{
    private readonly List<Collider> cachedColliders = new List<Collider>(4);
    private bool hasCachedColliders;

    public ProjectileMovement ProjectileMovement { get; private set; }
    public Hovl.HS_ProjectileMover HovlProjectileMover { get; private set; }
    public RocketProjectileMovement RocketProjectileMovement { get; private set; }
    public ProjectileDamageDealer DamageDealer { get; private set; }

    // 최초 활성화 전에 투사체에서 자주 쓰는 컴포넌트를 캐시한다
    private void Awake()
    {
        CacheComponents();
        CacheColliders();
    }

    // 투사체 루트의 런타임 컴포넌트 참조를 갱신한다
    public void CacheComponents()
    {
        if (ProjectileMovement == null)
        {
            ProjectileMovement = GetComponent<ProjectileMovement>();
        }

        if (HovlProjectileMover == null)
        {
            HovlProjectileMover = GetComponent<Hovl.HS_ProjectileMover>();
        }

        if (RocketProjectileMovement == null)
        {
            RocketProjectileMovement = GetComponent<RocketProjectileMovement>();
        }

        if (DamageDealer == null)
        {
            DamageDealer = GetComponent<ProjectileDamageDealer>();
        }
    }

    // 데미지 처리 컴포넌트를 반환하고 없으면 한 번만 추가한다
    public ProjectileDamageDealer GetOrAddDamageDealer()
    {
        if (DamageDealer != null)
        {
            return DamageDealer;
        }

        DamageDealer = gameObject.AddComponent<ProjectileDamageDealer>();
        return DamageDealer;
    }

    // 캐시된 투사체 콜라이더를 호출자가 재사용하는 리스트로 복사한다
    public void CopyCollidersTo(List<Collider> target)
    {
        if (target == null)
        {
            return;
        }

        if (!hasCachedColliders)
        {
            CacheColliders();
        }

        target.Clear();
        for (int i = 0; i < cachedColliders.Count; i++)
        {
            Collider cachedCollider = cachedColliders[i];
            if (cachedCollider != null)
            {
                target.Add(cachedCollider);
            }
        }
    }

    // 투사체 하위 콜라이더 목록을 캐시한다
    private void CacheColliders()
    {
        cachedColliders.Clear();
        GetComponentsInChildren(false, cachedColliders);
        hasCachedColliders = true;
    }
}
