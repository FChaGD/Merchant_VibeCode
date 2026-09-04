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
- **에디터 인스톨러**: `Assets/Scripts/Editor/FormationUIBuilder.cs`의 `BuildDebugPanel` /
  `BuildDebugLabel` 메서드 + `Build` 안의 `BuildDebugPanel(panelRoot.transform)` 호출 한 줄
  (installer 파일 자체는 항상 Editor 전용이라 `#if` 불필요, `using Game.Core.DebugTools;` 로 참조).
- **상수**: `FormationUIElementIds.DebugPanelRoot`.
- **씬에 생성되는 오브젝트**: Hub.unity, Formation 패널 하위 `DebugPanel`(자식 라벨/입력창 포함).
- **제거 방법**: `Core/Debug/Formation/` 폴더 삭제 → `FormationPanel.cs`의 `#if UNITY_EDITOR` 블록
  4곳 삭제 → `Tools > Game > Build Hub Scene` 재실행 → Hub.unity에서 `DebugPanel`
  오브젝트가 정리되는지 확인 후 저장.

## 2. 상행 준비 UI - 지도 위 디버그 도시 배치 / 경로 연결

지역 데이터 시스템이 아직 없어서, 지도 위에 도시를 수동으로 찍고 경로를 그어 출발/도착 지정
기능(정식, 02번 기획)을 테스트할 수 있게 만든 임시 배치 도구.

- **경계**: `ITripDestinationReader`/`ITripDestinationAssigner`/`TripDestinationAssigner`/`TripRole`/
  `ITripRouteReader`/`ITripCurrentLocationReader`/`ITripCurrentLocationRepository`/
  `InMemoryTripCurrentLocationRepository`(도착지 지정 로직 + "현재 위치" 상태, 정식 로직, `Core/UI/Trip/`)는
  애초 리팩토링(배치 도구 신설)에서는 전혀 손대지 않았으나, 두 차례 예외적으로 이 경계를 넘었다:
  (1) **2026-09-03 도시 지도 Id 전면 정수화**(기획 15번 §8.2, 설계 20번 §9) - 도시 Id 타입을
  `string`→`int`(미배정 자리는 `int?`)로 통일. (2) **2026-09-03 출발/도착 지정 로직 전면 재설계**
  (기획 16번, 설계 21번) - 옛 `TripOriginDestinationAssigner`(출발/도착 두 역할 모두를 다루던 상태
  머신)를 폐기하고 `TripDestinationAssigner`(도착지 전용)로 교체, "현재 위치" 개념을 신설했다.
  `TripDestinationAssigner`가 `ITripRouteReader`/`ITripCurrentLocationReader` 인터페이스에만
  의존하는 구조는 유지되므로, 실제 지역/경로 데이터 시스템이 생기면 이 클래스 자체는 여전히 손댈
  필요가 없다(구현체만 교체). `TripDestinationAssigner`/`InMemoryTripCurrentLocationRepository`는
  전역 DI 싱글턴(`IManagedComponent`, `ManagerHierarchyInstaller`가 생성)이라 빌드에도 항상
  존재한다 - 다만 지금은 도시 지도 자체가 에디터 전용이라 빌드에서는 값이 바뀔 방법이 없을 뿐이다
  (설계 21번 §6).
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
    도시를 세션 동안만 들고 있는 임시 저장소(`ITripCityRepository`는 `ITripCityReader`를 상속 - 저장
    기능 전용 전체 조회 계약, 2026-09-03 도시 지도 데이터 저장 기능에서 추가됨).
  - `InMemoryTripRouteRepository.cs`, `ITripRouteRepository.cs` - 디버그로 그은 경로를 세션 동안만
    들고 있는 임시 저장소(`ITripRouteRepository`는 정식 `ITripRouteReader`를 상속 + 저장 기능 전용
    `GetAllRoutes()` 추가).
  - `TripCityMapPersistence.cs` - "저장" 버튼이 배치된 도시/경로를 엑셀 워크북(`Assets/Table/Trip/
    TripCityMap.xlsx`)으로 내보내는 에디터 전용 로직(2026-09-03 도입, 2026-09-03 엑셀 테이블화로
    개정 - Docs/설계/19번→20번). `TripCityMapAsset`을 더 이상 직접 쓰지 않는다 - `TryLoad`로 읽기만
    한다(그 에셋은 이제 `TripCityMapTableImporter`가 채운다, 아래 "제거 방법" 참고).
  - `TripMapInteractionCoordinator.cs` - 디버그 도시/경로 배선을 담당하는 조율자. 도착지 지정
    로직(`TripDestinationAssigner`)과 "현재 위치"(`ITripCurrentLocationReader`)는 더 이상 이 클래스가
    만들지 않는다 - 전역 DI로 주입받는다(`Bind()` 매개변수, 기획 16번, 설계 21번). 이 클래스 자체는
    여전히 디버그 저장소(`InMemoryTripCityRepository` 등)에 의존하므로 이 폴더에 있다 - 실제 지역/
    경로 시스템이 생기면 이 파일 전체를 그 시스템에 맞는 코디네이터로 새로 짜야 한다(주입받는
    `TripDestinationAssigner`는 그대로 재사용 가능).
    `Bind()`가 저장 버튼 배선 + `EnsureSavedMapLoaded()`(씬 진입 시 저장된 지도 자동 복원)도 담당한다.
