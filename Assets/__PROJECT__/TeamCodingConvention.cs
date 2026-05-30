/// <summary>
/// 팀 코딩/협업 컨벤션 문서.
/// README 성격의 문서 전용 .cs 파일.
/// </summary>
public static class TeamCodingConvention
{
    /*
     * ============================================================
     * 1) 코드 컨벤션
     * ============================================================
     *
     * 1. 클래스/구조체/열거형/메서드/ScriptableObject 타입명:
     *    PascalCase
     *    - 예: MemoryPool, PoolObject, GetInstance
     *
     * 2. 필드/메서드 파라미터/열거형 값:
     *    camelCase
     *    - 파라미터명과 필드명이 충돌하면 파라미터 뒤에 '_'를 붙인다.
     *    - 예:
     *      void SetObjectType(Type objType_)
     *      {
     *          objType = objType_;
     *      }
     *
     * 3. static 변수명:
     *    PascalCase
     *    - 예: Inst
     *
     * 4. const 변수명:
     *    UPPER_CASE
     *    - 예: MAX_POOL_SIZE
     *
     * 5. 프로퍼티(Property) 이름:
     *    PascalCase
     *    - 예: public int CurrentHp { get; private set; }
     *
     * 6. 어트리뷰트 + 필드 선언:
     *    한 줄 수평 형식
     *    - 예: [HideInInspector] public int value;
     *
     * 7. 중괄호 사용:
     *    모든 조건문/반복문에서 항상 사용
     *    (if / else if / else / for / foreach / while)
     *
     * 8. 중괄호 스타일:
     *    Allman 스타일
     *    - 예:
     *      void MethodExample()
     *      {
     *          // source code...
     *      }
     *
     * 9. 헝가리안 표기:
     *    사용 금지
     *    - 금지 예: m_bActive, m_iAmmoCount
     *
     * 10. 람다 사용:
     *     꼭 필요한 경우에만 사용 (과도한 체이닝/중첩 지양)
     *
     * ------------------------------------------------------------
     * MemoryPool / PoolObject 적용 규칙
     * ------------------------------------------------------------
     *
     * A. 싱글톤 종료 안정성:
     *    OnDestroy()에서 (Inst == this)일 때만 Inst를 null로 설정한다.
     *
     * B. 풀 반환 안정성:
     *    ReturnInstance()에서 Push() 전에 OriginStack null 방어를 한다.
     *
     * C. 입력값 안정성:
     *    GetInstance / CreateInstance / Prewarm에서 prefab null을 방어한다.
     *
     * D. 타입 불일치 가시성:
     *    GetComponent<T>() 실패 시 즉시 LogError로 원인을 노출한다.
     *
     * E. 런타임 오브젝트 제거 정책:
     *    중복 싱글톤 제거는 Destroy를 사용하고, 런타임 흐름에서
     *    DestroyImmediate 사용은 지양한다.
     *
     * F. 풀 객체 초기화 타이밍 (중요):
     *    풀에서 꺼낸 모든 객체는 사용 전에 반드시 Reset/Init를 호출한다.
     *    (체력, 위치/회전, 속도, 타이머, 런타임 컴포넌트 상태 포함)
     *
     * G. 초기화 호출 순서:
     *    이전 생명주기 데이터에 의존하지 않도록 아래 순서를 권장한다.
     *    Pop -> 위치/데이터 설정 -> Reset/Init -> SetActive(true)
     *
     * ============================================================
     * 2) Unity Editor 컨벤션
     * ============================================================
     *
     * 1. 모든 GameObject / Prefab 이름:
     *    PascalCase
     *
     * 2. 동적 오브젝트 Hierarchy 관리:
     *    Instantiate / GetInstance로 생성된 객체는 타입별 전용 컨테이너
     *    부모 하위로 정리한다.
     *    - 예: SpawnedBullets, SpawnedZombies
     *
     * ============================================================
     * 3) 협업 프로세스
     * ============================================================
     *
     * 1. 메인 씬 작업 전/후 디스코드에 공유한다.
     * 2. 오전 데일리 스크럼에 당일 작업 계획을 간단히 공유한다.
     *
     * ============================================================
     * 4) 성능 / GC 가이드
     * ============================================================
     *
     * 1. GC 할당 최소화:
     *    배열/클래스/컬렉션을 가능한 재사용한다.
     *
     * 2. 고빈도 루프 규칙:
     *    Update/FixedUpdate 등 핫패스에서 내부적으로 새 배열을 반환하는
     *    할당형 Unity API 사용을 지양한다.
     *    - 지양 예: GetComponents, FindGameObjectsWithTag, Physics.OverlapSphere
     *
     * 3. NonAlloc + 캐시 버퍼 사용:
     *    NonAlloc 계열 API와 캐시된 배열 버퍼를 우선 사용한다.
     *    - 예: Physics.OverlapSphereNonAlloc
     */
}
