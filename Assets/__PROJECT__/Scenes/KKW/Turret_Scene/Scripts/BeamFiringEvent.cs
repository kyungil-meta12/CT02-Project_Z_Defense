using ProjectZima.PolygonModularTurretsPack;
using UnityEngine;
using ProjectZDefense.StatusEffects;

/// <summary>
/// 투사체를 생성하지 않고 총구와 타겟 사이에 빔 VFX를 유지하며 빔 공격 규칙과 선택적 Frost 상태 효과를 적용한다.
/// </summary>
public class BeamFiringEvent : FiringEvent
{
    [SerializeField] private GameObject beamPrefab;
    [SerializeField] private BeamAttackProfileSO attackProfile;
    [SerializeField] private FrostStatusProfileSO frostStatusProfile;
    [SerializeField, Min(0.01f)] private float beamVisibleDuration = 0.15f;
    [SerializeField, Min(0.01f)] private float minBeamDistance = 0.1f;
    [SerializeField, Min(0.01f)] private float targetValidationInterval = 0.1f;
    [SerializeField] private bool useTargetAimPoint = true;

    private BeamInstance[] beamInstances;
    private GameObject currentTarget;
    private TargetFinder targetFinder;
    private IDamageable currentDamageable;
    private RaycastHit[] damageHitBuffer;
    private IDamageable[] damageTargetBuffer;
    private float targetValidationTimer;
    private float invalidTargetTimer;
    private float damageTickTimer;
    private float projectileScale = 1.0f;
    private float currentProjectileDamage;
    private bool currentLogProjectileDamage;
    private int frostStatusLevel = 1;

    // 외부 VFX 프로필에서 사용할 빔 프리팹을 설정한다
    public void SetBeamPrefab(GameObject beamPrefab_)
    {
        if (beamPrefab == beamPrefab_)
        {
            return;
        }

        beamPrefab = beamPrefab_;
        ClearBeamInstances();
    }

    // 외부 프로필에서 사용할 빔 공격 규칙을 설정한다
    public void SetAttackProfile(BeamAttackProfileSO attackProfile_)
    {
        attackProfile = attackProfile_;
        EnsureDamageBuffers();
    }

    // 외부 터렛 정의에서 사용할 Frost 상태 프로필과 현재 레벨을 설정한다
    public void SetFrostStatusProfile(FrostStatusProfileSO frostStatusProfile_, int level)
    {
        frostStatusProfile = frostStatusProfile_;
        frostStatusLevel = Mathf.Max(1, level);
    }

    // 런타임 projectile scale 진행 값을 빔 스케일에도 반영한다
    public void SetProjectileScale(float scale)
    {
        projectileScale = Mathf.Max(0.01f, scale);
    }

    // 빔 발사 요청마다 현재 타겟과 빔 유지 상태를 갱신한다
    public override void Fire(GameObject projectilePrefab, GameObject target, float projectileSpeed, float projectileScale_, float projectileDamage, int projectilePierceCount, bool logProjectileDamage, PoisonStatusPayload poisonStatusPayload, ElectroStatusPayload electroStatusPayload)
    {
        if (beamPrefab == null || target == null || !target.activeInHierarchy)
        {
            HideBeamInstances();
            return;
        }

        SetProjectileScale(projectileScale_);
        EnsureBeamInstances();
        bool targetChanged = currentTarget != target;
        currentTarget = target;
        currentDamageable = ResolveDamageable(target);
        currentProjectileDamage = projectileDamage;
        currentLogProjectileDamage = logProjectileDamage;
        targetValidationTimer = 0.0f;
        invalidTargetTimer = 0.0f;

        if (targetChanged)
        {
            damageTickTimer = 0.0f;
        }

        UpdateBeamInstances();

        if (attackProfile == null)
        {
            ApplyDamage(currentDamageable, projectileDamage, logProjectileDamage);
            return;
        }

        if (damageTickTimer <= 0.0f)
        {
            ApplyBeamDamageTick();
            damageTickTimer = GetDamageTickInterval();
        }
    }

    // 매 프레임 활성 빔의 위치와 길이, 빔 데미지 틱을 갱신한다
    private void Update()
    {
        if (beamInstances == null)
        {
            return;
        }

        if (!IsCurrentTargetValid())
        {
            invalidTargetTimer += Time.deltaTime;
            if (invalidTargetTimer > beamVisibleDuration)
            {
                HideBeamInstances();
            }

            return;
        }

        invalidTargetTimer = 0.0f;
        UpdateBeamInstances();
        UpdateDamageTick(Time.deltaTime);
    }

