# Turret UI Rework Status

## Current Goal

터렛 UI는 선택 허브와 기능별 팝업을 분리한다.

- 1번 클릭: 설치된 터렛의 사거리만 표시한다.
- 같은 터렛 2번 클릭: `TurretSelectPopup`을 표시한다.
- 선택 팝업: 업그레이드, 세부정보, 스킬, X 닫기만 담당한다.
- 업그레이드/진화/상세 스탯/스킬은 각각 전용 팝업에서 처리한다.

## Completed Today

- `TurretSelectionUIController`가 설치된 터렛 클릭을 받아 1번 클릭 사거리 표시, 같은 터렛 2번 클릭 선택 팝업 표시 흐름을 담당한다.
- `TurretSelectPopupUI`가 `Upgrade`, `Information`, `Skill`, `X`, 바깥 배경 클릭 이벤트를 상위 컨트롤러로 전달한다.
- `TurretSelectPopupUI`의 `Level`, `DPS`, `FireRate`는 TMP 원문 템플릿의 `{}` 구간만 값으로 치환한다.
- `TurretSelectPopupUI`의 `Note`는 `TurretDefinitionSO.shortDescription`을 표시한다.
- `TurretDefinitionSO.shortDescription`을 추가했고, TMP `<nobr>` 태그를 그대로 사용할 수 있다.
- `TurretUpgradePopupUI`는 현재/다음 레벨, DPS, 발사간격, 사거리, 변화량, 업그레이드 비용을 표시한다.
- `TurretUpgradePopupUI` 비용 슬롯은 `ResourceCost`와 `InventorySystem` 메타데이터를 사용해 재화 이름, 수량, 이미지를 표시한다.
- `TurretUpgradePopupUI`의 `LowPanel/Evolution` 버튼은 직접 진화하지 않고 `TurretEvolutionPopupUI`를 열도록 라우팅한다.
- `TurretEvolutionPopupUI`는 진화 분기 수에 따라 `MiddlePanel_A/B/C`를 전환하고, 현재 터렛/다음 후보 이미지와 이름, 후보별 진화 비용, 진화 실행 버튼을 표시한다.
- `TurretDetailPopupUI`는 현재 터렛의 기본 상세 스탯과 `TurretDamagePolishProfileSO` 기반 치명타/강타 확률을 읽기 전용으로 표시한다.
- `TurretSkillPopupUI`는 아직 실제 기능 없이 준비 중 상태를 담당한다.
- 모든 하위 팝업의 `BackButton`은 선택 팝업으로 돌아가는 방향으로 정리 중이다.

## 2026-07-01 Evolution Popup Structure

- `UpgradePopup`의 `LowPanel/EvolutionFrame/Evolution` 버튼은 직접 진화하지 않고 `EvolutionPopup`을 연다.
- `EvolutionPopup`은 선택된 터렛의 `TurretEvolutionProgressionSO` 기준으로 사용 가능한 다음 진화 후보 수를 계산한다.
- 후보 수가 1개면 `MiddlePanel_A`, 2개면 `MiddlePanel_B`, 3개 이상이면 `MiddlePanel_C`를 활성화한다. 현재 데이터 기준 `MiddlePanel_C`는 4분기용이다.
- 각 패널의 `CurrentTurretImage`는 현재 터렛 `TurretDefinitionSO.uiIcon`을 표시한다.
- 각 `NextTurretImage` 또는 `NextTurretImage_1~4`는 후보 `targetDefinition.uiIcon`을 표시하고, 후보 이름은 `targetDefinition.displayName`을 우선 사용한다.
- 후보 이미지를 클릭하면 즉시 진화하지 않고 선택 후보만 변경한다.
- 선택 후보가 바뀌면 `MiddleLowPanel/RequireSorceImagePanel`의 필요 재료 슬롯이 해당 후보의 `evolutionCosts` 기준으로 즉시 갱신된다.
- 실제 진화 실행은 하단 `LowPanel/EvolutionFrame/Evolution` 버튼에서만 수행한다.
- 선택된 후보의 `NextTurretImageFrame` 또는 `NextTurretImageFrame_1~4`는 붉은색으로 표시하고, 나머지 후보 프레임은 원래 색상으로 복구한다.

## 2026-07-01 Evolution Cost Slot Rules

