using UnityEngine;

/// <summary>
/// 타겟 루트가 터렛 조준에 사용할 캐시된 기준점을 제공하는 계약이다.
/// </summary>
public interface IAimPointProvider
{
    // 지정한 높이 비율에 맞는 터렛 조준 위치를 반환한다
    Vector3 GetAimPosition(float aimHeightRatio);
}
