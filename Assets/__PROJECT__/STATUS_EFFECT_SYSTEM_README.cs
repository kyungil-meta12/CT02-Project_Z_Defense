/* ==========================================================================================
 * [STATUS EFFECT SYSTEM README] 상태이상 시스템 구조 문서
 * ==========================================================================================
 *
 * 이 문서는 Frost, Poison 및 이후 Burn, Stun 같은 상태이상을 추가할 때 다시 읽기 위한
 * 프로젝트 루트 레벨 구조 기록이다.
 *
 * 핵심 목적:
 * - 상태이상 데이터, 전달, 런타임, 비주얼, 타겟 필터의 책임을 분리한다.
 * - NormalZombie / BossZombie가 상태이상별 타이머와 스택 로직을 직접 소유하지 않게 한다.
 * - 터렛별 특수 정책은 ScriptableObject, Runtime, CandidateFilter로 분산해 유지보수한다.
 * - 앞으로 추가될 Burn, Stun, Bleed, Shock 계열도 같은 패턴으로 확장한다.
 *
 * ==========================================================================================
 * 1. DIRECTORY / FILE MAP
 * ==========================================================================================
 *
 * ■ 프로젝트 루트 문서
 * - Assets/__PROJECT__/STATUS_EFFECT_SYSTEM_README.cs
 *   현재 문서. 상태이상 구조와 책임 경계를 큰 흐름으로 설명한다.
 *
 * ■ 상태이상 공통 런타임
 * - Assets/__PROJECT__/Scripts/StatusEffects/FrostStatusRuntime.cs
 *   대상 단위 Frost 런타임. 슬로우 누적, 빙결 타이머, 빙결 쿨타임,
 *   Frost 비주얼 토글, Ice_Cubes_Explosion 취소, 빙결 사망 VFX 실행을 담당한다.
 *
 * - Assets/__PROJECT__/Scripts/StatusEffects/PoisonStatusRuntime.cs
 *   대상 단위 Poison 런타임. 지속시간, 틱 타이머, 스택, 틱데미지,
 *   처형 예측, Poison 비주얼, 처형 사망 폭발 트리거를 담당한다.
 *
 * - Assets/__PROJECT__/Scripts/StatusEffects/ElectroStatusRuntime.cs
 *   대상 단위 Electro 런타임. Shock 스택 수, 스택 유지시간,
 *   Volt Sphere 1 기반 스택 비주얼 생성/회전/해제를 담당한다.
 *
 * - Assets/__PROJECT__/Scripts/StatusEffects/StatusEffectVisualController.cs
 *   좀비 프리팹 측 상태이상 비주얼 슬롯 관리자.
 *   Frost overlay, Poison body/foot aura, Poison lethal indicator 같은 표시를
 *   상태이상 런타임에서 직접 프리팹 계층을 뒤지지 않고 켜고 끄게 해준다.
 *
 * ■ 상태이상 Payload / Receiver / Profile
 * - Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Scripts/IFrostStatusEffectReceiver.cs
 *   ProjectZDefense.StatusEffects.FrostStatusPayload와 IFrostStatusEffectReceiver를 정의한다.
 *
 * - Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Scripts/IPoisonStatusEffectReceiver.cs
 *   ProjectZDefense.StatusEffects.PoisonStatusPayload와 IPoisonStatusEffectReceiver를 정의한다.
 *
 * - Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Scripts/IElectroStatusEffectReceiver.cs
 *   ProjectZDefense.StatusEffects.ElectroStatusPayload와 IElectroStatusEffectReceiver를 정의한다.
 *
 * - Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Scripts/FrostStatusProfileSO.cs
 *   Frost 기본 밸런스와 VFX 참조를 관리한다.
 *
 * - Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Scripts/PoisonStatusProfileSO.cs
 *   Poison 기본 틱 데미지, 지속시간, 스택, 보스 배율, 사망 폭발 프로필 참조를 관리한다.
 *
 * - Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Scripts/ElectroStatusProfileSO.cs
 *   Electro 체인 라이트닝, Shock 스택 유지시간, Shock 스택 VFX,
 *   향후 Overload/경직 값, 체인 링크 VFX 값을 관리한다.
 *
 * - Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Scripts/PoisonDeathBurstProfileSO.cs
 *   Poison 처형 사망 폭발 VFX, 범위, 약한 Poison, 연쇄 허용 여부를 관리한다.
 *
 * - Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Scripts/PoisonTurretStatGrowthProfileSO.cs
 *   Poison_Turret 전용 성장값을 관리한다.
 *   공유 TurretStatGrowthProfileSO에 Poison 전용 필드를 직접 늘리지 않기 위한 분리 지점이다.
 *
 * ■ 상태이상 Utility
 * - Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Scripts/FrostStatusEffectUtility.cs
 *   Frost 빙결 이펙트 생성, 지연 폭발 데미지 예약, 범위 데미지,
 *   짧은 보조 슬로우, 빙결 사망 VFX 생성을 담당한다.
 *
 * - Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Scripts/FrostFreezeExplosionDamageTimer.cs
 *   Ice_Cubes_Explosion을 원 대상에 따라가게 하고, 설정된 지연 시간 뒤 폭발 데미지를 적용한다.
 *
 * - Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Scripts/PoisonStatusRuntimeUtility.cs
 *   Poison 틱 수, 남은 틱 수, 체력비례 틱 데미지 같은 순수 계산을 담당한다.
 *
 * - Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Scripts/PoisonDeathBurstEffectUtility.cs
 *   Poison 처형 사망 폭발 VFX와 약한 범위 Poison 적용을 담당한다.
 *
 * ■ 타겟 후보 필터
 * - Assets/__PROJECT__/Scripts/Targeting/ITargetCandidateFilter.cs
 *   TargetFinder가 상태이상 특수 정책을 직접 알지 않게 하는 후보 제외 계약이다.
 *
 * - Assets/__PROJECT__/Scripts/Targeting/PoisonLethalTargetCandidateFilter.cs
 *   Poison_Turret 전용 필터. Poison 틱으로 이미 사망이 확정된 대상을 다시 쏘지 않게 한다.
 *
 * - Assets/__PROJECT__/Scripts/Targeting/FrostFreezeSuppressedTargetCandidateFilter.cs
 *   Frost_Turret 전용 필터. 빙결 중이거나 재빙결 쿨타임 중인 일반 좀비를 후보에서 제외한다.
 *
 * ■ 수신 대상
 * - Assets/__PROJECT__/Prefabs/Damageable/NormalZombie/NormalZombie.cs
 *   IFrostStatusEffectReceiver, IPoisonStatusEffectReceiver, IFrostStatusRuntimeOwner를 구현한다.
 *   상태이상 계산은 Runtime으로 위임하고, 자신은 HP, 사망, 보상, 이동/공격 속도 반영만 담당한다.
 *
 * - Assets/__PROJECT__/Prefabs/Damageable/BossZombie/BossZombie.cs
 *   NormalZombie와 같은 계약을 구현하지만, Frost는 현재 slow-only 정책이며 빙결 폭발은 비활성이다.
 *   Poison은 bossDamageMultiplier를 적용하고, 처형 사망 폭발은 비활성이다.
 *
 * ==========================================================================================
 * 2. CORE RESPONSIBILITY RULE
 * ==========================================================================================
 *
 * 상태이상은 아래 원칙으로 나눈다.
 *
 * ■ ProfileSO
 * - 에디터에서 조정할 밸런스 데이터와 VFX 참조를 소유한다.
 * - 런타임 중 변하는 타이머, 스택, 현재 대상 상태를 소유하지 않는다.
 * - 예: FrostStatusProfileSO, PoisonStatusProfileSO, PoisonDeathBurstProfileSO.
 *
 * ■ Payload
 * - 공격자가 수신자에게 전달하는 변경 불가능한 값 묶음이다.
 * - ProfileSO와 현재 터렛 레벨/성장 데이터를 조합한 결과를 담는다.
 * - 런타임 대상은 Payload를 저장하거나 복사해도 되지만, 원본 SO를 직접 수정하지 않는다.
 * - 예: FrostStatusPayload, PoisonStatusPayload.
 *
 * ■ Receiver
 * - "이 대상이 이 상태이상을 받을 수 있다"는 최소 계약이다.
 * - 상태이상별 실제 타이머나 스택 계산을 직접 구현하지 않는다.
 * - 예: IFrostStatusEffectReceiver.ApplyFrostStatus, IPoisonStatusEffectReceiver.ApplyPoisonStatus.
 *
 * ■ Runtime
 * - 대상별 mutable state를 소유한다.
 * - 지속시간, 틱 타이머, 스택, 빙결 타이머, 처형 예측, 비주얼 토글, 사망 특수 트리거를 관리한다.
 * - NormalZombie / BossZombie는 Runtime을 캐시하고 Tick, Apply, Reset, Death hook만 호출한다.
 * - 예: FrostStatusRuntime, PoisonStatusRuntime.
 *
 * ■ Utility
 * - 순수 계산, NonAlloc 범위 판정, VFX 스폰, 풀 반환 같은 공통 동작을 담당한다.
 * - 대상별 상태 필드를 직접 소유하지 않는다.
 * - 예: FrostStatusEffectUtility, PoisonStatusRuntimeUtility, PoisonDeathBurstEffectUtility.
 *
 * ■ Visual Controller
 * - 좀비 프리팹 안의 상태이상 표시 슬롯과 앵커를 소유한다.
 * - 상태이상 런타임은 "Poison 켜기", "Frost 끄기", "처형 표시 켜기" 같은 명령만 보낸다.
 * - 새 상태이상은 가능한 한 StatusEffectVisualSlot으로 추가한다.
 *
 * ■ Target Candidate Filter
 * - TargetFinder가 특정 상태이상 세부 지식을 직접 갖지 않도록 분리한 정책 컴포넌트다.
 * - 터렛 프리팹 루트에 붙이고 TargetFinder.targetCandidateFilterBehaviours에 연결한다.
 * - 상태이상 타겟 제외 조건이 늘어나면 이 패턴을 우선 사용한다.
 *
 * ==========================================================================================
 * 3. FROST CURRENT FLOW
 * ==========================================================================================
 *
 * Frost_Turret은 BeamFiringEvent 기반의 연속 빔 터렛이다.
 *
 * 1. TurretDefinitionSO.frostStatusProfile에 FrostStatusProfileSO를 연결한다.
 * 2. TurretDefinitionRuntimeController가 BeamFiringEvent.SetFrostStatusProfile을 호출한다.
 * 3. BeamFiringEvent는 빔 데미지 틱마다 FrostStatusProfileSO.CreatePayload를 호출한다.
 * 4. 피격 대상이 IFrostStatusEffectReceiver이면 ApplyFrostStatus(payload)를 호출한다.
 * 5. NormalZombie / BossZombie는 FrostStatusRuntime.ApplyFrostStatus로 위임한다.
 * 6. FrostStatusRuntime은 slowBuildUpDuration, maxSlowRatio, slowHoldDuration을 기준으로 슬로우를 누적한다.
 * 7. 일반 좀비는 canTriggerFreeze = true이므로 frostSlowRatio가 freezeTriggerRatio에 도달하면 빙결이 발동된다.
 * 8. 보스는 canTriggerFreeze = false이므로 슬로우만 받고 빙결 폭발은 발생하지 않는다.
 *    BossZombie는 적용 배율을 최소 0.5로 보정해 Frost로 이동/공격 애니메이션이 완전히 멈추지 않게 한다.
 * 9. 빙결 발동 시 FrostStatusRuntime은 activeFreezePayload를 저장하고 Ice_Cubes_Explosion을 생성한다.
 * 10. FrostFreezeExplosionDamageTimer가 Ice_Cubes_Explosion을 대상에 따라가게 하며 지연 폭발 데미지를 예약한다.
 * 11. 대상이 죽거나 풀로 리셋되면 FrostStatusRuntime.ResetStatus가 Ice_Cubes_Explosion과 예약 데미지를 취소한다.
 * 12. 대상이 freezeTimer가 살아있는 상태에서 죽으면 TriggerFreezeDeathEffectIfNeeded가 freezeDeathEffectPrefab을 별도로 재생한다.
 *
 * Frost_Turret 타겟 필터:
 * - FrostFreezeSuppressedTargetCandidateFilter는 빙결 중이거나 재빙결 쿨타임 중인 대상을 제외한다.
 * - 단순 슬로우 누적 상태는 제외하지 않는다.
 * - 보스는 canTriggerFreeze = false라서 필터 조건에 걸리지 않는다.
 * - 필터의 excludeFrozenTargets, excludeFreezeCooldownTargets는 프리팹 인스펙터에서 확인할 수 있다.
 *
 * Frost 주요 에디터 필드:
 * - FrostStatusProfileSO.freezeEffectPrefab
 *   빙결 발동 시 생성되는 Ice_Cubes_Explosion 계열 이펙트.
 *
 * - FrostStatusProfileSO.freezeDeathEffectPrefab
 *   빙결 상태인 대상이 죽을 때 별도로 재생되는 사망 보강 VFX.
 *
 * - FrostStatusProfileSO.freezeCooldownPerTarget
 *   같은 대상이 다시 빙결 폭발을 일으키기까지의 개별 쿨타임.
 *
 * - FrostStatusProfileSO.freezePrimaryTargetMaxHpDamageRatio
 *   Ice_Cubes_Explosion 원 대상에게 적용할 최대체력 비례 데미지.
 *
 * - FrostStatusProfileSO.freezeExplosionRadius / freezeExplosionDamage
 *   원 대상을 제외한 주변 범위 데미지.
 *
 * - FrostStatusProfileSO.freezeExplosionSlowRatio / freezeExplosionSlowDuration
 *   폭발에 맞은 주변 대상에게 적용하는 짧은 보조 슬로우.
 *   이 보조 슬로우는 canTriggerFreeze = false로 전달되어 연쇄 빙결 폭발을 만들지 않는다.
 *
 * ==========================================================================================
 * 4. POISON CURRENT FLOW
 * ==========================================================================================
 *
 * Poison_Turret은 기존 투사체 터렛 구조를 재사용한다.
 * Gun, FiringEvent, ProjectileDamageDealer, TargetFinder 경로는 기존 1~2세대 투사체 터렛과 유사하다.
 *
 * 1. TurretDefinitionSO.poisonStatusProfile에 PoisonStatusProfileSO를 연결한다.
 * 2. Poison_Turret 전용 성장값은 PoisonTurretStatGrowthProfileSO에서 관리한다.
 * 3. TurretDefinitionRuntimeController.CreatePoisonStatusPayload가 프로필과 성장값을 조합한다.
 * 4. Turret.SetPoisonStatusPayload로 현재 투사체에 전달할 Payload를 저장한다.
 * 5. Gun / ProjectileDamageDealer가 발사체 생성 시 PoisonStatusPayload를 전달한다.
 * 6. ProjectileDamageDealer는 직접 데미지를 먼저 적용한다.
 * 7. 대상이 살아 있고 IPoisonStatusEffectReceiver이면 ApplyPoisonStatus(payload)를 호출한다.
 * 8. NormalZombie / BossZombie는 PoisonStatusRuntime.ApplyPoisonStatus로 위임한다.
 * 9. PoisonStatusRuntime은 지속시간, tickInterval, maxStackCount, stackRefreshMode를 기준으로 상태를 관리한다.
 * 10. PoisonStatusRuntimeUtility는 남은 틱 수와 체력비례 틱 데미지를 계산한다.
 * 11. 남은 확정 틱 데미지로 현재 HP가 죽을 수 있으면 IsPoisonLethalPending = true가 된다.
 * 12. StatusEffectVisualController가 PoisonIcon 같은 처형 표시 자식을 켠다.
 * 13. PoisonLethalTargetCandidateFilter가 IsPoisonLethalPending 대상 재타겟을 막는다.
 * 14. 일반 좀비가 IsPoisonLethalPending 상태로 죽으면 PoisonDeathBurstEffectUtility가 사망 폭발을 실행한다.
 * 15. PoisonDeathBurstProfileSO.allowChainDeathBurst가 true이면 약한 Poison도 처형 표시와 연쇄 폭발을 만들 수 있다.
 *
 * ==========================================================================================
 * 4-1. ELECTRO CURRENT FLOW
 * ==========================================================================================
 *
 * Electro_Turret은 기존 투사체 터렛 구조를 재사용하고, 직접 피격 후 체인 라이트닝을 전파한다.
 *
 * 1. TurretDefinitionSO.electroStatusProfile에 ElectroStatusProfileSO를 연결한다.
 * 2. TurretDefinitionRuntimeController가 ElectroStatusProfileSO.CreatePayload로 payload를 만든다.
 * 3. Turret, FiringEvent, Gun, ProjectileDamageDealer가 payload를 투사체에 전달한다.
 * 4. ProjectileDamageDealer는 직접 데미지를 먼저 적용한 뒤 IElectroStatusEffectReceiver에 ApplyElectroStatus를 호출한다.
 * 5. ElectroChainLightningUtility는 주변 대상에게 체인 데미지를 주고, 체인 대상에게도 ApplyElectroStatus를 호출한다.
 * 6. NormalZombie / BossZombie는 ElectroStatusRuntime.ApplyElectroStatus로 위임한다.
 * 7. ElectroStatusRuntime은 Electro 피격마다 Shock 스택을 1개 추가하고 shockStackDuration으로 유지시간을 갱신한다.
 * 8. Shock 스택은 현재 최대 3개까지 시각화되며, Volt Sphere 1 인스턴스가 대상 몸 중앙 주변을 회전한다.
 * 8-1. ElectroStatusProfileSO에서 일반/보스 회전 반지름, 높이, 회전 속도, 스케일, 카메라 기준 뒤쪽 알파 페이드를 조정한다.
 * 8-2. 기본 Electro 프로필은 1~2스택에서 일부 반짝임 자식을 끄고, 3스택에서 전체 Volt Sphere 1 파츠를 켜 완전 충전 상태를 강조한다.
 * 9. Electro 적중은 IElectroStunRuntimeOwner를 통해 일반 좀비에게 짧은 경직을 적용하고, StatusEffectVisualController.ElectroStun 슬롯으로 경직 VFX를 켤 수 있다. 보스는 bossHitStunDurationMultiplier = 0으로 짧은 경직에서 제외하며, 향후 Overload 긴 스턴은 bossStunDurationMultiplier로 별도 조정한다.
 * 10. 현재 Electro 자체 공격은 3스택을 소모하거나 Overload를 발동하지 않는다.
 * 11. 다음 단계는 3스택 대상이 비-Electro 피해를 받을 때 Overload VFX, 큰 데미지, 긴 기절을 적용하는 것이다.
 *
 * Poison 주요 에디터 필드:
 * - PoisonStatusProfileSO.maxHpDamageRatioPerTick
 *   각 Poison 틱의 최대체력 비례 데미지.
 *
 * - PoisonStatusProfileSO.tickInterval
 *   Poison 틱 데미지 간격.
 *
 * - PoisonStatusProfileSO.duration
 *   Poison 지속시간.
 *
 * - PoisonStatusProfileSO.maxStackCount
 *   Poison 중첩 상한.
 *
 * - PoisonStatusProfileSO.stackRefreshMode
 *   반복 피격 시 지속시간/스택 갱신 정책.
 *
 * - PoisonStatusProfileSO.bossDamageMultiplier
 *   보스 대상 Poison 데미지 배율.
 *
 * - PoisonStatusProfileSO.deathBurstProfile
 *   처형 사망 폭발이 필요한 경우 연결하는 PoisonDeathBurstProfileSO.
 *
 * - PoisonDeathBurstProfileSO.allowChainDeathBurst
 *   약한 범위 Poison이 다시 처형 표시와 사망 폭발을 만들 수 있는지 결정한다.
 *
 * - PoisonTurretStatGrowthProfileSO
 *   Poison_Turret 전용 레벨 성장값.
 *   Poison 틱 데미지, 지속시간, 사망 폭발 범위, 약한 Poison 데미지, 약한 Poison 지속시간을 관리한다.
 *
 * ==========================================================================================
 * 5. VISUAL SLOT POLICY
 * ==========================================================================================
 *
 * StatusEffectVisualController는 새 상태이상 비주얼의 기본 확장 지점이다.
 *
 * 현재 정책:
 * - FrostSlow는 RendererOverlay 슬롯을 사용한다.
 * - Poison은 Anchor 슬롯을 사용해 발 밑/몸 중앙 VFX를 붙인다.
 * - Electro Shock 스택은 ElectroStatusProfileSO.shockStackVisualPrefab을 사용해
 *   ElectroStatusRuntime이 대상별로 최대 3개를 지연 생성하고 재사용한다.
 * - 보스 Shock 스택은 useBossShockStackOrbitRadius와 bossShockStackOrbitRadius로 일반 좀비보다 큰 궤도를 사용할 수 있다.
 * - 뒤쪽 Shock 스택은 ElectroShockStackVisualFader가 파티클 렌더러를 캐시한 뒤 MaterialPropertyBlock으로 인스턴스 알파만 부드럽게 조절한다.
 * - 1~2스택 약한 전하 모드는 ElectroShockStackVisualModeController가 지정된 자식 오브젝트만 인스턴스 단위로 꺼서 구현한다.
 * - ElectroStun 슬롯에는 FX_Electricity_02 1 같은 Anchor 방식 짧은 파티클을 연결한다.
 * - Poison 처형 표시는 Lethal Indicator Child Name으로 캐시한 자식 오브젝트를 토글한다.
 * - PoisonIcon이 몸에 묻히면 Lethal Indicator Local Position Offset으로 앞으로 빼거나 위로 올린다.
 *
 * 새 상태이상 추가 시:
 * 1. StatusEffectVisualType에 새 타입을 추가한다.
 * 2. StatusEffectVisualController에 SetNewStatusActive 같은 명확한 public 메서드를 추가한다.
 * 3. 좀비 프리팹에 StatusEffectVisualSlot을 추가한다.
 * 4. Runtime은 VisualController 메서드만 호출하고, 매번 Transform.Find를 하지 않는다.
 *
 * 피해야 할 방식:
 * - 상태이상 Runtime이 매 틱마다 자식 이름을 검색하는 구조.
 * - NormalZombie / BossZombie가 VFX 프리팹을 직접 Instantiate하는 구조.
 * - 상태이상별 전용 VFX 필드가 좀비 클래스에 계속 늘어나는 구조.
 *
 * ==========================================================================================
 * 6. TARGET FILTER POLICY
 * ==========================================================================================
 *
 * TargetFinder는 "가장 가까운 유효 타겟 탐색"만 담당해야 한다.
 * Poison, Frost, Burn, Stun 같은 상태이상별 제외 정책은 ITargetCandidateFilter로 분리한다.
 *
 * 현재 필터:
 * - PoisonLethalTargetCandidateFilter
 *   Poison 처형 확정 대상 제외.
 *
 * - FrostFreezeSuppressedTargetCandidateFilter
 *   빙결 중 또는 재빙결 쿨타임 중인 대상 제외.
 *
 * 프리팹 연결 위치:
 * - 터렛 프리팹 루트에 필터 컴포넌트를 붙인다.
 * - TargetFinder.targetCandidateFilterBehaviours 배열에 해당 컴포넌트를 연결한다.
 *
 * 새 필터 추가 기준:
 * - 특정 터렛만 아는 상태이상/대상 제외 정책이면 필터로 만든다.
 * - TargetFinder에 상태이상 이름이 들어가는 if 문을 추가하지 않는다.
 * - 후보 루프에서 GC 할당이 발생하지 않도록 필터 내부에서 LINQ, 새 리스트 생성, 문자열 포맷을 피한다.
 *
 * ==========================================================================================
 * 7. NORMAL / BOSS ZOMBIE RESPONSIBILITY
 * ==========================================================================================
 *
 * NormalZombie와 BossZombie는 상태이상의 실제 계산 주체가 아니다.
 *
 * 담당해야 하는 것:
 * - IDamageable.TotalHp / CurrHp / IsAlive / TakeDamage 구현.
 * - IFrostStatusEffectReceiver / IPoisonStatusEffectReceiver 수신 후 Runtime으로 위임.
 * - IFrostStatusRuntimeOwner.ApplyFrostSpeedMultiplier로 이동/공격 속도에 배율 반영.
 * - Update에서 Runtime.Tick 호출.
 * - Die / Reset / Pool 재사용 시 Runtime reset 또는 death hook 호출.
 *
 * 담당하지 말아야 하는 것:
 * - Poison 틱 타이머, 스택, 처형 예측 직접 보유.
 * - Frost 빙결 타이머, 빙결 쿨타임 직접 보유.
 * - 상태이상 VFX 프리팹 직접 생성.
 * - 상태이상별 타겟 제외 조건 직접 판단.
 *
 * Normal / Boss 정책 차이:
 * - NormalZombie Frost: canTriggerFreeze = true.
 * - BossZombie Frost: canTriggerFreeze = false. 현재는 최대 50% 감속만 허용하는 slow-only.
 * - NormalZombie Poison: bossDamageMultiplier 미사용, death burst 허용.
 * - BossZombie Poison: bossDamageMultiplier 사용, death burst 비허용.
 *
 * ==========================================================================================
 * 8. ADDING A NEW STATUS EFFECT
 * ==========================================================================================
 *
 * Burn, Stun, Bleed, Shock 같은 새 상태이상을 추가할 때 기본 순서:
 *
 * 1. Payload / Receiver 정의
 * - ProjectZDefense.StatusEffects 네임스페이스에 IBurnStatusEffectReceiver와 BurnStatusPayload를 만든다.
 *
 * 2. ProfileSO 정의
 * - BurnStatusProfileSO처럼 기본 밸런스와 VFX 참조를 둔다.
 * - 레벨 성장값이 특정 터렛 전용이면 전용 GrowthProfileSO를 만든다.
 *
 * 3. Runtime 정의
 * - BurnStatusRuntime처럼 대상별 mutable state를 둔다.
 * - Zombie 클래스에 타이머/스택 필드를 직접 추가하지 않는다.
 *
 * 4. Utility 정의
 * - 순수 계산, 범위 적용, VFX 스폰이 필요하면 Utility로 분리한다.
 *
 * 5. Visual Slot 연결
 * - StatusEffectVisualType과 StatusEffectVisualController에 표시 API를 추가한다.
 * - 좀비 프리팹에는 슬롯/앵커만 세팅한다.
 *
 * 6. TurretDefinition / RuntimeController 연결
 * - TurretDefinitionSO에 새 ProfileSO 참조를 추가할지 검토한다.
 * - TurretDefinitionRuntimeController가 현재 터렛 레벨 기준 Payload를 생성하게 한다.
 *
 * 7. 발사체/빔 전달 경로 연결
 * - 투사체 상태이상은 ProjectileDamageDealer 계열에서 직접 데미지 후 전달한다.
 * - 빔 상태이상은 BeamFiringEvent 계열에서 데미지 틱과 함께 전달한다.
 *
 * 8. Target Filter 필요 여부 결정
 * - 이미 죽음 확정, 이미 기절 중, 이미 화상 중첩 최대치 같은 타겟 제외 정책이 있으면
 *   ITargetCandidateFilter로 분리한다.
 *
 * 9. 문서와 검증 추가
 * - Assets/__PROJECT__/Docs/TURRET_SYSTEM.md 또는 COMMON_SYSTEMS.md에 흐름을 기록한다.
 * - 가능하면 Validation 메뉴에서 누락 연결을 잡도록 확장한다.
 *
 * ==========================================================================================
 * 9. EDGE CASE / OPTIMIZATION CHECKLIST
 * ==========================================================================================
 *
 * 상태이상 작업 후 반드시 확인할 것:
 *
 * - 죽은 대상에게 상태이상이 새로 적용되지 않는가.
 * - 죽는 순간 Runtime death hook이 Reset보다 먼저 호출되는가.
 * - 풀 재사용 시 타이머, 스택, 비주얼, 예약 데미지, VFX 핸들이 모두 초기화되는가.
 * - Poison 처형 표시는 실제 남은 틱으로 죽을 수 있을 때만 켜지는가.
 * - Frost 빙결 폭발 예약 데미지는 원 대상이 죽으면 취소되는가.
 * - 빙결 사망 VFX는 Ice_Cubes_Explosion 취소와 별개로 1회만 재생되는가.
 * - 보스 정책이 일반 좀비 정책과 섞이지 않는가.
 * - TargetFinder 후보 필터에서 LINQ, 새 컬렉션, 문자열 포맷 등 핫패스 할당이 없는가.
 * - 범위 적용은 Physics.OverlapSphereNonAlloc을 사용하는가.
 * - 중복 대상 데미지를 막기 위한 고정 버퍼가 정리되는가.
 * - StatusEffectVisualController가 매 프레임 자식 검색을 하지 않는가.
 * - VFX 스폰은 PooledObjectUtility 또는 MemoryPool 기반으로 처리되는가.
 *
 * ==========================================================================================
 * 10. CURRENT EDITOR CHECKPOINTS
 * ==========================================================================================
 *
 * Frost_Turret:
 * - Frost_Turret_Definition.frostStatusProfile 연결.
 * - FrostStatusProfileSO.freezeEffectPrefab 연결.
 * - FrostStatusProfileSO.freezeDeathEffectPrefab 연결.
 * - FrostStatusProfileSO.freezeCooldownPerTarget 설정.
 * - Frost_Turret.prefab TargetFinder.targetCandidateFilterBehaviours에
 *   FrostFreezeSuppressedTargetCandidateFilter 연결.
 * - FrostFreezeSuppressedTargetCandidateFilter.excludeFrozenTargets = true.
 * - FrostFreezeSuppressedTargetCandidateFilter.excludeFreezeCooldownTargets = true.
 *
 * Poison_Turret:
 * - Poison_Turret_Definition.poisonStatusProfile 연결.
 * - PoisonStatusProfileSO.deathBurstProfile 필요 시 연결.
 * - Poison_Turret_Stat Growth Profile SO에 Poison 전용 성장값 설정.
 * - Poison_Turret.prefab TargetFinder.targetCandidateFilterBehaviours에
 *   PoisonLethalTargetCandidateFilter 연결.
 * - Poison 시각효과는 StatusEffectVisualController Visual Slots에 연결.
 * - PoisonIcon은 Lethal Indicator Child Name과 Local Position Offset으로 조정.
 *
 * Electro_Turret:
 * - Electro_Turret_Definition.electroStatusProfile 연결.
 * - ElectroStatusProfileSO.shockStackVisualPrefab에 Volt Sphere 1 연결.
 * - ElectroStatusProfileSO.maxShockStackCount = 3.
 * - ElectroStatusProfileSO.shockStackDuration = 15.
 * - canElectroHitTriggerOverload는 현재 false 유지.
 * - Overload는 다음 단계에서 비-Electro 피해 경로와 연결한다.
 *
 * Zombie Prefabs:
 * - StatusEffectVisualController 존재 여부.
 * - FrostSlow RendererOverlay 슬롯.
 * - Poison Anchor 슬롯.
 * - PoisonFootAnchor / PoisonBodyAnchor 위치.
 * - PoisonIcon 자식 오브젝트 기본 비활성화.
 *
 * ==========================================================================================
 * 11. SHORT SUMMARY
 * ==========================================================================================
 *
 * - ProfileSO는 데이터.
 * - Payload는 전달값.
 * - Receiver는 받을 수 있다는 계약.
 * - Runtime은 대상별 상태.
 * - Utility는 계산과 범위/VFX 공통 처리.
 * - VisualController는 프리팹 비주얼 슬롯.
 * - TargetCandidateFilter는 터렛별 후보 제외 정책.
 * - NormalZombie / BossZombie는 상태이상 계산을 직접 소유하지 않는다.
 *
 * ==========================================================================================
 */
