using UnityEngine;

/// <summary>
/// 데미지 팝업 렌더링 백엔드가 구현해야 하는 공통 계약.
/// </summary>
public interface IDamagePopupRenderBackend
{
    // 백엔드가 현재 설정으로 팝업을 표시할 수 있는지 반환한다
    bool IsAvailable { get; }

    // 백엔드가 사용하는 풀과 런타임 리소스를 미리 준비한다
    void Prewarm();

    // 지정 위치에 데미지 팝업을 표시한다
    bool TrySpawn(int damageValue, Vector3 position, DamagePopupType damageType, Camera targetCamera);
}