    // 비활성화될 때 남아 있는 빔 인스턴스를 숨긴다
    private void OnDisable()
    {
        HideBeamInstances();
    }

    // 제거될 때 생성한 빔 인스턴스를 정리한다
    private void OnDestroy()
    {
        ClearBeamInstances();
    }

    // 설정된 총구 수에 맞춰 빔 인스턴스를 준비한다
    private void EnsureBeamInstances()
    {
        if (gunPrefabs == null || gunPrefabs.Length == 0 || beamPrefab == null)
        {
            return;
        }

        if (beamInstances != null && beamInstances.Length == gunPrefabs.Length)
        {
            return;
        }

        ClearBeamInstances();
        beamInstances = new BeamInstance[gunPrefabs.Length];

        for (int i = 0; i < gunPrefabs.Length; i++)
        {
            GameObject gunObject = gunPrefabs[i];
            if (gunObject == null)
            {
                continue;
            }

            Transform muzzleTransform = ResolveMuzzleTransform(gunObject);
            if (muzzleTransform == null)
            {
                continue;
            }

            GameObject beamObject = Instantiate(beamPrefab, muzzleTransform.position, muzzleTransform.rotation, muzzleTransform);
            beamObject.SetActive(false);
            beamInstances[i] = new BeamInstance(
                beamObject,
                muzzleTransform,
                ResolveBeamTarget(beamObject.transform),
                ResolveBeamHitEffect(beamObject.transform));
        }
    }

    // 현재 빔 타겟이 계속 공격 가능한 상태인지 검사한다
    private bool IsCurrentTargetValid()
    {
        if (currentTarget == null || !currentTarget.activeInHierarchy)
        {
            return false;
        }

        if (currentDamageable == null || !currentDamageable.IsAlive)
        {
            return false;
        }

        targetValidationTimer += Time.deltaTime;
        if (targetValidationTimer < targetValidationInterval)
        {
            return true;
        }

        targetValidationTimer = 0.0f;
        return IsCurrentTargetInRange();
    }

    // 현재 타겟이 타겟 파인더 사거리 안에 있는지 확인한다
    private bool IsCurrentTargetInRange()
    {
        TargetFinder finder = GetTargetFinder();
        if (finder == null || finder.radius <= 0.0f)
        {
            return true;
        }

        Vector3 origin = finder.pivotObject != null ? finder.pivotObject.transform.position : transform.position;
        Vector3 targetPosition = ResolveTargetPosition(currentTarget);

        if (finder.useHorizontalDistance)
        {
            Vector3 horizontalOffset = new Vector3(targetPosition.x - origin.x, 0.0f, targetPosition.z - origin.z);
            return horizontalOffset.sqrMagnitude <= finder.radius * finder.radius;
        }

        return (targetPosition - origin).sqrMagnitude <= finder.radius * finder.radius;
    }

    // 같은 터렛에 붙은 타겟 파인더를 캐시해서 반환한다
    private TargetFinder GetTargetFinder()
    {
        if (targetFinder == null)
        {
            targetFinder = GetComponent<TargetFinder>();
        }

        return targetFinder;
    }

    // 모든 빔 인스턴스의 위치, 방향, 길이를 현재 타겟에 맞춘다
    private void UpdateBeamInstances()
    {
        if (beamInstances == null || currentTarget == null)
        {
            return;
        }

        Vector3 targetPosition = ResolveTargetPosition(currentTarget);
        for (int i = 0; i < beamInstances.Length; i++)
        {
            BeamInstance beamInstance = beamInstances[i];
            if (!beamInstance.IsValid)
            {
                continue;
            }

            UpdateBeamInstance(beamInstance, targetPosition);
        }
    }