- `RequireSorceText`는 큰 제목용 TMP이며 런타임에서 덮어쓰지 않는다. 씬에 입력된 `필요 재료` 문구를 유지한다.
- `RequireSorceImagePanel` 아래 `RequireSorceImageFrame 1~8`은 진화 비용 표시 슬롯이다.
- 각 슬롯은 `ItemName 1~8`, `ItemCount 1~8`, 아이템 이미지로 구성한다.
- 비용 아이템 이미지는 `InventorySystem.GetMetaData(currencyType).ItemImage`를 사용한다.
- 아이템 이름은 `InventorySystem.GetName(currencyType)`을 우선 사용하고, 없으면 `RewardCurrencyType` 이름을 사용한다.
- 수량은 `보유/필요` 형식으로 표시한다.
- 보유량이 부족하면 수량 TMP에 붉은색 Rich Text를 적용한다.
- 필요한 재료 수가 8개보다 적으면 남는 슬롯은 기본 이미지, 현재 씬 기준 `crosshair`, 를 유지하고 `ItemName`/`ItemCount` 텍스트는 비운다.

## 2026-07-01 Detail Popup Damage Polish Display

- `DescriptionPopup > TurretDescriptionPopupBackground > MiddlePanel > DetailInfoPanel` 아래 `CriticalChance`, `HeavyHitChance` TMP를 상세 수치 표시 대상에 추가했다.
- `TurretDetailPopupUI`는 현재 선택 터렛의 `TurretDefinitionSO.damagePolishProfile`에서 치명타/강타 확률을 읽어 백분율로 표시한다.
- Damage Polish Profile이 비어 있으면 치명타/강타 확률은 `0%`로 표시한다.
- 선택된 터렛이 없을 때는 다른 상세 수치와 동일하게 `-`를 표시한다.
- 기존 `statText` 단일 상세 문자열은 레거시 호환용으로만 유지한다. 신규 UI는 `DetailInfoPanel`의 개별 TMP를 기준으로 본다.
- Roslyn `OutOfMemoryException` 재발을 줄이기 위해 레거시 `statText` 문자열은 중첩 보간 문자열 대신 `string.Concat`과 미리 계산된 `damagePolishProfile` 인자를 사용한다.

## 2026-07-01 Button Reference Hardening

- `TurretSelectPopupUI`는 `BackgroundButton` 참조가 비어 있어도 팝업 루트의 `Image` 오브젝트에 런타임 `Button`을 보강해 바깥 클릭 닫기를 처리한다.
- `TurretPopupPageUI`의 공통 `Awake`/`OnDestroy` 흐름을 자식 팝업이 명시적으로 호출하도록 정리했다.
- `TurretUpgradePopupUI`와 `TurretEvolutionPopupUI`는 자식 전용 Back/X 버튼 참조가 비어 있어도 공통 `closeButton`/`backButton` 참조를 재사용한다.
- 같은 버튼에 공통 리스너와 자식 리스너가 중복 등록되지 않도록 방지했다.
- 현재 Main 씬의 `Canvas > Turret UI`에는 `SkillPopup` 오브젝트가 없고 `TurretSelectPopupUI`의 Skill 버튼은 준비 중 정책으로 비활성화되어 있다.

## 2026-07-02 Evolution Popup Runtime Context Fix

- `TurretEvolutionPopupUI`가 진화 성공 후 내부 `CurrentContext`만 새 터렛으로 갱신하던 문제를 수정했다.
- 하위 팝업이 변경된 선택 컨텍스트를 `TurretSelectionUIController`에 반영할 수 있도록 `TurretPopupPageUI.RequestSelectionContextUpdate`와 `TurretSelectionUIController.UpdateSelectionFromChild`를 추가했다.
- 진화 직후 상위 선택 컨텍스트와 사거리 표시가 새 터렛 기준으로 갱신되므로, 하단 Evolution 실행 뒤 Back으로 선택 팝업에 복귀해도 이전 터렛 참조가 남지 않는다.
- 진화 성공 후 `EvolutionPopup`에 머무르지 않고 새 터렛 기준 `TurretSelectPopup`으로 복귀한다. 이후 Upgrade/Detail/Evolution 루프를 같은 방식으로 다시 진행할 수 있다.

## 2026-07-02 Detail Popup Upgrade Route Fix

- `DescriptionPopup`의 `LowPanel/Upgrade` 버튼이 아무 동작도 하지 않던 문제를 수정했다.
- `TurretDetailPopupUI`에 `UpgradeRequested` 이벤트와 상세 팝업 전용 Upgrade 버튼 바인딩을 추가했다.
- `TurretSelectionUIController`가 상세 팝업의 `UpgradeRequested`를 기존 `OpenUpgradePopup` 경로에 연결하므로, 선택 팝업에서 Upgrade로 들어간 경우와 동일하게 업그레이드를 진행할 수 있다.

## 2026-07-02 Evolved Turret Reselection Fix

