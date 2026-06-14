/* ==========================================================================================
 * [PROJECT README] 기업협약 프로젝트 - 좀비 타워 디펜스 게임 기획서
 * ==========================================================================================
 * * ■ TITLE             : Z-Defence
 * ■ PLATFORM          : Cross-Platform (Mobile & Windows PC - 세로 화면 조작)
 * ■ GENRE             : Zombie Tower Defense
 * ■ VIEW              : 3인칭 아이소메트릭 뷰 (3D Isometric View)
 * ■ REFERENCE         : Project Zomboid, 위키드 디펜스, 방치형 킹덤 디펜스
 * * ==========================================================================================
 * 1. CORE SYSTEM & GAMEPLAY (핵심 기획 의도 및 차별점)
 * ==========================================================================================
 * - [요새 방어] 요새 내부를 배회하는 아군 NPC를 선택하여 타워에 배치 및 타워 업그레이드 진행.
 * - [웨이브 시스템] 각 Wave마다 몰려오는 좀비를 처치하며, 특정 Wave에는 특수 보스 좀비 출현.
 * * ==========================================================================================
 * 2. TECHNICAL ARCHITECTURE (게임 구조 및 개발 설계)
 * ==========================================================================================
 * ■ Data Management (ScriptableObject)
 * - 좀비 스탯 (Zombie Stats)
 * - 아군 NPC 스탯 (Ally NPC Stats)
 * - 타워 기본 스탯 (Tower Stats)
 * * ■ AI & Pattern (Behavior Tree)
 * - 아군 NPC 및 좀비의 유기적인 행동 패턴 제어 및 판단 로직 구축.
 * * ■ Tower Weapon System (Upgrades & Variations)
 * - 기본 업그레이드 : 내구도 향상, 공격력 향상, 사거리 향상, 연사 속도 향상 등
 * - 탄종 바리에이션 : 관통탄, 집속탄, 소이탄 등
 * * ■ Ultimate Skill (Central Tower)
 * - 중앙 타워 전용 궁극기 : 긴 쿨타임, 초강력 데미지, 광역(AOE) 범위 판정.
 * * ==========================================================================================
 * 3. ENEMY VARIATIONS (보스 좀비 특수 능력)
 * ==========================================================================================
 * - [스크리머 (Screamer)] 어그로 담당. 주변 좀비를 끌어모아 요새의 한 지점을 집중 공격하도록 유도.
 * - [부머 (Boomer)] 디버프 담당. 주기적 폭발 및 산성 토사물 분사로 주변 타워를 부식(지속 피해).
 * - [차저 (Charger)] 탱커/돌진 담당. 높은 내구도를 바탕으로 타워를 향해 돌진하여 강력한 충돌 피해 제공.
 * * ==========================================================================================
 * 4. ECONOMY & REWARD SYSTEM (보상 및 드론 자동화)
 * ==========================================================================================
 * ■ Drop Items
 * - 좀비 처치 시 '코인' 및 '업그레이드 부품' 드랍 (플레이어가 직접 클릭하여 수집 가능).
 * - 코인 : NPC 및 타워의 기초 스탯 영구 업그레이드에 사용.
 * - 부품 : 타워의 무기 바리에이션(탄종 등) 특수 업그레이드에 사용.
 * * ■ Collection Drone (자동 수집 드론 시스템)
 * - 코인을 자동으로 수집하는 편의성 시스템. 아래 스탯 위주의 업그레이드 구조 설계 필요.
 * - [이동속도] 드론의 맵 이동 속도
 * - [수집속도] 코인을 획득하는 물리적 속도
 * - [보관함 용량] 최대 소지 코인량 (용량 초과 시 요새로 복귀하여 비워야 함)
 * - [배터리 용량] 방전 시 일정 쿨타임 동안 충전 상태 돌입 (행동 불능)
 * - [배터리 충전속도] 요새 복귀 시 배터리가 차오르는 속도
 * * ==========================================================================================
 * 5. PENALTY & GAME LOOP (패널티 및 루프 스테이지)
 * ==========================================================================================
 * - [Wave 클리어 실패 시] 소지하고 있는 코인 중 일정 비율(또는 고정 수치) 차감 패널티 부과.
 * - [재도전] 패널티 적용 후, 실패한 현재 Wave의 0초 시점으로 돌아가 다시 시작.
 * * ==========================================================================================
 *
 * ==========================================================================================
 * 6. 팀 역할 및 작업 경계
 * ==========================================================================================
 *
 * [준영] 게임 코어 / 시스템 담당
 * - 웨이브 진행, 적 스폰 규칙, 타겟팅 정책, 데미지 규칙, 체력/사망/보상 흐름을 담당한다.
 * - 터렛 스탯, 업그레이드 규칙, ScriptableObject 데이터 구조, 전체 게임 상태를 담당한다.
 * - 터렛 진화 요구 레벨은 TurretEvolutionProgressionSO.requiredLevel에서 관리하고,
 *   TurretDefinitionSO.maxLevel은 더 이상 진화하지 않는 최종 터렛의 하드 캡에만 사용한다.
 * - 예상 모듈:
 *   WaveManager, EnemySpawner, TargetingSystem, DamageSystem, Health,
 *   TowerStatData, EnemyStatData, UpgradeData, GameStateManager.
 * - 최종 책임:
 *   터렛, 좀비, 맵, 연출 시스템이 함께 사용할 게임 규칙과 공통 API를 정의한다.
 *
 * [규원] 터렛 / 발사체 연출 담당
 * - 터렛 배치 연출, 조준/발사 연출, 발사체 VFX, 머즐 플래시, 히트 이펙트,
 *   발사 사운드, 반동/총열 애니메이션, 터렛 연출 테스트 씬을 담당한다.
 * - 터렛 연출에 필요한 VFX 데이터 구성을 담당한다.
 * - 터렛별 TurretVFXProfileSO, TurretVFXProgressionSO, projectile prefab, muzzle/fire effect 연결을 담당한다.
 * - 예상 모듈:
 *   TurretVFXProfile, TurretFireVFXController, ProjectileVFXAdapter,
 *   MuzzleFlashController, HitEffectController, turret range/debug gizmos,
 *   AAA Projectiles candidate selection and matching.
 * - 최종 책임:
 *   터렛 발사가 잘 보이고, 반응성이 좋으며, 코어 전투 로직에 쉽게 연결되도록 만든다.
 * - 작업 경계:
 *   최종 데미지 공식, 업그레이드 밸런스, 웨이브 밸런스는 담당하지 않는다.
 *   더미 적과 테스트용 훅은 터렛 연출 검증 용도로만 사용한다.
 *
 * [준호] 좀비 / 맵 / 애니메이션 / 행동 담당
 * - 좀비 프리팹 구성, 좀비 이동, 좀비 애니메이션, 행동트리/상태 로직,
 *   맵 경로, 스폰/목표 지점, 장애물 구성, 특수 좀비 행동을 담당한다.
 * - 예상 모듈:
 *   ZombieController, ZombieMovement, ZombieAnimator, ZombieBehaviorTree,
 *   ZombieAttack, MapPath, SpawnPoint, GoalPoint.
 * - 특수 좀비 담당:
 *   Screamer, Boomer, Charger의 행동과 애니메이션 연동.
 * - 최종 책임:
 *   적이 이동, 반응, 공격할 수 있게 만들고, 게임 시스템이 사용할 타겟/데미지 연결점을 제공한다.
 *
 * ------------------------------------------------------------------------------------------
 * 공통 API 계약
 * ------------------------------------------------------------------------------------------
 *
 * 아래 계약은 작업 충돌과 담당 범위 중복을 줄이기 위해 초기에 합의한다.
 *
 * public interface IDamageable
 * {
 *     float TotalHp { get; }
 *     float CurrHp { get; }
 *     bool IsAlive { get; }
 *     void TakeDamage(float damage);
 * }
 *
 * public interface ITargetable
 * {
 *     Transform TargetPoint { get; }
 *     bool IsAlive { get; }
 * }
 *
 * public interface IProjectileHitHandler
 * {
 *     void OnProjectileHit(GameObject target);
 * }
 *
 * 담당 연결:
 * - 준영은 최종 데미지 계산과 IDamageable 구현 정책을 담당한다.
 * - 규원은 발사체 충돌 전후의 연출과 합의된 피격 훅에 VFX를 연결하는 작업을 담당한다.
 * - 준호는 좀비 쪽 IDamageable / ITargetable 연동과 적 반응을 담당한다.
 *
 * 우선순위:
 * 1) 준영: IDamageable, ITargetable, 기본 Health, 테스트 가능한 데미지 흐름.
 * 2) 준호: 더미 좀비 경로 이동, 피격 반응, 사망 반응.
 * 3) 규원: 터렛 VFX 테스트 씬, 발사체 후보 선정, 머즐/히트/사운드 프로필 구성.
 *
 * ------------------------------------------------------------------------------------------
 * 현재 구현된 전투 연결 현황
 * ------------------------------------------------------------------------------------------
 *
 * - 터렛 발사체는 ProjectileDamageDealer를 통해 IDamageable 대상에게 데미지를 전달한다.
 * - ProjectileDamageDealer는 발사 시 전달받은 추적 타겟 IDamageable을 저장하여,
 *   HOVL 피격 이펙트가 데미지 레이어가 아닌 자식 콜라이더에서 먼저 터져도 실제 타겟에게 데미지를 적용한다.
 * - ProjectileHitDetector는 빠르게 이동하는 발사체의 누락을 줄이기 위해 추적 대상 판정,
 *   Trigger/Collision 판정, 이동 구간 Raycast 판정을 함께 사용한다.
 * - 발사체는 이미 맞은 IDamageable을 중복 타격하지 않고, IsAlive == false 대상은 무시한다.
 * - pierceCount는 한 발사체가 추가로 관통 가능한 대상 수로 사용하며, 한계 도달 시 풀로 반환한다.
 * - NormalZombie와 BossZombie는 IDamageable을 구현하고, 사망 시 타겟 후보에서 제외되도록 IsAlive를 갱신한다.
 * - 좀비 사망 후 남아 있던 콜라이더 문제를 줄이기 위해 사망 상태와 충돌 판정을 함께 정리한다.
 * - 일반 좀비 16종은 Weak, Basic, Fast, Tough, Attacker, Elite 6개 NormalZombieSpec 역할군으로 묶어 기본 전투 스탯을 관리한다.
 * - NormalZombieSpec과 BossZombieSpec은 HP, 공격력, 이동/공격 속도, 공격거리, 개체별 랜덤 편차만 소유한다.
 * - ZombieWaveSpawnProfileSO는 일반/보스 공통으로 웨이브 구간, 스폰 수, 스폰 간격, 등장 프리팹 가중치, 보스 마지막 스폰, HP/공격력/속도/보상 배율을 소유한다.
 * - 기존 ZombieSpawnData 기반 스폰 간격/스폰 수 성장 데이터는 제거되었고, Main 씬의 ZombieSpawner는 ZombieWaveSpawnProfileSO를 사용한다.
 * - 현재 1차 캠페인 밸런스는 1~500웨이브 기준이며, 451~500 구간 hpMultiplier 280으로 후반 Elite 일반 좀비가 약 79,800~100,800 HP 범위에 들어오도록 잡는다.
 * - 데미지 발생 시 DamagePopupSpawner.SpawnDamage를 통해 월드 공간 데미지 숫자를 표시한다.
 * - TargetFinder는 콜라이더 자식이 아니라 태그 또는 IDamageable 기준의 안정적인 타겟 루트를 반환한다.
 * - TargetFinder의 시야 판정은 인스펙터 설정에 따라 ObstacleBuildSlot 보조 콜라이더,
 *   설치된 Obstacle 콜라이더, 추가 무시 레이어를 통과시킬 수 있다.
 * - 터렛 조준은 TurretAimPointUtility를 통해 콜라이더 중앙보다 낮은 몸통 기준점을 사용한다.
 * - 터렛 회전은 조준점/타겟 속도 보간, 예측 리드 시간 제한, 수직 예측 무시 옵션으로 공통 스무딩을 적용한다.
 *
 * ------------------------------------------------------------------------------------------
 * 현재 구현된 터렛 / UI / 연출 현황
 * ------------------------------------------------------------------------------------------
 *
 * - Sentinel-01에서 Sentry Pulse 또는 Vector MG로 분기하는 진화 테스트 흐름이 구현되어 있다.
 * - Sentry Pulse는 Pulse Repeater, Vector MG는 Vulcan Node로 이어지는 1세대 진화 분기를 가진다.
 * - Pulse Repeater와 Vulcan Node는 2세대 진입 터렛이며, 현재 maxLevel은 0으로 유지한다.
 * - Pulse Repeater와 Vulcan Node의 2세대 진입 EvolutionProgressionSO는 Definition에 연결되어 있다.
 * - 2세대 터렛 Definition 24개가 생성되어 있고 base prefab, stat, stat growth, VFX progression, projectile scale progression 참조가 연결되어 있다.
 * - 2세대 터렛 라인업:
 *   Machinegun_Blue_1~3, Machinegun_Red_1~3,
 *   Laser_Blue_1~3, Laser_Red_1~3,
 *   Lethal_Green_1~3, Lethal_Red_1~3,
 *   Plasma_Blue_1~3, Plasma_Yellow_1~3.
 * - 2세대 VFX 매핑:
 *   Laser_Blue -> Blue Laser, Laser_Red -> Red Laser,
 *   Machinegun_Blue -> Blue Fire, Machinegun_Red -> Black Fire,
 *   Lethal_Red -> Orange Explosion, Lethal_Green -> Green Explosion,
 *   Plasma_Blue -> Nova Violet, Plasma_Yellow -> Nova Orange.
 * - 2세대 내부 진화는 각 계열의 _1 -> _2 -> _3 구조로 연결되어 있다.
 * - 2세대 _3 터렛들은 아직 3세대 계획이 없으므로 evolutionProgressionProfile을 null로 둔다.
 * - Evolution Entry displayName은 target Definition displayName과 같은 언더스코어 포함 형식으로 통일한다.
 * - 2세대 Definition의 basePrefab 참조는 child model이 아니라 prefab root를 가리키도록 검증 및 수정했다.
 * - TurretEvolutionRuntimeUI는 런타임에서 레벨업, 진화 선택, Max Level 표시를 담당한다.
 * - 임시 런타임 업그레이드 팝업은 가능한 진화 엔트리 수만큼 버튼을 동적으로 생성하므로 4분기 진입 UI를 표시할 수 있다.
 * - 터렛 스탯, VFX 프로필, 발사체 크기, 진화 규칙은 ScriptableObject 기반 데이터로 분리되어 있다.
 * - 메인 씬에는 터렛 진화 UI와 좀비 타겟팅/피격 흐름이 연결되어 있다.
 * - DamagePopupSettings는 데미지 팝업 프리팹, 풀 초기 크기, 폰트, 색상, 위치, 이동, 스케일, 유지 시간을 관리한다.
 * - DamagePopup.prefab과 DamagePopupSettings.asset은 Assets/__PROJECT__/Resources/UI 경로에서 Resources.Load로 사용한다.
 * - TurretPlacementUI는 하단 터렛 슬롯 목록을 표시하고, 드래그/클릭으로 터렛 배치 흐름을 시작한다.
 * - TurretPlacementController는 터렛 아이콘 드래그 중 TurretBase 레이어의 PlacementHitArea를 Raycast하여 설치 가능 여부를 판단한다.
 * - TurretBaseSlot은 각 Turret Base의 BuildPoint, PlacementHitArea, 현재 설치된 터렛 점유 상태를 관리한다.
 * - 터렛 설치는 항상 BuildPoint의 자식으로 생성하고, 설치 터렛의 localPosition/localRotation을 0으로 맞춘다.
 * - 설치 가능 위치는 초록 프리뷰, 점유된 베이스 또는 일반 맵 위치는 빨간 프리뷰로 표시한다.
 * - 일반 맵 위 배치 불가 프리뷰는 건물/벽 콜라이더 hit가 아니라 고정 바닥 평면 투영을 기본으로 사용한다.
 * - TurretShopEntrySO는 배치 UI에 표시할 터렛 정의, 아이콘, 비용, 프리뷰 프리팹을 관리한다.
 *
 * ------------------------------------------------------------------------------------------
 * 레포지토리 / 에셋 관리 메모
 * ------------------------------------------------------------------------------------------
 *
 * - 루트 프로젝트 레포는 코드, 씬, 프로젝트 레벨 래퍼/설정 에셋을 관리한다.
 * - Private Assets 레포는 외부/구매/공용 원본 에셋을 따로 관리한다.
 * - 현재 프로젝트는 Git 레포지토리 2개를 함께 사용한다.
 *   1) 루트 레포: D:/Git/CT02-Project_Z_Defense/CT02-Project_Z_Defense
 *   2) Private Assets 레포: Assets/__PROJECT__/Private Assets
 * - 루트 레포의 git status는 Private Assets 내부 수정 파일을 직접 보여주지 않을 수 있다.
 * - Private Assets 내부 파일을 수정했을 때는 해당 폴더에서 별도로 git status를 확인해야 한다.
 * - 커밋도 두 레포에서 각각 필요할 수 있으므로, 작업 완료 전 루트 레포와 Private Assets 레포 상태를 모두 확인한다.
 * - 최근 터렛/좀비 전투 연결 작업은 루트 프로젝트 레포 중심으로 진행하되,
 *   Turret.cs, TargetFinder.cs, Gun.cs, HS_ProjectileMover.cs처럼 외부 에셋 런타임 연결점은 최소 범위로 직접 수정했다.
 * - 외부 에셋 스크립트를 직접 수정해야 하는 경우에는 변경 범위를 작게 유지하고,
 *   프로젝트 레벨 어댑터/래퍼로 대체 가능한지 먼저 검토한다.
 * - Resources 경로 이동 시 Unity .meta GUID를 유지해야 기존 참조가 깨지지 않는다.
 *
 * 한 줄 요약:
 * - 준영: 규칙과 데이터.
 * - 규원: 터렛 발사 화면과 소리.
 * - 준호: 적 이동과 반응.
 *
 * ==========================================================================================
 */

using UnityEngine;

namespace ProjectZDefense
{
    /// <summary>
    /// This is a conceptual configuration file for the AI assistant and developers.
    /// Do not delete or modify unless the core game concept changes.
    /// </summary>
    public class PROJECT_README : MonoBehaviour
    {
        [Header("Project Information")]
        public string ProjectName = "Project_Z_Defense";
        public string Platform = "Cross-Platform (Mobile / PC)";
        public string CurrentStatus = "Concept & Prototype Stage";

        private void Awake()
        {
            // 이 컴포넌트는 기획서 열람용이므로 빌드 및 실행 시 자동 파괴 처리합니다.
            Destroy(this);
        }
    }
}