    // 단일 빔 인스턴스를 총구에서 타겟 위치까지 이어지도록 갱신한다
    private void UpdateBeamInstance(BeamInstance beamInstance, Vector3 targetPosition)
    {
        Vector3 startPosition = beamInstance.MuzzleTransform.position;
        Vector3 direction = targetPosition - startPosition;
        float distance = Mathf.Max(minBeamDistance, direction.magnitude);
        Vector3 safeDirection = direction.sqrMagnitude > 0.0001f ? direction / distance : beamInstance.MuzzleTransform.forward;

        Transform beamTransform = beamInstance.BeamObject.transform;
        beamTransform.position = startPosition;
        beamTransform.rotation = Quaternion.FromToRotation(Vector3.left, safeDirection);
        ApplyBeamScale(beamTransform);

        if (beamInstance.TargetTransform != null)
        {
            beamInstance.TargetTransform.position = targetPosition;
        }

        if (beamInstance.HitEffectTransform != null)
        {
            beamInstance.HitEffectTransform.position = targetPosition;
        }

        if (!beamInstance.BeamObject.activeSelf)
        {
            beamInstance.BeamObject.SetActive(true);
        }
    }

    // 빔 루트에는 진행도 스케일만 균일하게 적용한다
    private void ApplyBeamScale(Transform beamTransform)
    {
        beamTransform.localScale = Vector3.one * projectileScale;
    }

    // 빔 공격 프로필 기준으로 데미지 틱 시간을 누적하고 적용한다
    private void UpdateDamageTick(float deltaTime)
    {
        if (attackProfile == null)
        {
            return;
        }

        damageTickTimer -= deltaTime;
        if (damageTickTimer > 0.0f)
        {
            return;
        }

        ApplyBeamDamageTick();
        damageTickTimer += GetDamageTickInterval();
    }

    // 현재 빔 공격 모드에 맞춰 데미지 틱을 적용한다
    private void ApplyBeamDamageTick()
    {
        if (attackProfile == null)
        {
            ApplyDamage(currentDamageable, currentProjectileDamage, currentLogProjectileDamage);
            return;
        }

        switch (attackProfile.targetMode)
        {
            case BeamAttackTargetMode.PierceLine:
                ApplyPierceLineDamage();
                break;
            case BeamAttackTargetMode.ChainNearest:
                ApplyDamage(currentDamageable, GetProfileDamage(), currentLogProjectileDamage);
                break;
            default:
                ApplyDamage(currentDamageable, GetProfileDamage(), currentLogProjectileDamage);
                break;
        }
    }