- 프리팹 교체 진화 성공 후 `TurretBaseSlot.CurrentTurret`가 새 터렛으로 갱신되지 않아, 모든 팝업을 닫고 슬롯/터렛을 다시 클릭할 때 선택 팝업이 열리지 않을 수 있던 문제를 수정했다.
- `TurretEvolutionPopupUI`가 진화 성공 직후 기존 슬롯에 `SetCurrentTurret(evolvedTurret)`를 호출해 슬롯 점유 상태를 새 터렛으로 동기화한다.
- `TurretSelectionUIController`의 슬롯 클릭 fallback은 `CurrentTurret`가 비어 있으면 `RefreshAndGetCurrentTurret()`로 빌드 포인트 아래 터렛을 한 번 재탐색한다.

## 2026-06-30 Placement Input Regression

### Symptom

- 머지 이후 `BottomBar`의 터렛 아이콘을 드래그해도 `TurretBase`에 터렛이 설치되지 않았다.
- 같은 시점에 화면 클릭 유지 후 마우스 이동으로 카메라를 움직이는 기능도 동작하지 않았다.
- `EventSystemDebugger`의 `RaycastAll` 진단 로그는 `TurretPlacementSlot_Button`, `BottomBar`, `CameraControlCanvas/Panel`을 정상 감지했다.
- 하지만 `TurretPlacementSlotUI.OnPointerDown/OnBeginDrag`와 `CameraTouchHandler.OnPointerDown/OnDrag` 로그는 전혀 찍히지 않았다.

### Cause

- UI 그래픽 레이캐스트 자체는 정상이었지만, `EventSystem`의 포인터 이벤트 콜백 전달 경로가 끊겨 있었다.
- `InputSystemUIInputModule` 참조가 머지 과정에서 현재 프로젝트의 `Assets/InputSystem_Actions.inputactions` GUID와 맞지 않는 상태가 되었고, 이후 GUID를 복구해도 런타임 포인터 콜백은 계속 들어오지 않았다.
- 결과적으로 `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`, `IPointerDownHandler` 기반 코드가 모두 실행되지 않아 터렛 배치와 카메라 드래그가 동시에 멈췄다.

### Fix

- `Assets/__PROJECT__/Scenes/PJY/EventSystemDebugger.cs`에 `Input.GetMouseButton*`와 `Input.touchCount` 기반의 `Legacy Pointer Bridge`를 추가했다.
- 브릿지는 기존 `EventSystem.current.RaycastAll` 결과의 최상단 UI 오브젝트를 기준으로 `PointerDown`, `BeginDrag`, `Drag`, `EndDrag`, `PointerUp`, `Click` 이벤트를 `ExecuteEvents`로 직접 전달한다.
- 이 브릿지 적용 후 `BottomBar` 터렛 드래그 배치와 카메라 드래그 이동이 다시 동작하는 것을 플레이 모드에서 확인했다.

### Diagnostic Keywords

- `[UI 레이캐스트 진단]`
- `[UI 입력 브릿지]`
- `[터렛 배치 슬롯 UI]`
- `[터렛 배치]`
- `[CameraTouchHandler]`

### Follow-Up

- `Legacy Pointer Bridge`는 현재 입력 복구용 호환 레이어다.
- 장기적으로는 `InputSystemUIInputModule`이 포인터 콜백을 보내지 못하는 정확한 에디터/액션 에셋 설정 원인을 다시 확인하고, 브릿지를 유지할지 제거할지 결정해야 한다.
- 브릿지를 유지하는 동안에는 `EventSystemDebugger`가 Main 씬에서 활성화되어 있어야 한다.

## 2026-07-02 Evolution Candidate Info Popup

### Goal

- `EvolutionPopup`에서 진화 후보 이미지를 짧게 한 번 클릭하면 기존처럼 선택과 비용 표시만 갱신한다.
- 이미 선택된 후보를 다시 클릭하거나, 후보 이미지를 0.5초 이상 누르고 있으면 `TurretInfoPopup`을 겹쳐 표시한다.
- `TurretInfoPopup`은 `EvolutionPopup`을 닫지 않고 위에 뜨며, Back 버튼과 상단 X 버튼은 정보 팝업만 닫는다.

### Fix

