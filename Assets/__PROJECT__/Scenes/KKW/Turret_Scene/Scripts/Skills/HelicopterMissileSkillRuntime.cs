using System.Collections;
using UnityEngine;

public class HelicopterMissileSkillRuntime : MonoBehaviour
{
    private HelicopterMissileSkillDefinitionSO definition;
    private HelicopterMissileSkillLevelData levelData;
    private Camera targetCamera;
    private Vector3 areaCenter;
    private Quaternion areaRotation;
    private GameObject helicopterInstance;
    private Collider[] hitBuffer;
    private IDamageable[] damagedTargets;
    private bool hasAppliedCastDamage;
    private int activeMissileCount;

    // 스킬 런타임 데이터를 초기화하고 연출 코루틴을 시작한다.
    public void Initialize(HelicopterMissileSkillDefinitionSO definition_, HelicopterMissileSkillLevelData levelData_, Camera targetCamera_, Vector3 areaCenter_, Quaternion areaRotation_)
    {
        definition = definition_;
        levelData = levelData_;
        targetCamera = targetCamera_;
        areaCenter = areaCenter_;
        areaRotation = areaRotation_;

        int bufferSize = Mathf.Max(1, definition.DamageBufferSize);
        hitBuffer = new Collider[bufferSize];
        damagedTargets = new IDamageable[bufferSize];

        StartCoroutine(RunSkillCoroutine());
    }

    // 헬기를 이동시키면서 미사일을 순차 발사한다.
    private IEnumerator RunSkillCoroutine()
    {
        if (definition == null || levelData == null)
        {
            Destroy(gameObject);
            yield break;
        }

        Vector3 helicopterStart = ResolveViewportPoint(definition.HelicopterStartViewport);
        Vector3 helicopterEnd = ResolveViewportPoint(definition.HelicopterEndViewport);
        SpawnHelicopter(helicopterStart, helicopterEnd);

        float travelDistance = Vector3.Distance(helicopterStart, helicopterEnd);
        float travelDuration = travelDistance / Mathf.Max(0.1f, definition.HelicopterSpeed);
        float elapsedTime = 0f;
        int firedMissileCount = 0;
        float nextFireTime = 0f;

        while (elapsedTime < travelDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = travelDuration > 0f ? Mathf.Clamp01(elapsedTime / travelDuration) : 1f;

            if (helicopterInstance != null)
            {
                helicopterInstance.transform.position = Vector3.Lerp(helicopterStart, helicopterEnd, t);
            }

            if (firedMissileCount < definition.MissileCount && elapsedTime >= nextFireTime)
            {
                FireMissile(firedMissileCount);
                firedMissileCount++;
                nextFireTime = elapsedTime + definition.MissileInterval;
            }

            yield return null;
        }

        while (firedMissileCount < definition.MissileCount)
        {
            FireMissile(firedMissileCount);
            firedMissileCount++;
            yield return new WaitForSeconds(definition.MissileInterval);
        }

        if (helicopterInstance != null)
        {
            Destroy(helicopterInstance);
        }

        yield return WaitForMissileImpacts();

        Destroy(gameObject, definition.ExplosionEffectDuration + definition.SmokeDetachDuration + 0.5f);
    }

    // 모든 미사일 충돌 처리가 끝날 때까지 런타임 제거를 늦춘다.
    private IEnumerator WaitForMissileImpacts()
    {
        float timeout = Mathf.Max(3f, definition.HelicopterAltitude / Mathf.Max(0.1f, definition.MissileSpeed) + definition.MissileCount * definition.MissileInterval + 3f);
        float elapsedTime = 0f;

        while (activeMissileCount > 0 && elapsedTime < timeout)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    // 카메라 뷰포트 좌표를 헬기 고도 기준 월드 좌표로 변환한다.
    private Vector3 ResolveViewportPoint(Vector2 viewportPoint)
    {
        float flightY = areaCenter.y + definition.HelicopterAltitude;

        if (targetCamera == null)
        {
            return areaCenter + new Vector3(0f, definition.HelicopterAltitude, 0f);
        }

        Ray ray = targetCamera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));
        Plane flightPlane = new Plane(Vector3.up, new Vector3(0f, flightY, 0f));
        if (!flightPlane.Raycast(ray, out float enter))
        {
            return areaCenter + new Vector3(0f, definition.HelicopterAltitude, 0f);
        }