- **정식 코드 연동 지점** (`Core/UI/Trip/TripPanel.cs`, 전부 `#if UNITY_EDITOR`로 표시돼 있음 - 단
  `currentLocationReader`/`destinationAssigner` 필드와 그걸 채우는 `RegisterTripUI` 매개변수는
  정식 DI 타입이라 예외):
  - `debugCityMarkerPrefab`/`debugRoadLinePrefab` 직렬화 필드
  - `debugCityPaletteView`/`debugRoadToggleView`/`debugCityBulkDeleteButton`/`debugRoadBulkDeleteButton`
    /`debugMapSaveButton`/`mapInteractionCoordinator` 필드
  - `TryBind` 안의 디버그 요소 조회 5줄
  - `RegisterTripUI` 안의 `SetupDebugMapInteraction()` 호출 한 줄
  - `SetupDebugMapInteraction` 메서드 전체
  - `RefreshStartButtonInteractable`는 `#if/#else`로 분기 - 디버그가 없으면 "상행 시작" 버튼은
    항상 활성 상태로 대체된다(조회 대상은 `destinationAssigner?.IsAssigned`).
- **상수**: `TripUIElementIds.cs`의 `DebugCityPaletteRoot` / `DebugRoadToggleButton` /
  `DebugCityBulkDeleteButton` / `DebugRoadBulkDeleteButton` / `DebugMapSaveButton` (문자열 상수라
  그대로 둬도 무해하지만, 완전히 정리하려면 함께 삭제).
- **에디터 인스톨러**: `Assets/Scripts/Editor/HubSceneInstaller.cs`의 `BuildTripDebugMapControls`,
  `GetOrCreateCityMarkerPrefab`, `GetOrCreateRoadLinePrefab` 메서드와 `BuildTripUI` 안의
  `BuildTripDebugMapControls(panelRoot.transform)` 호출 한 줄 (`using Game.Core.DebugTools;`로 참조).
- **생성되는 프리팹/오브젝트**:
  - 프리팹: `Assets/Prefabs/UI/Trip/TripDebugCityMarker.prefab`, `TripDebugRoadLine.prefab`.
  - Hub.unity, Trip 패널 하위 `DebugCityPalette`, `DebugRoadToggleButton`,
    `DebugCityBulkDeleteButton`, `DebugRoadBulkDeleteButton`, `DebugMapSaveButton`.
- **제거 방법**: 실제 지역/경로 데이터 시스템이 생겼을 때, `Core/Debug/Trip/` 폴더를 지우고
  `TripPanel.cs`의 `#if UNITY_EDITOR` 블록들을 지운 뒤(`RefreshStartButtonInteractable`은 `#else` 쪽만
  남긴다), `TripMapInteractionCoordinator`를 새 데이터 소스에 맞는 코디네이터로 교체하고
  `Tools > Game > Build Hub Scene` 재실행 → Hub.unity 저장. **주의**: 다음은 **제거 대상이 아니다**
  - 데이터 테이블 임포터들(`CharacterStatsTableImporter` 등)과 같은 성격의 정식 콘텐츠 파이프라인이라
  실제 지역 시스템이 이어받는다(Docs/기획/15번 §5, 설계 20번 §7):
  - `Core/UI/Trip/TripCityMapAsset.cs`와 그 `.asset` 인스턴스(`Assets/Prefabs/ScriptableObejct/TripCityMap.asset`)
  - `Core/UI/Trip/TripCityStringsTableAsset.cs`와 그 `.asset` 인스턴스(`Assets/Prefabs/ScriptableObejct/TripCityStringsTable.asset`, 도시 이름/설명)
  - `Core/UI/Trip/TripCityMapCoordinateConverter.cs`(좌표 정규화 변환)
  - `Assets/Scripts/Editor/TripCityMapTableImporter.cs`(엑셀→에셋 임포터)와 그 워크북(`Assets/Table/Trip/TripCityMap.xlsx`)
  - `Assets/Plugins/Editor/ClosedXML/`(엑셀 쓰기 라이브러리)
  - `Core/UI/Trip/`의 `ITripCurrentLocationReader.cs`/`ITripCurrentLocationRepository.cs`/
    `InMemoryTripCurrentLocationRepository.cs`/`ITripDestinationReader.cs`/`ITripDestinationAssigner.cs`/
    `TripDestinationAssigner.cs`(기획 16번, 설계 21번 - 정식 상태/로직, `ManagerHierarchyInstaller`의
    해당 배선 두 줄도 함께 유지)

  지워야 하는 건 `TripCityMapPersistence.cs`(저장 버튼이 엑셀로 내보내는 에디터 전용 로직)와 저장
  버튼 배선뿐이다.
- **알려진 버그(2026-09-03 발견/수정)**: 도시/경로 불러오기는 `TripPanel.Open()`이 패널을 활성화한
  "뒤"(`mapInteractionCoordinator.EnsureSavedMapLoaded()`)에 실행돼야 한다 - `Bind()`(등록 시점, 패널
  아직 비활성) 때 불러오면 `TripMapView.Content`가 아직 `null`이라 마커가 부모 없이 생성돼 화면에
  나타나지 않는다(저장 자체는 정상 동작 - 실제로 저장된 데이터를 다시 불러오지 못하는 것처럼 보이는
  버그였다). `TripMapView.Content`/`Viewport`가 패널 최초 활성화 전까지 지연 초기화된다는 제약은
  `TripMapView.cs`의 기존 주석에 이미 명시돼 있었다.

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