- `TurretEvolutionPopupUI`가 후보 버튼의 `PointerDown`, `PointerUp`, `PointerExit`을 감지하도록 후보별 포인터 전달 컴포넌트를 자동 연결한다.
- 후보를 누르고 있는 시간이 `candidateInfoHoldDuration` 이상이면 해당 진화 후보의 1레벨 기준 정보를 `TurretInfoPopup`에 표시한다.
- 이미 선택된 붉은 프레임 후보를 다시 클릭하면 같은 정보 팝업을 연다.
- 길게 누르기로 정보 팝업이 열린 경우, 같은 입력의 후속 클릭이 선택 로직을 다시 실행하지 않도록 억제한다.
- `TurretInfoPopupUI`를 추가해 후보 터렛의 이름, 레벨, 공격력, 발사간격, 아이콘을 표시하고, Information 버튼으로 1레벨 상세 스탯을 표시한다.
- `TurretInfoPopupUI`의 `HighPanel/ExitFrame/Button`을 별도 닫기 버튼으로 바인딩해 Back과 동일하게 `TurretInfoPopup`만 닫는다.
- 비활성 상태의 `TurretInfoPopup`에 런타임으로 `TurretInfoPopupUI`가 자동 부착되는 경우 `Awake`가 아직 실행되지 않아 `popupRoot`가 비어 있을 수 있으므로, `Show`와 `Hide`에서 참조를 즉시 보강한다.
- `Show`로 활성화되는 순간 `Awake`가 실행되더라도 현재 표시 대상이 있으면 다시 `Hide`하지 않도록 처리한다.

### Play Mode Check

- 첫 클릭: 후보 선택, 붉은 프레임 표시, 비용 표시만 변경.
- 선택된 후보 재클릭: `TurretInfoPopup` 표시.
- 후보 0.5초 이상 홀드: `TurretInfoPopup` 표시.
- 정보 팝업 Back: `TurretInfoPopup`만 닫힘.
- 정보 팝업 상단 X: `TurretInfoPopup`만 닫힘.
- 정보 팝업 Information: 진화 대상 터렛의 1레벨 상세 스탯 표시.

## 2026-07-02 Evolution Candidate Description Preview

### Goal

- `TurretInfoPopup`의 Information 버튼은 기존 팝업 내부 텍스트 변경이 아니라, 해당 진화 후보 터렛의 `DescriptionPopup`을 추가로 표시한다.
- `DescriptionPopup`은 1레벨 기준 상세 수치를 표시하며, `TurretInfoPopup`과 `EvolutionPopup`은 닫지 않는다.
- 진화 후보 정보 보기에서 열린 `TurretInfoPopup`의 Upgrade 버튼은 사용할 수 없게 비활성화한다.

### Fix

- `TurretDetailPopupUI`에 `ShowPreview(TurretDefinitionSO)`를 추가해 실제 설치 터렛 컨텍스트 없이 후보 터렛 정의를 1레벨 기준으로 표시한다.
- `TurretDetailPopupUI`는 미리보기 모드에서 `BackButtonFrame/BackButton`과 `CloseFrame/CloseButton` 입력을 `Hide`로 처리해 `DescriptionPopup`만 닫는다.
- `TurretDetailPopupUI`는 미리보기 모드에서 `LowPanel/UpgradeFrame`을 숨기고 Upgrade 버튼을 비활성화한다.
- `TurretInfoPopupUI`의 Information 버튼은 씬의 `DescriptionPopup`을 찾아 `ShowPreview`를 호출한다.
- `TurretInfoPopupUI`도 진화 후보 전용 팝업이므로 `LowPanel/UpgradeFrame`을 숨기고 Upgrade 버튼을 비활성화한다.

### Play Mode Check

- 진화 후보 재클릭 또는 0.5초 홀드: `TurretInfoPopup` 표시.
- `TurretInfoPopup` Information: 후보 터렛의 `DescriptionPopup` 표시.
- `DescriptionPopup` Back: `DescriptionPopup`만 닫히고 `TurretInfoPopup`으로 복귀.
- `DescriptionPopup` X: `DescriptionPopup`만 닫히고 `TurretInfoPopup`으로 복귀.
- 후보 상세 미리보기의 Upgrade 버튼은 비활성화.

## 2026-07-02 Turret UI Damage Display Correction

### Goal

- Turret UI의 `Damage` 표시는 초당 피해량이 아니라 `KKW/Turret_Scene/SO/Turret Stat Profile`에서 계산된 단발 피해량을 표시한다.
- 기존 씬 계층명이 `DPS`에서 `Damage`로 변경된 로컬 변경과 기존 `DPS` 계층명을 모두 지원한다.

### Fix

