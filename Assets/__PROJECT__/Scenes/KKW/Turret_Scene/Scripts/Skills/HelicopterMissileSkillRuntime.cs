using System;
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
    private IDamageable[] castDamagedTargets;
    private int castDamagedTargetCount;
    private int activeMissileCount;
    private bool warnedCastDamageCapacityExceeded;
    private bool allMissilesImpacted;
    private Coroutine runningCoroutine;
    private Action<HelicopterMissileSkillRuntime> returnCallback;

    // 스킬 런타임 데이터를 초기화하고 연출 코루틴을 시작한다.
    public void Initialize(HelicopterMissileSkillDefinitionSO definition_, HelicopterMissileSkillLevelData levelData_, Camera targetCamera_, Vector3 areaCenter_, Quaternion areaRotation_, Action<HelicopterMissileSkillRuntime> returnCallback_)
    {
        StopRunningCoroutine();

        definition = definition_;
        levelData = levelData_;
        targetCamera = targetCamera_;
        areaCenter = areaCenter_;
        areaRotation = areaRotation_;
        returnCallback = returnCallback_;

        if (definition == null || levelData == null)
        {
            runningCoroutine = StartCoroutine(RunSkillCoroutine());
            return;
        }

        int bufferSize = Mathf.Max(1, definition.DamageBufferSize);
        int castDamageCapacity = bufferSize * Mathf.Max(1, definition.MissileCount);
        EnsureBuffers(bufferSize, castDamageCapacity);
        ResetCastState();

        runningCoroutine = StartCoroutine(RunSkillCoroutine());
    }

    // 비활성화될 때 진행 중인 코루틴과 참조 상태를 정리한다.
    private void OnDisable()
    {
        StopRunningCoroutine();
        ReturnActiveHelicopter();
        ResetReferences();
    }

    // 헬기를 이동시키면서 미사일을 순차 발사한다.
    private IEnumerator RunSkillCoroutine()
    {
        if (definition == null || levelData == null)
        {
            runningCoroutine = null;
            ReturnToPool();
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
            yield return WaitForMissileInterval();
        }

        if (helicopterInstance != null)
        {
            ReturnActiveHelicopter();
        }

        yield return WaitForMissileImpacts();

        runningCoroutine = null;
        if (this.allMissilesImpacted)
        {
            ReturnToPool();
            yield break;
        }

        Debug.LogWarning("[헬기 스킬] 일부 미사일 충돌 완료를 확인하지 못해 런타임을 풀에 반환하지 않고 제거합니다.", this);
        Destroy(gameObject, definition.ExplosionEffectDuration + definition.MissileDestroyDelayAfterImpact + 0.5f);
    }

    // 모든 미사일 충돌 처리가 끝날 때까지 런타임 제거를 늦춘다.
    private IEnumerator WaitForMissileImpacts()
    {
        float timeout = Mathf.Max(3f, definition.HelicopterAltitude / Mathf.Max(0.1f, definition.MissileRootMoveSpeed) + definition.MissileCount * definition.MissileInterval + 3f);
        float elapsedTime = 0f;

        while (activeMissileCount > 0 && elapsedTime < timeout)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        allMissilesImpacted = activeMissileCount <= 0;
    }

    // WaitForSeconds 할당 없이 미사일 발사 간격만큼 대기한다.
    private IEnumerator WaitForMissileInterval()
    {
        float interval = Mathf.Max(0f, definition.MissileInterval);
        float elapsedTime = 0f;

        while (elapsedTime < interval)
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

        helicopterInstance = PooledObjectUtility.Spawn(definition.HelicopterPrefab, startPosition, rotation);
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
        GameObject missileObject = PooledObjectUtility.SpawnProjectile(definition.MissilePrefab, spawnPosition, missileRotation);
        if (missileObject == null)
        {
            Debug.LogWarning("[헬기 스킬] 미사일 오브젝트 생성에 실패했습니다.", this);
            return;
        }

        HelicopterMissileSkillProjectile projectile = missileObject.GetComponent<HelicopterMissileSkillProjectile>();

        if (projectile == null)
        {
            projectile = missileObject.AddComponent<HelicopterMissileSkillProjectile>();
        }

        activeMissileCount++;
        projectile.Initialize(
            targetPosition,
            definition.MissileRootMoveSpeed,
            definition.ExplosionEffectPrefab,
            definition.ExplosionEffectDuration,
            definition.MissileDestroyDelayAfterImpact,
            definition.MissileImpactArmDelay,
            definition.MissileImpactMinTravelDistance,
            OnMissileImpact);
    }

    // 범위 직사각형 내부에 미사일 목표점을 분산 배치한다.
    private Vector3 ResolveMissileTargetPoint(int missileIndex)
    {
        int count = Mathf.Max(1, definition.MissileCount);
        float lengthOffset = 0f;
        float widthOffset = 0f;
        float effectiveAreaLength = GetMissileSpreadAreaLength();
        float effectiveAreaWidth = GetMissileSpreadAreaWidth();
        float lengthRange = effectiveAreaLength * Mathf.Clamp01(definition.MissileSpreadLengthRatio) * 0.5f;
        float widthRange = effectiveAreaWidth * Mathf.Clamp01(definition.MissileSpreadWidthRatio) * 0.5f;

        switch (definition.MissileSpreadMode)
        {
            case HelicopterMissileSpreadMode.evenLine:
                ResolveEvenLineMissileOffset(missileIndex, count, lengthRange, out lengthOffset);
                break;
            case HelicopterMissileSpreadMode.zigzag:
                ResolveZigzagMissileOffset(missileIndex, count, lengthRange, widthRange, out lengthOffset, out widthOffset);
                break;
            case HelicopterMissileSpreadMode.grid:
                ResolveGridMissileOffset(missileIndex, count, lengthRange, widthRange, out lengthOffset, out widthOffset);
                break;
            default:
                ResolveRandomMissileOffset(lengthRange, widthRange, out lengthOffset, out widthOffset);
                break;
        }

        Vector3 localOffset = new Vector3(widthOffset, definition.MissileTargetHeightOffset, lengthOffset);

        return areaCenter + areaRotation * localOffset;
    }

    // 미사일 착탄 분포에 사용할 전투 범위 폭을 반환한다.
    private float GetMissileSpreadAreaWidth()
    {
        return levelData.AreaWidth;
    }

    // 미사일 착탄 분포에 사용할 전투 범위 길이를 반환한다.
    private float GetMissileSpreadAreaLength()
    {
        return levelData.AreaLength;
    }

    // 표시 범위 안에서 무작위 착탄 위치를 계산한다.
    private void ResolveRandomMissileOffset(float lengthRange, float widthRange, out float lengthOffset, out float widthOffset)
    {
        lengthOffset = UnityEngine.Random.Range(-lengthRange, lengthRange);
        widthOffset = UnityEngine.Random.Range(-widthRange, widthRange);
    }

    // 표시 범위의 길이 방향으로 균등한 착탄 위치를 계산한다.
    private void ResolveEvenLineMissileOffset(int missileIndex, int count, float lengthRange, out float lengthOffset)
    {
        float ratio = count == 1 ? 0.5f : missileIndex / (float)(count - 1);
        lengthOffset = Mathf.Lerp(-lengthRange, lengthRange, ratio);
    }

    // 표시 범위의 길이 방향으로 균등 배치하고 폭 방향은 좌우 번갈아 배치한다.
    private void ResolveZigzagMissileOffset(int missileIndex, int count, float lengthRange, float widthRange, out float lengthOffset, out float widthOffset)
    {
        ResolveEvenLineMissileOffset(missileIndex, count, lengthRange, out lengthOffset);

        if (count <= 1)
        {
            widthOffset = 0f;
            return;
        }

        widthOffset = missileIndex % 2 == 0 ? -widthRange : widthRange;
    }

    // 표시 범위 안에서 행/열 형태의 착탄 위치를 계산한다.
    private void ResolveGridMissileOffset(int missileIndex, int count, float lengthRange, float widthRange, out float lengthOffset, out float widthOffset)
    {
        int rowCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
        int columnCount = Mathf.Max(1, Mathf.CeilToInt(count / (float)rowCount));
        int row = missileIndex / columnCount;
        int column = missileIndex % columnCount;
        float lengthRatio = rowCount == 1 ? 0.5f : row / (float)(rowCount - 1);
        float widthRatio = columnCount == 1 ? 0.5f : column / (float)(columnCount - 1);

        lengthOffset = Mathf.Lerp(-lengthRange, lengthRange, lengthRatio);
        widthOffset = Mathf.Lerp(-widthRange, widthRange, widthRatio);
    }

    // 미사일 충돌 시 폭발 데미지를 적용한다.
    private void OnMissileImpact(Vector3 impactPosition)
    {
        activeMissileCount = Mathf.Max(0, activeMissileCount - 1);

        ApplyImpactDamage(impactPosition);
    }

    // 미사일 착탄 위치를 중심으로 원형 폭발 데미지를 적용한다.
    private void ApplyImpactDamage(Vector3 impactPosition)
    {
        float damageRadius = Mathf.Max(0.1f, definition.MissileDamageRadius);
        int hitCount = Physics.OverlapSphereNonAlloc(impactPosition, damageRadius, hitBuffer, definition.DamageLayerMask, definition.DamageTriggerInteraction);
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
            if (!IsValidDamageTarget(damageable) || ContainsDamagedTarget(damageable, damagedTargets, damagedCount))
            {
                continue;
            }

            if (definition.ApplyDamageOncePerCast && ContainsDamagedTarget(damageable, castDamagedTargets, castDamagedTargetCount))
            {
                continue;
            }

            if (definition.ApplyDamageOncePerCast && !CanRegisterCastDamagedTarget())
            {
                continue;
            }

            damageable.TakeDamage(levelData.Damage);
            if (definition.ApplyDamageOncePerCast)
            {
                castDamagedTargets[castDamagedTargetCount] = damageable;
                castDamagedTargetCount++;
            }

            if (damagedCount < damagedTargets.Length)
            {
                damagedTargets[damagedCount] = damageable;
                damagedCount++;
            }
        }

        ClearDamagedTargets(damagedCount);
    }

    // 캐스트 전체 중복 방지 배열에 새 대상을 기록할 수 있는지 확인한다.
    private bool CanRegisterCastDamagedTarget()
    {
        if (castDamagedTargetCount < castDamagedTargets.Length)
        {
            return true;
        }

        if (!warnedCastDamageCapacityExceeded)
        {
            Debug.LogWarning("[헬기 스킬] 캐스트 중복 방지 배열이 가득 차 추가 데미지를 건너뜁니다. Damage Buffer Size 또는 Missile Count 설정을 확인해주세요.", this);
            warnedCastDamageCapacityExceeded = true;
        }

        return false;
    }

    // 스킬 타격 대상이 데미지를 받을 수 있는 생존 대상인지 확인한다.
    private bool IsValidDamageTarget(IDamageable damageable)
    {
        return damageable != null && damageable.IsAlive;
    }

    // 한 번의 범위 판정에서 같은 대상이 중복 피격되는지 확인한다.
    private bool ContainsDamagedTarget(IDamageable damageable, IDamageable[] targets, int targetCount)
    {
        for (int i = 0; i < targetCount; i++)
        {
            if (targets[i] == damageable)
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

    // 필요한 크기보다 작을 때만 판정 버퍼를 다시 만든다.
    private void EnsureBuffers(int bufferSize, int castDamageCapacity)
    {
        if (hitBuffer == null || hitBuffer.Length < bufferSize)
        {
            hitBuffer = new Collider[bufferSize];
        }

        if (damagedTargets == null || damagedTargets.Length < bufferSize)
        {
            damagedTargets = new IDamageable[bufferSize];
        }

        if (castDamagedTargets == null || castDamagedTargets.Length < castDamageCapacity)
        {
            castDamagedTargets = new IDamageable[castDamageCapacity];
        }
    }

    // 새 시전을 시작하기 전에 이전 시전 상태를 초기화한다.
    private void ResetCastState()
    {
        ClearHitBuffer();
        ClearDamagedTargets(damagedTargets != null ? damagedTargets.Length : 0);
        ClearCastDamagedTargets();
        castDamagedTargetCount = 0;
        activeMissileCount = 0;
        warnedCastDamageCapacityExceeded = false;
        allMissilesImpacted = false;
    }

    // OverlapSphereNonAlloc 재사용 버퍼의 남은 참조를 비운다.
    private void ClearHitBuffer()
    {
        if (hitBuffer == null)
        {
            return;
        }

        for (int i = 0; i < hitBuffer.Length; i++)
        {
            hitBuffer[i] = null;
        }
    }

    // 캐스트 전체 중복 방지 대상 참조를 비운다.
    private void ClearCastDamagedTargets()
    {
        if (castDamagedTargets == null)
        {
            return;
        }

        for (int i = 0; i < castDamagedTargetCount && i < castDamagedTargets.Length; i++)
        {
            castDamagedTargets[i] = null;
        }
    }

    // 진행 중인 스킬 코루틴을 중단한다.
    private void StopRunningCoroutine()
    {
        if (runningCoroutine == null)
        {
            return;
        }

        StopCoroutine(runningCoroutine);
        runningCoroutine = null;
    }

    // 풀 반환 시 다음 시전에 영향을 줄 수 있는 런타임 참조를 정리한다.
    private void ResetReferences()
    {
        definition = null;
        levelData = null;
        targetCamera = null;
        helicopterInstance = null;
        returnCallback = null;
        castDamagedTargetCount = 0;
        activeMissileCount = 0;
        warnedCastDamageCapacityExceeded = false;
        allMissilesImpacted = false;
    }

    // 남아 있는 헬기 연출 오브젝트를 풀 또는 제거 경로로 정리한다.
    private void ReturnActiveHelicopter()
    {
        if (helicopterInstance == null)
        {
            return;
        }

        PooledObjectUtility.ReturnOrDestroy(helicopterInstance);
        helicopterInstance = null;
    }

    // 스킬 런타임을 소유자 풀로 반환한다.
    private void ReturnToPool()
    {
        ClearHitBuffer();
        ClearDamagedTargets(damagedTargets != null ? damagedTargets.Length : 0);
        ClearCastDamagedTargets();

        Action<HelicopterMissileSkillRuntime> callback = returnCallback;
        if (callback != null)
        {
            callback(this);
            return;
        }

        gameObject.SetActive(false);
    }
}
