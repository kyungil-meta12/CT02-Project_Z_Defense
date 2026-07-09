using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// HOVL 투사체의 종료 처리 없이 피격 이펙트만 별도 인스턴스로 재생한다.
/// </summary>
public static class HovlProjectileHitEffectUtility
{
    private const BindingFlags FIELD_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static Type cachedMoverType;
    private static FieldInfo hitField;
    private static FieldInfo hitParticleField;
    private static FieldInfo hitOffsetField;
    private static FieldInfo useFirePointRotationField;
    private static FieldInfo rotationOffsetField;

    // HOVL 투사체의 피격 이펙트를 투사체 종료 없이 재생한다
    public static void Play(Hovl.HS_ProjectileMover projectileMover, Vector3 point, Vector3 normal)
    {
        if (projectileMover == null || !TryCacheFields(projectileMover.GetType()))
        {
            return;
        }

        GameObject hitPrefab = GetFieldValue<GameObject>(projectileMover, hitField);
        if (hitPrefab == null)
        {
            return;
        }

        ParticleSystem hitParticleSystem = GetFieldValue<ParticleSystem>(projectileMover, hitParticleField);
        float hitOffset = GetFieldValue<float>(projectileMover, hitOffsetField);
        bool useFirePointRotation = GetFieldValue<bool>(projectileMover, useFirePointRotationField);
        Vector3 rotationOffset = GetFieldValue<Vector3>(projectileMover, rotationOffsetField);
        Vector3 safeNormal = normal.sqrMagnitude <= 0.0001f ? -projectileMover.transform.forward : normal.normalized;
        Vector3 position = point + safeNormal * hitOffset;
        Quaternion rotation = ResolveHitRotation(projectileMover.transform, safeNormal, useFirePointRotation, rotationOffset);
        PooledObjectUtility.SpawnEffect(hitPrefab, position, rotation, ResolveHitDuration(hitParticleSystem));
    }

    // HOVL 타입의 피격 이펙트 필드 정보를 캐시한다
    private static bool TryCacheFields(Type moverType)
    {
        if (moverType == null)
        {
            return false;
        }

        if (cachedMoverType == moverType)
        {
            return hitField != null;
        }

        cachedMoverType = moverType;
        hitField = ResolveField(moverType, "hit");
        hitParticleField = ResolveField(moverType, "hitPS");
        hitOffsetField = ResolveField(moverType, "hitOffset");
        useFirePointRotationField = ResolveField(moverType, "UseFirePointRotation");
        rotationOffsetField = ResolveField(moverType, "rotationOffset");

        return hitField != null &&
               hitParticleField != null &&
               hitOffsetField != null &&
               useFirePointRotationField != null &&
               rotationOffsetField != null;
    }

    // 상속 계층을 따라 지정 이름의 필드를 찾는다
    private static FieldInfo ResolveField(Type type, string fieldName)
    {
        Type currentType = type;
        while (currentType != null)
        {
            FieldInfo fieldInfo = currentType.GetField(fieldName, FIELD_FLAGS);
            if (fieldInfo != null)
            {
                return fieldInfo;
            }

            currentType = currentType.BaseType;
        }

        return null;
    }

    // 캐시된 필드에서 지정 타입 값을 읽는다
    private static T GetFieldValue<T>(object target, FieldInfo fieldInfo)
    {
        if (target == null || fieldInfo == null)
        {
            return default;
        }

        object value = fieldInfo.GetValue(target);
        if (value is T typedValue)
        {
            return typedValue;
        }

        return default;
    }

    // HOVL 원본 옵션에 맞춰 피격 이펙트 회전을 계산한다
    private static Quaternion ResolveHitRotation(Transform projectileTransform, Vector3 normal, bool useFirePointRotation, Vector3 rotationOffset)
    {
        if (projectileTransform == null)
        {
            return Quaternion.identity;
        }

        if (useFirePointRotation)
        {
            return projectileTransform.rotation * Quaternion.Euler(0.0f, 180.0f, 0.0f);
        }

        if (rotationOffset != Vector3.zero)
        {
            return Quaternion.Euler(rotationOffset);
        }

        Vector3 lookDirection = normal;
        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            lookDirection = projectileTransform.forward;
        }

        return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    // 원본 피격 파티클 기준으로 이펙트 생존 시간을 계산한다
    private static float ResolveHitDuration(ParticleSystem hitParticleSystem)
    {
        if (hitParticleSystem == null)
        {
            return 1.0f;
        }

        return Mathf.Max(hitParticleSystem.main.duration, 0.05f);
    }

}