- `TurretSelectPopupUI`, `TurretInfoPopupUI`, `TurretDetailPopupUI`, `TurretUpgradePopupUI`의 Damage 표시값을 `damage * projectileCount / fireInterval` 계산값에서 `TurretRuntimeStat.damage`로 변경했다.
- `TurretUpgradePopupUI`의 Damage 변화량도 DPS 변화율이 아니라 `currentStat.damage` 대비 `nextStat.damage` 변화율로 계산한다.
- 자동 참조 경로는 `Damage`, `NextDamage`, `DamageDelta`를 우선 찾고, 기존 `DPS`, `NextDPS`, `DPSDelta`를 fallback으로 유지한다.

## Known Weak Points

- 일부 버튼과 TMP/Image 참조는 에디터 계층 이름에 의존하는 자동 연결 상태다.
- `TurretSelectPopupBackground`의 바깥 클릭용 `Button` 연결 여부를 에디터에서 계속 확인해야 한다.
- `UpgradePopup`, `EvolutionPopup`, `DetailPopup`의 실제 계층 이름은 현재 자동 연결 경로와 맞춰져 있지만, 계층 이름 변경 시 바인딩이 깨질 수 있다.
- `EvolutionPopup`은 후보 선택 후 하단 `Evolution` 버튼이 선택된 후보 인덱스로 진화하는지 회귀 검증이 필요하다.
- `SkillPopup`은 아직 기능 없음 상태이며, 버튼 비활성/준비 중 문구 정책을 나중에 확정해야 한다.
- Main 씬 기준 `SkillPopup` 오브젝트와 `TurretSelectionUIController.skillPopup` 참조가 아직 없다.
- 기존 옛날 터렛 UI 프리팹/오브젝트가 남아 있으면 더블클릭 또는 업그레이드 입력과 충돌할 수 있다.
- 터렛 업그레이드 후 사거리 표시 갱신, 선택 팝업 값 갱신, 하위 팝업 값 갱신이 모두 같은 타이밍에 맞는지 검증이 필요하다.
- `Legacy Pointer Bridge`와 `InputSystemUIInputModule` 기본 포인터 이벤트가 동시에 살아날 경우 같은 입력이 중복 전달될 수 있으므로, 추후 Input System 경로 복구 시 중복 입력 여부를 먼저 확인해야 한다.

## 2026-07-04 Turret Tech Tree Info UI First Pass

### Goal

- BottomBar에 별도 터렛 트리 버튼을 배치하고, 버튼 클릭 시 전체 터렛 계보를 Scroll View 기반 정보 화면으로 표시한다.
- 이 화면은 정보 전용이며 실제 진화 실행은 기존 설치 터렛 더블클릭 후 `TurretUpgradePopupUI` / `TurretEvolutionPopupUI` 경로에서만 처리한다.
- 잠금 상태 노드도 클릭 가능하며, 노드 클릭 시 터렛 1레벨 기준 상세 스펙과 `VideoClip` 프리뷰를 표시한다.

### Runtime Structure

- `TurretTechTreeViewProfileSO`는 터렛별 UI 표시 데이터와 프리뷰 `VideoClip`, 상태별 노드/라인 색상, 기본 해금 루트 터렛을 소유한다.
- `TurretTechTreeUIController`는 창 열기/닫기, 설치된 터렛 스캔, 노드 상태 계산, 수동 배치된 노드/라인 갱신을 담당한다.
- `TurretTechTreeNodeUI`는 에디터에서 배치한 노드 오브젝트에 붙이고, 각 노드의 `TurretDefinitionSO`와 아이콘/프레임/TMP/Button 참조를 연결한다.
- `TurretTechTreeLineUI`는 에디터에서 배치한 라인 오브젝트에 붙이고, 부모/자식 `TurretDefinitionSO`를 연결한다.
- `TurretTechTreeDetailPopupUI`는 상세 팝업에 있는 단일 `VideoPlayer`를 재사용해 선택 노드의 `VideoClip`만 교체 재생한다.
- `TurretTechTreeOpenButton`은 BottomBar의 터렛 트리 버튼에 붙여 `TurretTechTreeUIController.Show` 또는 `Toggle`을 호출한다.

### State Policy

- 내부 상태 우선순위는 `Unlocked > Ready > BlockedByCost > BlockedByLevel > Locked`이다.
- 현재 씬에 설치된 모든 활성 `TurretDefinitionRuntimeController`를 스캔하고, 동일 노드에 여러 상태 후보가 있으면 가장 높은 우선순위를 표시한다.
- 설치된 터렛의 모든 가능한 조상 경로는 `Unlocked`로 표시한다. 현재 런타임에는 실제 진화 경로 기록이 없으므로 3세대처럼 다중 부모를 가진 터렛은 가능한 부모 경로가 모두 조상으로 처리될 수 있다.
- 설치된 부모 터렛의 현재 티어 레벨이 진화 요구 레벨보다 낮으면 자식 노드는 `BlockedByLevel`이다.
- 부모 레벨 조건은 만족했지만 `TurretEvolutionEntry.evolutionCosts`를 현재 `InventorySystem`으로 지불할 수 없으면 `BlockedByCost`이다.
- 부모 레벨 조건과 비용 조건을 모두 만족하면 자식 노드는 `Ready`이다.
- 부모-자식 라인은 연결별로 따로 계산하며, 자식이 `Ready`이면 해당 라인만 Pulse 대상이 된다.