        return ray.GetPoint(enter);
    }

    // 헬기 프리팹을 생성하고 진행 방향으로 회전시킨다.
    private void SpawnHelicopter(Vector3 startPosition, Vector3 endPosition)
    {
        if (definition.HelicopterPrefab == null)
        {
            Debug.LogWarning("[헬기 스킬] 헬기 프리팹이 연결되지 않았습니다.", this);
            return;
        }

        Vector3 direction = endPosition - startPosition;
        Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : Quaternion.identity;
        rotation *= Quaternion.Euler(definition.HelicopterRotationOffsetEuler);

        helicopterInstance = Instantiate(definition.HelicopterPrefab, startPosition, rotation);
        ConfigurePropellerAnimator();
    }

    // 헬기 프리팹에 프로펠러 회전 컴포넌트를 자동 연결한다.
    private void ConfigurePropellerAnimator()
    {
        if (helicopterInstance == null || !definition.AutoAddPropellerAnimator)
        {
            return;
        }

        HelicopterPropellerAnimator animator = helicopterInstance.GetComponentInChildren<HelicopterPropellerAnimator>();
        if (animator == null)
        {
            animator = helicopterInstance.AddComponent<HelicopterPropellerAnimator>();
        }

        animator.Configure(definition.PropellerRotationSpeed, definition.PropellerLocalRotationAxis, definition.PropellerBindings);
    }

    // 지정된 순번의 미사일 목표점을 계산하고 미사일을 발사한다.
    private void FireMissile(int missileIndex)
    {
        if (definition.MissilePrefab == null)
        {
            Debug.LogWarning("[헬기 스킬] 미사일 비주얼 프리팹이 연결되지 않았습니다.", this);
            return;
        }

        Vector3 spawnPosition = helicopterInstance != null
            ? helicopterInstance.transform.TransformPoint(definition.MissileSpawnLocalOffset)
            : areaCenter + Vector3.up * definition.HelicopterAltitude;

        Vector3 targetPosition = ResolveMissileTargetPoint(missileIndex);
        Vector3 missileDirection = targetPosition - spawnPosition;
        Quaternion missileRotation = missileDirection.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(missileDirection.normalized, Vector3.up) : Quaternion.identity;
        GameObject missileObject = Instantiate(definition.MissilePrefab, spawnPosition, missileRotation);
        HelicopterMissileSkillProjectile projectile = missileObject.GetComponent<HelicopterMissileSkillProjectile>();

        if (projectile == null)
        {
            projectile = missileObject.AddComponent<HelicopterMissileSkillProjectile>();
        }

        activeMissileCount++;
        projectile.Initialize(
            targetPosition,
            definition.MissileSpeed,
            definition.ExplosionEffectPrefab,
            definition.ExplosionEffectDuration,
            definition.SmokeDetachDuration,
            OnMissileImpact);
    }

    // 범위 직사각형 내부에 미사일 목표점을 분산 배치한다.
    private Vector3 ResolveMissileTargetPoint(int missileIndex)
    {
        int count = Mathf.Max(1, definition.MissileCount);
        float lengthRatio = count == 1 ? 0.5f : missileIndex / (float)(count - 1);
        float lengthOffset = Mathf.Lerp(-0.45f, 0.45f, lengthRatio) * levelData.AreaLength;
        float widthOffset = Random.Range(-0.35f, 0.35f) * levelData.AreaWidth;
        Vector3 localOffset = new Vector3(widthOffset, definition.MissileTargetHeightOffset, lengthOffset);

        return areaCenter + areaRotation * localOffset;
    }

    // 미사일 충돌 시 폭발 데미지를 적용한다.
    private void OnMissileImpact(Vector3 impactPosition)
    {
        activeMissileCount = Mathf.Max(0, activeMissileCount - 1);

        if (definition.ApplyDamageOncePerCast)
        {
            if (hasAppliedCastDamage)
            {
                return;
            }

            hasAppliedCastDamage = true;
        }

        ApplyAreaDamage();
    }

    // 직사각형 범위 안의 일반 좀비와 보스 좀비에게 데미지를 적용한다.
    private void ApplyAreaDamage()
    {
        Vector3 boxCenter = areaCenter + Vector3.up * (definition.DamageBoxHeight * 0.5f);
        Vector3 halfExtents = new Vector3(levelData.AreaWidth * 0.5f, definition.DamageBoxHeight * 0.5f, levelData.AreaLength * 0.5f);
        int hitCount = Physics.OverlapBoxNonAlloc(boxCenter, halfExtents, hitBuffer, areaRotation, definition.DamageLayerMask, definition.DamageTriggerInteraction);
        int damagedCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hitBuffer[i];
            hitBuffer[i] = null;

            if (hitCollider == null)
            {
                continue;
            }

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            if (!IsValidDamageTarget(damageable) || ContainsDamagedTarget(damageable, damagedCount))
            {
                continue;
            }

            damageable.TakeDamage(levelData.Damage);
            if (damagedCount < damagedTargets.Length)
            {
                damagedTargets[damagedCount] = damageable;
                damagedCount++;
            }
        }

        ClearDamagedTargets(damagedCount);
    }

    // 스킬 타격 대상이 일반 좀비 또는 보스 좀비인지 확인한다.
    private bool IsValidDamageTarget(IDamageable damageable)
    {
        if (damageable == null || !damageable.IsAlive)
        {
            return false;
        }

        return damageable is NormalZombie || damageable is BossZombie;
    }

    // 한 번의 범위 판정에서 같은 대상이 중복 피격되는지 확인한다.
    private bool ContainsDamagedTarget(IDamageable damageable, int damagedCount)
    {
        for (int i = 0; i < damagedCount; i++)
        {
            if (damagedTargets[i] == damageable)
            {
                return true;
            }
        }

        return false;
    }

    // 중복 방지 배열을 다음 판정을 위해 비운다.
    private void ClearDamagedTargets(int damagedCount)
    {
        for (int i = 0; i < damagedCount; i++)
        {
            damagedTargets[i] = null;
        }
    }
}
