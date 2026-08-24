# 디버그/테스트 전용 기능 목록

정식 콘텐츠가 아니라 개발 편의를 위해 임시로 넣은 기능들을 모아둔다. 세 기능 모두 리팩토링을 거쳐
`Assets/Scripts/Core/Debug/`, `Assets/Scripts/Editor/Debug/` 아래 각 하위 폴더로 격리되어 있고,
런타임 코드는 전부 `#if UNITY_EDITOR`로 감싸 실제 게임 빌드에는 포함되지 않는다(에디터 Play에서는
`UNITY_EDITOR`가 항상 정의되므로 평소 개발 흐름에는 영향이 없다).

공통 제거 원칙: 각 기능의 "정식 코드 연동 지점"에 나열된 `#if UNITY_EDITOR ... #endif` 블록은 전부
"이 기능을 지울 때 함께 지워야 하는 부분"이라는 뜻으로 이미 소스에 주석이 달려 있다. 즉 제거 작업은
① 해당 Debug 폴더 삭제 → ② 정식 코드 파일에서 그 블록만 삭제 → ③ 관련 에디터 인스톨러 재실행,
세 단계로 항상 동일하다.

## 1. Hub 배치(Formation) UI - 그리드 열/행·타일 크기 조절 패널

Play 모드에서 배치 UI 그리드의 열(X)/행(Y) 수와 타일 크기를 즉시 바꿔보기 위한 온스크린 패널.

- **런타임**: `Assets/Scripts/Core/Debug/Formation/FormationGridDebugView.cs` (`#if UNITY_EDITOR`)
- **정식 코드 연동 지점** (`Core/UI/Formation/FormationPanel.cs`, 전부 `#if UNITY_EDITOR`로 표시돼 있음):
  - `debugView` 필드
  - `TryBind`의 `DebugPanelRoot` 조회 한 줄
  - `Open()`의 `debugView?.Initialize(...)` 호출 한 줄
  - `HandleDebugApply` / `ResizeGrid` / `ResizeSlotSize` 메서드 블록(디버그 패널의 유일한 호출부라 통째로 묶여 있음)
- **에디터 인스톨러**: `Assets/Scripts/Editor/FormationUIInstaller.cs`의 `BuildDebugPanel` /
  `BuildDebugLabel` 메서드 + `BuildFormationUI` 안의 `BuildDebugPanel(panelRoot.transform)` 호출 한 줄
  (installer 파일 자체는 항상 Editor 전용이라 `#if` 불필요, `using Game.Core.DebugTools;` 로 참조).
- **상수**: `FormationUIElementIds.DebugPanelRoot`.
- **씬에 생성되는 오브젝트**: Hub.unity, Formation 패널 하위 `DebugPanel`(자식 라벨/입력창 포함).
- **제거 방법**: `Core/Debug/Formation/` 폴더 삭제 → `FormationPanel.cs`의 `#if UNITY_EDITOR` 블록
  4곳 삭제 → `Tools > Game > Build Formation Hierarchy` 재실행 → Hub.unity에서 `DebugPanel`
  오브젝트가 정리되는지 확인 후 저장.

## 2. 상행 준비 UI - 지도 위 디버그 도시 배치 / 경로 연결

지역 데이터 시스템이 아직 없어서, 지도 위에 도시를 수동으로 찍고 경로를 그어 출발/도착 지정
기능(정식, 02번 기획)을 테스트할 수 있게 만든 임시 배치 도구.

- **경계**: `TripOriginDestinationAssigner`/`ITripOriginDestinationReader`/`ITripOriginDestinationAssigner`
  /`TripRole`/`ITripRouteReader`(출발/도착 지정 상태 머신, 정식 로직, `Core/UI/Trip/`)는 이 리팩토링에서
  전혀 손대지 않았다. `TripOriginDestinationAssigner`는 `ITripRouteReader` 인터페이스에만 의존해 지금은
  아래 디버그 구현체가 그 데이터를 대지만, 실제 지역/경로 데이터 시스템이 생겨도 이 클래스 자체는
  손댈 필요가 없다.