### Editor Setup Recommendation

- `Canvas > Turret Tech Tree Popup` 루트에는 `TurretTechTreeUIController`를 붙이고, 내부에 양방향 `ScrollRect`와 수동 배치 노드/라인을 둔다.
- 모든 노드 오브젝트에는 `TurretTechTreeNodeUI`를 붙이고 해당 `TurretDefinitionSO`를 지정한다.
- 모든 라인 오브젝트에는 `TurretTechTreeLineUI`를 붙이고 부모/자식 `TurretDefinitionSO`를 지정한다.
- 상세 팝업에는 `TurretTechTreeDetailPopupUI`, `VideoPlayer`, `RawImage`, 닫기 버튼, 스탯 TMP들을 연결한다.
- 모든 터렛의 프리뷰 영상은 `TurretTechTreeViewProfileSO`의 노드 데이터에 `VideoClip` 직접 참조로 연결한다.
- 1차 구현은 Scroll View 탐색만 지원한다. 줌은 추후 필요 시 별도 입력 레이어로 추가한다.

## 2026-07-05 Turret Tech Tree Video Clip Prep

### Current Asset Location

- 터렛 트리 프리뷰용 녹화/편집 영상은 `Assets/__PROJECT__/Scenes/KKW/Turret_Scene/Art/TurretVideoClip/` 아래에 둔다.
- `TurretTechTreeViewProfileSO`의 각 노드 `Preview Clip` 필드에는 이 폴더의 `VideoClip`을 직접 참조로 연결한다.
- 현재 확인된 영상은 28개이며 전체 용량은 약 `198.8MB`이다. 개발/연결 검증 단계에서는 허용 가능하지만 모바일 빌드 전에는 압축 검토가 필요하다.
- `3rd Gen/Ignition_Turret.mp4`가 약 `66.7MB`로 가장 크므로, 최종 빌드 전 압축 우선순위 1순위로 본다.

### Video Optimization Guideline

- 우선 전체 UI 동작 검증을 위해 현재 클립을 그대로 연결한다.
- 빌드 전 목표 총량은 대략 `50~90MB` 범위를 권장한다.
- 팝업 내 작은 프리뷰 기준 권장 설정은 `1280x720` 또는 `960x540`, `30fps`, 오디오 제거, 6~8초 루프, `1.5~3 Mbps` 비트레이트이다.
- 영상은 한 번에 하나만 재생하므로 런타임 메모리보다 앱 설치 용량 증가가 더 큰 리스크다.
- 모든 노드에 영상이 준비되어 있으므로, `previewClip` 누락 fallback은 안전장치로만 유지한다.

### Next Editor Work

- `Turret Tech Tree View Profile SO.asset`을 열고 3세대까지 모든 노드의 `Preview Clip`을 `TurretVideoClip` 폴더의 대응 영상으로 연결한다.
- 터렛 이름과 영상 파일명이 다를 경우, 임시로 연결표를 별도 메모한 뒤 파일명 정리는 한 번에 진행한다.
- `Detail_Popup_Panel`의 `VideoPlayer`, `RenderTexture`, `RawImage` 연결을 먼저 검증한 뒤 전체 노드 연결로 넘어간다.
- Play Mode에서 노드 1~2개만 먼저 클릭해 영상 루프/닫기/다른 노드 전환 시 이전 영상 정지가 정상인지 확인한다.

## 2026-07-07 Turret Tech Tree Detail Popup Setup

### Current Scene Status

