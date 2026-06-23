using UnityEngine;

/// <summary>
/// 터렛 Definition이 선택한 상태이상 프로필을 받을 수 있는 보조 컴포넌트 계약이다.
/// </summary>
public interface ITurretStatusProfileReceiver
{
    // 터렛 Definition에서 선택한 상태이상 프로필, 현재 레벨, 성장 프로필을 전달한다
    void SetStatusProfile(ScriptableObject statusProfile, int level, TurretStatGrowthProfileSO growthProfile);
}
