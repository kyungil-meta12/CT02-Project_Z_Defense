using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 터렛에 배치된 엔지니어 수를 관리하고 공격력 버프를 런타임 스탯에 반영한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TurretStatProfileApplier))]
public class TurretEngineerBuffReceiver : MonoBehaviour
{
    [Header("엔지니어 버프")]
    [SerializeField, Min(0f)] private float damageBonusRatioPerEngineer = 0.1f;
    [SerializeField] private TurretStatProfileApplier statProfileApplier;
    [SerializeField] private TurretDefinitionRuntimeController runtimeController;

    [Header("버프 상태")]
    public int currentEngineerCount;
    public float currentDamageBonusRatio;
    public float currentDamageMultiplier = 1.0f;

    private readonly List<Survivor> engineers = new List<Survivor>(4);

    public int EngineerCount => engineers.Count;

    // 컴포넌트 추가 시 필요한 참조를 자동으로 수집한다
    private void Reset()
    {
        RefreshReferences();
    }

    // 시작 전에 필요한 참조를 수집한다
    private void Awake()
    {
        RefreshReferences();
    }

    // 비활성화 시 등록된 엔지니어 배치를 해제한다
    private void OnDisable()
    {
        ReleaseAllEngineers();
        ApplyBuff();
    }

    // 엔지니어를 중복 없이 등록하고 버프를 갱신한다
    public bool TryRegisterEngineer(Survivor engineer)
    {
        if (engineer == null)
        {
            return false;
        }

        for (int i = 0; i < engineers.Count; i++)
        {
            if (engineers[i] == engineer)
            {
                return false;
            }
        }

        engineers.Add(engineer);
        ApplyBuff();
        return true;
    }

    // 엔지니어 등록을 해제하고 버프를 갱신한다
    public void UnregisterEngineer(Survivor engineer)
    {
        if (engineer == null)
        {
            return;
        }

        if (engineers.Remove(engineer))
        {
            ApplyBuff();
        }
    }

    // 엔지니어 수에 따른 공격력 배율을 터렛에 적용한다
    private void ApplyBuff()
    {
        RefreshReferences();

        currentEngineerCount = engineers.Count;
        currentDamageBonusRatio = Mathf.Max(0.0f, damageBonusRatioPerEngineer) * currentEngineerCount;
        currentDamageMultiplier = 1.0f + currentDamageBonusRatio;

        if (statProfileApplier == null)
        {
            return;
        }

        statProfileApplier.SetDamageMultiplier(currentDamageMultiplier);

        if (runtimeController != null)
        {
            runtimeController.Apply();
        }
    }

    // 등록된 모든 엔지니어에게 배치 해제를 알리고 목록을 비운다
    private void ReleaseAllEngineers()
    {
        for (int i = engineers.Count - 1; i >= 0; i--)
        {
            Survivor engineer = engineers[i];
            if (engineer != null)
            {
                engineer.ReleaseEngineerAssignment(this);
            }
        }

        engineers.Clear();
    }

    // 필요한 터렛 관련 컴포넌트 참조를 수집한다
    private void RefreshReferences()
    {
        if (statProfileApplier == null)
        {
            statProfileApplier = GetComponent<TurretStatProfileApplier>();
        }

        if (runtimeController == null)
        {
            runtimeController = GetComponent<TurretDefinitionRuntimeController>();
        }
    }
}