    // 빔 시작점과 현재 타겟 사이의 라인에 있는 대상들에게 관통 데미지를 적용한다
    private void ApplyPierceLineDamage()
    {
        if (beamInstances == null || beamInstances.Length == 0 || !beamInstances[0].IsValid || currentTarget == null)
        {
            ApplyDamage(currentDamageable, GetProfileDamage(), currentLogProjectileDamage);
            return;
        }

        EnsureDamageBuffers();
        ResetDamageTargetBuffer();

        Vector3 startPosition = beamInstances[0].MuzzleTransform.position;
        Vector3 targetPosition = ResolveTargetPosition(currentTarget);
        Vector3 direction = targetPosition - startPosition;
        float distance = direction.magnitude;
        if (distance <= minBeamDistance)
        {
            ApplyDamage(currentDamageable, GetProfileDamage(), currentLogProjectileDamage);
            return;
        }

        Vector3 normalizedDirection = direction / distance;
        int hitCount = Physics.SphereCastNonAlloc(
            startPosition,
            attackProfile.pierceRadius,
            normalizedDirection,
            damageHitBuffer,
            distance,
            attackProfile.damageLayerMask,
            attackProfile.triggerInteraction);

        int maxTargets = Mathf.Min(attackProfile.maxTargets, damageTargetBuffer.Length);
        for (int i = 0; i < hitCount && GetDamageTargetCount() < maxTargets; i++)
        {
            Collider hitCollider = damageHitBuffer[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            IDamageable damageable = ResolveDamageable(hitCollider.gameObject);
            if (damageable == null || !damageable.IsAlive)
            {
                continue;
            }

            AddDamageTarget(damageable);
        }

        if (GetDamageTargetCount() == 0)
        {
            AddDamageTarget(currentDamageable);
        }

        float damage = GetProfileDamage();
        for (int i = 0; i < GetDamageTargetCount(); i++)
        {
            ApplyDamage(damageTargetBuffer[i], damage, currentLogProjectileDamage);
        }
    }

    // 현재 발사 틱의 타겟에게 한 번 데미지를 적용한다
    private void ApplyDamage(IDamageable damageable, float projectileDamage, bool logProjectileDamage)
    {
        if (damageable == null || !damageable.IsAlive)
        {
            return;
        }

        float safeDamage = Mathf.Max(0.0f, projectileDamage);
        NotifyNonElectroDamageReceived(damageable, safeDamage);
        damageable.TakeDamage(safeDamage);
        ApplyFrostStatus(damageable);

        if (logProjectileDamage)
        {
            Debug.Log($"[BeamFiringEvent] 빔 데미지 적용: {safeDamage:0.###}", this);
        }
    }

    // 프로필 설정에 따라 이번 틱에 적용할 데미지를 계산한다
    private float GetProfileDamage()
    {
        if (attackProfile == null)
        {
            return Mathf.Max(0.0f, currentProjectileDamage);
        }

        float baseDamage = Mathf.Max(0.0f, currentProjectileDamage);
        if (attackProfile.treatTurretDamageAsDps)
        {
            baseDamage *= GetDamageTickInterval();
        }

        return baseDamage * Mathf.Max(0.0f, attackProfile.damageMultiplier);
    }

    // 현재 프로필의 데미지 틱 간격을 반환한다
    private float GetDamageTickInterval()
    {
        if (attackProfile == null)
        {
            return 0.0f;
        }

        return Mathf.Max(0.01f, attackProfile.damageTickInterval);
    }

    // Frost 상태 효과를 받을 수 있는 대상이면 슬로우와 빙결 값을 전달한다
    private void ApplyFrostStatus(IDamageable damageable)
    {
        if (frostStatusProfile == null || !frostStatusProfile.HasFrostStatus)
        {
            return;
        }

        IFrostStatusEffectReceiver frostReceiver = damageable as IFrostStatusEffectReceiver;
        if (frostReceiver == null)
        {
            return;
        }

        frostReceiver.ApplyFrostStatus(frostStatusProfile.CreatePayload(frostStatusLevel, GetDamageTickInterval()));
    }

    // 관통 판정에 사용할 버퍼 배열을 준비한다
    private void EnsureDamageBuffers()
    {
        if (attackProfile == null)
        {
            return;
        }

        int bufferSize = Mathf.Max(1, attackProfile.damageBufferSize);
        if (damageHitBuffer == null || damageHitBuffer.Length != bufferSize)
        {
            damageHitBuffer = new RaycastHit[bufferSize];
        }

        int targetBufferSize = Mathf.Max(1, attackProfile.maxTargets);
        if (damageTargetBuffer == null || damageTargetBuffer.Length != targetBufferSize)
        {
            damageTargetBuffer = new IDamageable[targetBufferSize];
        }
    }

    // 관통 데미지 대상 버퍼를 초기화한다
    private void ResetDamageTargetBuffer()
    {
        if (damageTargetBuffer == null)
        {
            return;
        }

        for (int i = 0; i < damageTargetBuffer.Length; i++)
        {
            damageTargetBuffer[i] = null;
        }
    }

    // 데미지 대상 버퍼에 중복 없이 대상을 추가한다
    private void AddDamageTarget(IDamageable damageable)
    {
        if (damageable == null || damageTargetBuffer == null)
        {
            return;
        }

        for (int i = 0; i < damageTargetBuffer.Length; i++)
        {
            if (damageTargetBuffer[i] == damageable)
            {
                return;
            }
        }

        for (int i = 0; i < damageTargetBuffer.Length; i++)
        {
            if (damageTargetBuffer[i] != null)
            {
                continue;
            }

            damageTargetBuffer[i] = damageable;
            return;
        }
    }

    // 현재 데미지 대상 버퍼에 들어 있는 유효 대상 수를 반환한다
    private int GetDamageTargetCount()
    {
        if (damageTargetBuffer == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < damageTargetBuffer.Length; i++)
        {
            if (damageTargetBuffer[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    // 타겟 오브젝트에서 데미지를 받을 수 있는 컴포넌트를 찾는다
    private IDamageable ResolveDamageable(GameObject target)
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

    // 타겟의 조준 기준 위치를 반환한다
    private Vector3 ResolveTargetPosition(GameObject target)
    {
        if (useTargetAimPoint)
        {
            return TurretAimPointUtility.GetAimPosition(target);
        }

        return target.transform.position;
    }

    // 총구 프리팹에서 실제 발사 위치로 사용할 트랜스폼을 찾는다
    private Transform ResolveMuzzleTransform(GameObject gunObject)
    {
        Gun gun = gunObject.GetComponent<Gun>();
        if (gun != null && gun.muzzleObject != null)
        {
            return gun.muzzleObject.transform;
        }

        return gunObject.transform;
    }

    // 빔 프리팹 내부의 끝점 타겟 트랜스폼을 찾는다
    private Transform ResolveBeamTarget(Transform beamRoot)
    {
        Transform beamEmitterTarget = ResolveBeamEmitterTransformField(beamRoot, "beamTarget");
        if (beamEmitterTarget != null)
        {
            return beamEmitterTarget;
        }

        Transform[] children = beamRoot.GetComponentsInChildren<Transform>(true);
        Transform namedTarget = FindChildByName(children, beamRoot, "holder_Main");
        if (namedTarget != null)
        {
            return namedTarget;
        }

        Transform fallbackTarget = null;

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == beamRoot || child.name != "holder")
            {
                continue;
            }

            if (child.localPosition.x < -0.01f)
            {
                return child;
            }

            fallbackTarget = child;
        }

        return fallbackTarget;
    }

    // 빔 프리팹 내부의 피격 이펙트 트랜스폼을 찾는다
    private Transform ResolveBeamHitEffect(Transform beamRoot)
    {
        Transform beamEmitterHitEffect = ResolveBeamEmitterTransformField(beamRoot, "beamTargetHitFX");
        if (beamEmitterHitEffect != null)
        {
            return beamEmitterHitEffect;
        }

        Transform[] children = beamRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != beamRoot && child.name.Contains("Hit"))
            {
                return child;
            }
        }

        return null;
    }

    // 캐시된 트랜스폼 배열에서 지정한 이름의 하위 오브젝트를 찾는다
    private Transform FindChildByName(Transform[] children, Transform root, string targetName)
    {
        if (children == null)
        {
            return null;
        }

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == root)
            {
                continue;
            }

            if (child.name == targetName)
            {
                return child;
            }
        }

