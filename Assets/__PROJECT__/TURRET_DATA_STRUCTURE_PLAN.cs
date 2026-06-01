/*
 * Turret Data Structure Plan
 *
 * 목적
 * - 터렛 강화 단계에 따라 스탯, 발사체/VFX, 사운드, 외형 파츠가 바뀌는 구조를 정리한다.
 * - 팀장 담당 로직/밸런스 데이터와 규원 담당 연출 데이터를 분리해 작업 충돌을 줄인다.
 * - 최종적으로 터렛은 하나의 상위 설정을 읽고, 내부에서 스탯/연출/파츠 데이터를 나누어 적용한다.
 *
 * 기본 방향
 * - 스탯 SO와 VFX SO는 서로를 직접 참조하지 않는다.
 * - 강화 단계 SO 또는 터렛 정의 SO가 스탯/VFX/파츠 프로필을 묶는다.
 * - VFX 프로필에는 데미지, 공격 속도, 사거리 같은 밸런스 값을 넣지 않는다.
 * - 스탯 프로필에는 projectile prefab, muzzle VFX, hit VFX 같은 연출 값을 넣지 않는다.
 *
 * 추천 ScriptableObject 구조
 *
 * 1. TurretDefinitionSO
 * - 터렛 하나의 최상위 정의 데이터.
 * - 터렛 ID
 * - 표시 이름
 * - 기본 터렛 프리팹
 * - 강화 단계 목록
 *
 * 예시:
 *
 * public class TurretDefinitionSO : ScriptableObject
 * {
 *     public string turretId;
 *     public string displayName;
 *     public GameObject basePrefab;
 *     public TurretUpgradeLevelSO[] upgradeLevels;
 * }
 *
 * 2. TurretUpgradeLevelSO
 * - 특정 강화 단계에서 사용할 데이터를 묶는 단계별 config.
 * - 레벨
 * - 스탯 프로필
 * - VFX 프로필
 * - 파츠 프로필
 * - 강화 비용/해금 조건
 *
 * 예시:
 *
 * public class TurretUpgradeLevelSO : ScriptableObject
 * {
 *     public int level;
 *     public TurretStatProfileSO statProfile;
 *     public TurretVFXProfileSO vfxProfile;
 *     public TurretPartsProfileSO partsProfile;
 *     public int upgradeCost;
 * }
 *
 * 3. TurretStatProfileSO
 * - 게임 로직과 밸런스에 필요한 수치 데이터.
 * - 팀장 담당 영역.
 * - 데미지
 * - 사거리
 * - 공격 속도
 * - 탄속
 * - 투사체 수
 * - 관통 수
 * - 치명타 관련 값
 *
 * 예시:
 *
 * public class TurretStatProfileSO : ScriptableObject
 * {
 *     public float damage;
 *     public float range;
 *     public float fireInterval;
 *     public float projectileSpeed;
 *     public int projectileCount;
 *     public int pierceCount;
 * }
 *
 * 4. TurretVFXProfileSO
 * - 터렛 발사 연출에 필요한 데이터.
 * - 규원 담당 영역.
 * - projectile prefab
 * - muzzle VFX
 * - hit VFX
 * - fire sound
 * - impact sound
 * - VFX duration
 * - 화면에서 보이는 연출용 scale/color 보정값
 *
 * 예시:
 *
 * public class TurretVFXProfileSO : ScriptableObject
 * {
 *     public string displayName;
 *     public GameObject projectilePrefab;
 *     public GameObject muzzleVFX;
 *     public GameObject hitVFX;
 *     public AudioClip fireSound;
 *     public AudioClip impactSound;
 *     public float muzzleVFXDuration;
 *     public float hitVFXDuration;
 * }
 *
 * 5. TurretPartsProfileSO
 * - 강화 단계별로 붙거나 사라지는 외형 파츠 데이터.
 * - 규원 담당 영역.
 * - 추가 파츠 prefab
 * - 부착할 socket 이름
 * - local position/rotation/scale
 * - 기존 파츠 활성/비활성 옵션
 *
 * 예시:
 *
 * public class TurretPartsProfileSO : ScriptableObject
 * {
 *     public TurretPartEntry[] parts;
 * }
 *
 * [System.Serializable]
 * public class TurretPartEntry
 * {
 *     public string socketName;
 *     public GameObject partPrefab;
 *     public Vector3 localPosition;
 *     public Vector3 localEulerAngles;
 *     public Vector3 localScale = Vector3.one;
 * }
 *
 * 런타임 적용 흐름
 *
 * 1. 터렛이 자신의 TurretDefinitionSO를 가진다.
 * 2. 현재 강화 레벨에 맞는 TurretUpgradeLevelSO를 가져온다.
 * 3. statProfile은 공격 로직/타겟팅/데미지 시스템에 적용한다.
 * 4. vfxProfile은 발사체, 머즐, 히트, 사운드 연출에 적용한다.
 * 5. partsProfile은 터렛 모델의 socket에 파츠를 붙이거나 교체한다.
 *
 * 역할 경계
 *
 * 준영
 * - TurretStatProfileSO
 * - 업그레이드 비용/해금 조건
 * - 데미지, 타겟팅, 웨이브, 데이터 로딩 구조
 *
 * 규원
 * - TurretVFXProfileSO
 * - TurretPartsProfileSO
 * - 발사체 VFX, 머즐 VFX, 히트 VFX, 사운드, 강화 외형 파츠
 * - 터렛 연출 테스트 씬
 *
 * 주의사항
 * - 현재 TurretVFXProfile의 projectileSpeed는 테스트 편의용이다.
 * - 최종 구조에서는 projectileSpeed를 TurretStatProfileSO로 옮기는 것이 좋다.
 * - VFX 비교용 테스트 UI는 실제 게임 UI와 분리한다.
 * - Private Assets의 외부 에셋 원본은 직접 수정 범위를 최소화하고, 프로젝트용 복사본/프로필에서 조립한다.
 */
