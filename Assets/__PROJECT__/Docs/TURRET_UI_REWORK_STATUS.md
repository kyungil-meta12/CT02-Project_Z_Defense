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