        return null;
    }

    // BeamEmitter 컴포넌트의 직렬화된 Transform 필드를 반사로 가져온다
    private Transform ResolveBeamEmitterTransformField(Transform beamRoot, string fieldName)
    {
        MonoBehaviour[] behaviours = beamRoot.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            System.Type behaviourType = behaviour.GetType();
            if (behaviourType.Name != "BeamEmitter")
            {
                continue;
            }

            System.Reflection.FieldInfo fieldInfo = behaviourType.GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (fieldInfo == null)
            {
                continue;
            }

            return fieldInfo.GetValue(behaviour) as Transform;
        }

        return null;
    }

    // 생성된 빔 인스턴스를 모두 숨긴다
    private void HideBeamInstances()
    {
        currentTarget = null;
        currentDamageable = null;
        targetValidationTimer = 0.0f;
        invalidTargetTimer = 0.0f;
        damageTickTimer = 0.0f;

        if (beamInstances == null)
        {
            return;
        }

        for (int i = 0; i < beamInstances.Length; i++)
        {
            BeamInstance beamInstance = beamInstances[i];
            if (beamInstance.BeamObject != null)
            {
                beamInstance.BeamObject.SetActive(false);
            }
        }
    }

    // 생성된 빔 인스턴스를 모두 제거한다
    private void ClearBeamInstances()
    {
        if (beamInstances == null)
        {
            return;
        }

        for (int i = 0; i < beamInstances.Length; i++)
        {
            BeamInstance beamInstance = beamInstances[i];
            if (beamInstance.BeamObject == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(beamInstance.BeamObject);
            }
            else
            {
                DestroyImmediate(beamInstance.BeamObject);
            }
        }

        beamInstances = null;
    }

    // 빔 인스턴스에 필요한 런타임 참조를 보관한다
    private struct BeamInstance
    {
        public readonly GameObject BeamObject;
        public readonly Transform MuzzleTransform;
        public readonly Transform TargetTransform;
        public readonly Transform HitEffectTransform;

        public bool IsValid
        {
            get
            {
                return BeamObject != null && MuzzleTransform != null;
            }
        }

        // 빔 인스턴스 참조를 초기화한다
        public BeamInstance(GameObject beamObject, Transform muzzleTransform, Transform targetTransform, Transform hitEffectTransform)
        {
            BeamObject = beamObject;
            MuzzleTransform = muzzleTransform;
            TargetTransform = targetTransform;
            HitEffectTransform = hitEffectTransform;
        }
    }

    // Beam 계열 비-Electro 피해가 적용되기 전 대상에게 Overload 발동 검사를 요청한다
    private static void NotifyNonElectroDamageReceived(IDamageable damageable, float appliedDamage)
    {
        if (damageable == null || !damageable.IsAlive)
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
}