- `Main` 씬의 `TurretTechTreePanel` 아래에 `Detail_Popup_Panel`을 생성했다.
- 현재 계층 순서는 `Scroll View` 다음에 `Detail_Popup_Panel`이 오므로, 상세 팝업이 트리 Scroll View 위에 렌더링되는 방향이 맞다.
- `Detail_Popup_Panel`에는 `TurretTechTreeDetailPopupUI`가 붙어 있으며, `popupRoot`는 자기 자신으로 연결되어 있다.
- `Popup_Background`, `Header`, `TurretNameText`, `StateText`, `PreviewArea`, `PreviewFrame`, `PreviewRawImage`, `FallbackIconImage`, `DescriptionText`, `StatPanel`, `PreviewVideoPlayer` 등 상세 팝업용 기본 오브젝트 일부를 구성했다.
- `TurretNameText`는 UI용 `TextMeshProUGUI`로 다시 만든 상태다. 3D `TextMeshPro`를 쓰면 Canvas UI에서 보이지 않을 수 있으므로 새 텍스트는 반드시 `UI > Text - TextMeshPro`로 만든다.
- `FallbackIconImage`는 `Image`와 `Preserve Aspect` 설정이 되어 있고, `PreviewRawImage`는 `RawImage` 컴포넌트가 붙어 있다.
- `Line_Lethal_Red_3_To_Ignition_Turret_H`의 `parentDefinition`은 현재 GUID 기준 `Lethal_Red_Definition 3.asset`을 가리키므로 `Lethal_Red_3 -> Ignition_Turret` 연결로 확인됐다.

### Known Incomplete Items

- `TurretTechTreeDetailPopupUI`의 Inspector 필드는 대부분 아직 비어 있다. `closeButton`, `nameText`, `stateText`, `descriptionText`, 모든 스탯 TMP, `videoPlayer`, `videoImage`, `fallbackIconImage`, `missingVideoMessageRoot`를 연결해야 한다.
- `TurretTechTreeUIController.detailPopup`도 아직 명시 연결이 필요하다. 상세 팝업 하위 구성이 끝나면 컨텍스트 메뉴 `참조 다시 연결`을 실행하고 씬을 저장한다.
- `PreviewVideoPlayer`에는 `VideoPlayer` 컴포넌트를 추가해야 한다.
- 세로형 프리뷰 영상 기준 RenderTexture를 하나 만들어 `PreviewVideoPlayer.targetTexture`와 `PreviewRawImage.texture`에 함께 연결해야 한다. 권장 시작값은 `540x960` 또는 `720x1280`이다.
- `PreviewArea`, `DescriptionText`, `Dim_Background` 등 상세 팝업 자식 오브젝트는 기본 활성 상태로 저장해야 한다. 루트인 `Detail_Popup_Panel`은 `TurretTechTreeDetailPopupUI.Awake()`에서 숨겨지므로, 자식이 비활성으로 저장되면 팝업 표시 때도 보이지 않을 수 있다.
- `DescriptionText`, `StateText`, `DamageText`, `RangeText`, `FireRateText`, `ProjectileSpeedText`, `ProjectileCountText`, `PierceCountText`, `MissingVideoMessage`가 모두 UI용 `TextMeshProUGUI`인지 확인해야 한다.
- `CloseButton`은 `Button` 컴포넌트와 클릭 가능한 `Image`를 가져야 하며, `TurretTechTreeDetailPopupUI.closeButton`에 연결해야 한다.
- `TurretTechTreeViewProfileSO`의 각 노드 `Preview Clip` 연결은 아직 별도 확인이 필요하다.

### Recommended Detail Popup Hierarchy

```text
Detail_Popup_Panel
- Dim_Background
- Popup_Background
  - Header
    - TurretNameText
    - StateText
    - CloseButton
  - PreviewArea
    - PreviewFrame
      - PreviewRawImage
      - FallbackIconImage
      - MissingVideoMessage
  - DescriptionText
  - StatPanel
    - DamageText
    - RangeText
    - FireRateText
    - ProjectileSpeedText
    - ProjectileCountText
    - PierceCountText
- PreviewVideoPlayer
```

### Next Editor Work

1. `PreviewVideoPlayer`에 `VideoPlayer`를 추가하고 `Play On Awake`는 끄고 `Render Mode`는 `Render Texture`, `Audio Output Mode`는 `None`으로 둔다.
2. `RT_TurretTechTreePreview` RenderTexture를 만들고 `PreviewVideoPlayer.targetTexture`와 `PreviewRawImage.texture`에 연결한다.
3. `TurretTechTreeDetailPopupUI`의 모든 필드를 실제 하위 오브젝트에 연결한다.
4. `TurretTechTreeUIController.detailPopup`에 `Detail_Popup_Panel`의 `TurretTechTreeDetailPopupUI`를 연결하거나 컨텍스트 메뉴 `참조 다시 연결`을 실행한다.
5. `PreviewArea`, `DescriptionText`, `Dim_Background`, `StatPanel` 등 상세 팝업 자식이 비활성으로 저장되어 있지 않은지 확인한다.
6. `Detail_Popup_Panel`을 기본 활성으로 저장해도 런타임에서는 `Awake()`에서 숨겨진다. 배치 중 미리보기가 필요하면 일시적으로 `TurretTechTreeDetailPopupUI` 컴포넌트를 꺼두고 확인한다.
7. Play Mode에서 노드 하나를 클릭해 이름, 상태, 설명, 스탯, fallback 아이콘, 닫기 버튼 동작을 먼저 확인한다.
8. RenderTexture와 실제 `PreviewClip`을 연결한 뒤 영상 재생, 루프, 닫기 시 정지, 다른 노드 클릭 시 클립 교체를 확인한다.

