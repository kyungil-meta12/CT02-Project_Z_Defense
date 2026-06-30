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
- `TurretEvolutionPopupUI`는 진화 전후 이름/이미지, 진화 비용, 진화 버튼의 1차 연결 구조를 가진다.
- `TurretDetailPopupUI`는 현재 터렛의 기본 상세 스탯을 읽기 전용으로 표시한다.
- `TurretSkillPopupUI`는 아직 실제 기능 없이 준비 중 상태를 담당한다.
- 모든 하위 팝업의 `BackButton`은 선택 팝업으로 돌아가는 방향으로 정리 중이다.

## Known Weak Points

- 일부 버튼과 TMP/Image 참조는 에디터 계층 이름에 의존하는 자동 연결 상태다.
- `TurretSelectPopupBackground`의 바깥 클릭용 `Button` 연결 여부를 에디터에서 계속 확인해야 한다.
- `UpgradePopup`, `EvolutionPopup`, `DetailPopup`의 실제 계층 이름이 코드 자동 경로와 완전히 일치하는지 추가 검증이 필요하다.
- `EvolutionPopup`은 실제 진화 전후 이미지, 비용 슬롯, 버튼 활성 조건을 플레이 모드에서 더 확인해야 한다.
- `SkillPopup`은 아직 기능 없음 상태이며, 버튼 비활성/준비 중 문구 정책을 나중에 확정해야 한다.
- 기존 옛날 터렛 UI 프리팹/오브젝트가 남아 있으면 더블클릭 또는 업그레이드 입력과 충돌할 수 있다.
- 터렛 업그레이드 후 사거리 표시 갱신, 선택 팝업 값 갱신, 하위 팝업 값 갱신이 모두 같은 타이밍에 맞는지 검증이 필요하다.

## Next Work Plan

1. `Canvas > Turret UI` 아래 실제 오브젝트와 각 UI 스크립트의 Inspector 참조를 하나씩 대조한다.
2. `TurretSelectPopup`의 X, 바깥 클릭, 업그레이드, 세부정보, 스킬 버튼을 순서대로 검증한다.
3. `UpgradePopup`의 Back, Upgrade, Evolution 버튼을 검증한다.
4. `EvolutionPopup`의 Back, X, Evolution 버튼과 비용 슬롯 이름/수량/이미지 표시를 검증한다.
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
- `TurretEvolutionPopupUI`에서 현재/다음 터렛 이미지, 재화 이름/수량/이미지, Back/X/Evolution 버튼 연결 확인.
- `TurretSelectionUIController`에서 Select/Upgrade/Detail/Evolution Popup 참조 확인.