- **디버그 전용 폴더**: `Assets/Scripts/Core/Debug/Trip/` (전부 `#if UNITY_EDITOR`, `Game.Core.DebugTools`
  네임스페이스). `Core/UI/Trip/`에 있던 아래 파일들을 원래 `.meta`(GUID)를 유지한 채 이 폴더로
  옮겼다 - 씬/프리팹의 컴포넌트 참조는 그대로 유지된다.
  - `TripDebugCityPaletteView.cs` - 팔레트에서 도시 아이콘을 드래그해 지도에 배치하는 UI.
  - `TripDebugCityMarkerView.cs` - 지도 위 도시 마커 표시/드래그.
  - `TripDebugRoadToggleView.cs` - "경로 연결 모드" on/off 토글 버튼.
  - `TripDebugRoadModeController.cs`, `ITripDebugRoadModeController.cs`, `ITripDebugRoadModeReader.cs` -
    경로 연결 모드 상태 보관.
  - `TripDebugRoadLineView.cs` - 지도 위 경로 선 표시.
  - `MoveCityDragBehavior.cs`, `DrawRoadDragBehavior.cs`, `ICityDragBehavior.cs` - 드래그로 도시
    이동/경로 그리기 처리.
  - `TripCity.cs` - 디버그로 배치한 도시의 좌표 데이터 구조체.
  - `InMemoryTripCityRepository.cs`, `ITripCityRepository.cs`, `ITripCityReader.cs` - 디버그 배치
    도시를 세션 동안만 들고 있는 임시 저장소.
  - `InMemoryTripRouteRepository.cs`, `ITripRouteRepository.cs` - 디버그로 그은 경로를 세션 동안만
    들고 있는 임시 저장소(`ITripRouteRepository`는 정식 `ITripRouteReader`를 상속).
  - `TripMapInteractionCoordinator.cs` - 디버그 도시/경로 배선과 `TripOriginDestinationAssigner`
    생성·연결을 모두 담당하는 조율자. 이 클래스 자체가 디버그 저장소(`InMemoryTripCityRepository` 등)에
    의존하므로 이 폴더에 있다 - 실제 지역/경로 시스템이 생기면 이 파일 전체를 그 시스템에 맞는
    코디네이터로 새로 짜야 한다(내부에서 만드는 `TripOriginDestinationAssigner`는 그대로 재사용 가능).
- **정식 코드 연동 지점** (`Core/UI/Trip/TripPanel.cs`, 전부 `#if UNITY_EDITOR`로 표시돼 있음):
  - `debugCityMarkerPrefab`/`debugRoadLinePrefab` 직렬화 필드
  - `debugCityPaletteView`/`debugRoadToggleView`/`debugCityBulkDeleteButton`/`debugRoadBulkDeleteButton`
    /`mapInteractionCoordinator` 필드
  - `TryBind` 안의 디버그 요소 조회 4줄
  - `RegisterTripUI` 안의 `SetupDebugMapInteraction()` 호출 한 줄
  - `SetupDebugMapInteraction` 메서드 전체
  - `RefreshStartButtonInteractable`는 `#if/#else`로 분기 - 디버그가 없으면 "상행 시작" 버튼은
    항상 활성 상태로 대체된다.
- **상수**: `TripUIElementIds.cs`의 `DebugCityPaletteRoot` / `DebugRoadToggleButton` /
  `DebugCityBulkDeleteButton` / `DebugRoadBulkDeleteButton` (문자열 상수라 그대로 둬도 무해하지만,
  완전히 정리하려면 함께 삭제).
- **에디터 인스톨러**: `Assets/Scripts/Editor/TripUIInstaller.cs`의 `BuildDebugMapControls`,
  `GetOrCreateCityMarkerPrefab`, `GetOrCreateRoadLinePrefab` 메서드와 `BuildTripUI` 안의
  `BuildDebugMapControls(panelRoot.transform)` 호출 한 줄 (`using Game.Core.DebugTools;`로 참조).
- **생성되는 프리팹/오브젝트**:
  - 프리팹: `Assets/Prefabs/UI/Trip/TripDebugCityMarker.prefab`, `TripDebugRoadLine.prefab`.
  - Hub.unity, Trip 패널 하위 `DebugCityPalette`, `DebugRoadToggleButton`,
    `DebugCityBulkDeleteButton`, `DebugRoadBulkDeleteButton`.
- **제거 방법**: 실제 지역/경로 데이터 시스템이 생겼을 때, `Core/Debug/Trip/` 폴더를 지우고
  `TripPanel.cs`의 `#if UNITY_EDITOR` 블록들을 지운 뒤(`RefreshStartButtonInteractable`은 `#else` 쪽만
  남긴다), 새 데이터 소스로 `TripOriginDestinationAssigner`를 생성/주입하는 코드로 교체하고
  `Tools > Game > Build Trip UI` 재실행 → Hub.unity 저장.

## 3. Bootstrap 우회 진입 감지 가드

Hub.unity/Field.unity 등 콘텐츠 씬을 Bootstrap 없이 단독 Play했을 때(매니저 부재) 자동으로 Bootstrap을
거쳐 원래 씬으로 되돌려주는 에디터 전용 도구. 앞의 두 기능과 달리 정식 로직과 전혀 얽혀있지 않고
완전히 독립된 폴더로 분리되어 있다.

- **런타임**: `Assets/Scripts/Core/Debug/DebugBootstrapReentryGuard.cs` (`#if UNITY_EDITOR`로 빌드 제외)
- **에디터 인스톨러**: `Assets/Scripts/Editor/Debug/DebugBootstrapReentryGuardInstaller.cs`
  (`Tools/Game/Debug/Install Bootstrap Reentry Guards`, `Remove Bootstrap Reentry Guards`)
- **씬에 생성되는 오브젝트**: Build Settings에 등록된 콘텐츠 씬(Bootstrap 제외) 각각의 루트에
  `DebugBootstrapReentryGuard` 오브젝트 1개.
- **제거 방법**: `Tools > Game > Debug > Remove Bootstrap Reentry Guards` 실행 →
  `Core/Debug/`, `Editor/Debug/` 두 폴더 삭제. 기존 파일은 전혀 수정하지 않았으므로 이 두 단계만으로
  완전히 걷어낼 수 있다.