### Suggested Restart Prompt

```text
터렛 트리 상세 팝업 UI 이어서 하자.
33개 노드/56개 라인 연결은 끝났고, Detail_Popup_Panel 기본 계층은 만들었다.
현재 남은 일은 PreviewVideoPlayer에 VideoPlayer 추가, 세로형 RenderTexture 생성/연결,
TurretTechTreeDetailPopupUI와 TurretTechTreeUIController.detailPopup Inspector 참조 연결,
비활성 자식 오브젝트 확인, 노드 클릭 시 텍스트/아이콘/영상 프리뷰 Play Mode 검증이다.
```

## Next Work Plan

1. `Canvas > Turret UI` 아래 실제 오브젝트와 각 UI 스크립트의 Inspector 참조를 하나씩 대조한다.
2. `TurretSelectPopup`의 X, 바깥 클릭, 업그레이드, 세부정보, 스킬 버튼을 순서대로 검증한다.
3. `UpgradePopup`의 Back, Upgrade, Evolution 버튼을 검증한다.
4. `EvolutionPopup`의 Back, X, 후보 이미지 선택, 선택 프레임 색상, 비용 슬롯 이름/수량/이미지, Evolution 실행을 검증한다.
5. `DetailPopup`의 Back, X, 상세 스탯 표시, 엔지니어 상태 표시 확장 지점을 정리한다.
6. 모든 팝업의 닫힘 정책을 통일한다.
7. 필요한 경우 자동 연결 경로를 줄이고 Inspector 명시 참조 중심으로 정리한다.
8. 옛날 터렛 UI와 새 터렛 UI가 동시에 반응하는 경로를 제거한다.

## Suggested Restart Prompt

내일 다시 시작할 때는 이렇게 말하면 된다.

```text
어제 정리한 터렛 UI 리워크 흐름 이어서 하자.
현재 1번 클릭 사거리 표시, 2번 클릭 TurretSelectPopup, 선택 팝업의 기본 값 표시는 작동한다.
오늘은 Canvas > Turret UI 아래 실제 오브젝트 기준으로 오작동하는 버튼과 누락된 Inspector 참조를 하나씩 점검하고 고치자.
우선 TurretSelectPopup의 X/바깥클릭/Upgrade/Detail/Skill 버튼부터 확인하고,
그 다음 UpgradePopup의 Back/Upgrade/Evolution, EvolutionPopup의 Back/X/Evolution 순서로 보자.
기존 옛날 터렛 UI가 같이 반응하는지도 같이 확인해줘.
```

## Editor Checklist

- `TurretSelectPopupUI`에서 `Popup Root`, `Background Button`, `Close Button`, `Upgrade Button`, `Detail Button`, `Skill Button` 연결 확인.
- `TurretSelectPopupUI`에서 `Name`, `Level`, `DPS`, `FireRate`, `Note` TMP 연결 확인.
- `Note` TMP의 Rich Text 옵션 활성화 확인.
- `TurretDefinitionSO.shortDescription`에 터렛별 설명 입력 확인.
- `TurretUpgradePopupUI`에서 현재/다음 수치 TMP, 변화량 TMP, 재화 이름/수량/이미지, Back/Upgrade/Evolution 버튼 연결 확인.
- `TurretEvolutionPopupUI`에서 `MiddlePanel_A/B/C`, 현재/다음 터렛 이미지, 후보 프레임 이미지, 재화 이름/수량/이미지, Back/X/Evolution 버튼 연결 확인.
- `TurretEvolutionPopupUI`에서 후보 이미지 클릭 시 진화가 즉시 실행되지 않고 비용 표시와 선택 프레임만 바뀌는지 확인.
- `TurretDetailPopupUI`에서 `CriticalChance`, `HeavyHitChance` TMP가 현재 터렛의 Damage Polish Profile 확률을 백분율로 표시하는지 확인.
- `TurretSelectionUIController`에서 Select/Upgrade/Detail/Evolution Popup 참조 확인.
