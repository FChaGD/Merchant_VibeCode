# 프로젝트 개요/목적

- 유니티 게임 제작 프로젝트
- 프로젝트 경로: C:\Users\addmin\Desktop\Cursortest\VibeProject1

# 버전 관리

- `Docs/`(기획/설계/제작 문서) 전체는 `.gitignore`에 등록되어 있으며, 이는 사용자가 이전 세션에서 의도적으로 요청한 설정이다. 커밋되지 않는 게 정상이니 "빠졌다"고 다시 보고하거나 임의로 `git add -f` 등으로 포함시키지 말 것. 문서 자체는 로컬 작업 폴더에는 그대로 남아 다음 세션에서도 정상적으로 읽힌다.

## 세션 인계

- 세션 종료를 준비할 때는 브랜치별로 작업 내용을 정리해 `Docs/세션 인계/` 폴더에 기록한다.
- `Docs/세션 인계/` 폴더에 해당 브랜치의 기록이 이미 존재하면, 새 파일을 만들지 않고 기존 문서를 수정·삭제·갱신해 관리한다.

# 코드/작업 컨벤션

UIManager/배치(Formation) UI/상행 준비(Trip) UI 작업(`Assets/Scripts/Core/UI/`)에서 확립된 패턴. 새 시스템을 만들 때도 이 패턴을 기본값으로 따른다.

## 씬 편집

- `.unity`/`.prefab` 파일을 텍스트 도구로 직접 편집하지 않는다. 반드시 `Assets/Scripts/Editor/`에 `[MenuItem("Tools/Game/...")]`가 붙은 정적 "get or create" 인스톨러 메서드를 작성해 재실행 가능하게 만든다(예: `ManagerHierarchyInstaller`, `HubSceneInstaller`, `FieldUIInstaller`).
- 인스톨러는 항상 기존 오브젝트/컴포넌트를 재사용(get-or-create)하고, 이름/구조가 바뀌어 남은 옛 오브젝트는 `DestroyChildIfExists` 같은 정리 로직으로 제거한다. 재실행해도 안전해야 한다.
- 여러 인스톨러가 공유하는 저수준 조립 로직(오브젝트 생성, 앵커 설정, 버튼/레이블/마커 부착 등)은 특정 기능의 인스톨러(`HubSceneInstaller` 등)에 두지 않는다. `EditorUIBuilder` 같은 이름의 공용 `internal static` 유틸리티 클래스로 뽑아서 각 인스톨러가 그 공용 유틸리티에 의존하게 한다 — 한 인스톨러가 다른 인스톨러의 내부 메서드를 `internal`로 열어 갖다 쓰지 않는다(결합 방향이 틀어짐).
- 작업 완료 후 사용자에게 "Tools > Game > ... 실행 → Ctrl+S로 씬 저장" 순서를 안내한다.

## DI / 매니저 계층

- 전역 매니저(`GameManager`, `UIManager` 등)는 `IManagedComponent`(`RegisterSelf(IDependencyRegistrar)`, `ResolveDependencies(IDependencyRegistrar)`)를 구현하고 `ManagerHierarchyInstaller`의 `managedComponents` 목록에 등록한다.
- 어떤 매니저에만 종속된 하위 컴포넌트(`HubUIController`, `FormationPanel`, `TripPanel`)는 전역 DI 대상이 아니다. 같은 GameObject에 부착하고, 소유 매니저가 `GetComponent<IXxx>()`로 직접 조회한다(없으면 `InvalidOperationException`으로 조기 실패).
- 아직 설계되지 않은 데이터 시스템에 대한 의존성(`ICaravanRosterProvider`, `IFormationRepository`, `ITripInfoProvider` 등)은 `registrar.TryResolve(out x)`로 선택적으로 조회하고, 소비자는 null 가능성을 항상 처리한다.

## Placeholder 패턴

- 실제 데이터/로직 시스템이 아직 없는 영역은 `Placeholder` 접두사 클래스(`PlaceholderCaravanRosterProvider`, `PlaceholderTripInfoProvider` 등)로 인터페이스만 채운다. 이 클래스도 `IManagedComponent`로 DI 등록해 소비자 코드는 실제 구현체와 동일하게 다룬다.
- Placeholder 클래스의 요약 주석에 "실제 시스템 설계 후 대체/제거 대상"임을 명시한다. 실제 시스템이 생기면 Placeholder 클래스와 그 전용 아이콘/데이터 생성 로직을 통째로 제거한다.
- 값이 없는 텍스트 필드는 창작하지 말고 "값 없음" 같은 자리표시자 문자열을 쓴다.

## UI 패널 패턴

- 화면 단위 UI는 `IUIPanel`(`PanelId`, `Open()`, `Close()`)을 구현하고 `UIManager.Open(panelId)`/`Close(panelId)`로만 제어한다.
- 패널의 `Open()`/`Close()`는 "표시/숨김"만 한다. 다른 패널로 전환했다가 돌아오는 등의 네비게이션은 패널이 직접 처리하지 않고 `UIManager.Close(PanelId)`를 호출해 위임한다 — 패널 내부에서 자기 `Close()`를 직접 부르지 않는다. `Close()` 메서드 위에 이 규칙을 주석으로 남긴다(`FormationPanel.cs`, `TripPanel.cs` 참조).
- 여러 패널 간 전환(예: 상행 준비 UI → 배치 UI → 되돌아가기)이 필요해지면 `UIManager`에 로직을 직접 쌓지 말고 `PanelNavigationStack` 같은 전담 협력 객체로 분리한다. `UIManager`는 "패널 조회/등록 + 협력 객체에 위임"만 담당한다.
- 화면상 UI 요소는 `UIElementMarker(id)`를 붙이고 `SceneUIRoot.TryGetElement<T>(id)`로 조회한다. ID 문자열은 매직스트링으로 흩어놓지 않고 기능별 `XxxUIElementIds` 정적 클래스(`HubUIElementIds`, `FormationUIElementIds`, `TripUIElementIds`)에 상수로 모은다.
- 패널 로직 컴포넌트는 Bootstrap 씬(영속)에, 실제 시각 요소는 콘텐츠 씬(Hub 등)의 `SceneUIRoot` 하위에 둔다. `RegisterXxxUI(...)`에서 `SceneManager.GetSceneByName`으로 대상 씬을 찾아 바인딩하고, 요소를 못 찾으면 `Debug.LogWarning`으로 조기에 드러낸다.

## 인터페이스 설계 (SOLID)

- **ISP**: 소비자가 실제로 쓰는 조작만 볼 수 있게 인터페이스를 쪼갠다. 읽기만 필요한 소비자에게 쓰기 메서드까지 포함된 인터페이스를 그대로 주입하지 않는다 — 읽기 전용 상위 인터페이스를 추출한다(`IFormationReader` ← `IFormationRepository` 사례).
- **SRP**: 한 클래스가 "조회/등록"과 "정책/흐름 제어"를 동시에 갖지 않는다. 책임이 늘어나면 새 협력 객체로 뽑아낸다(`UIManager` ↔ `PanelNavigationStack` 사례).
- **DIP**: 컴포넌트는 구체 클래스가 아니라 인터페이스에 의존하고, 실제 구현체는 `RegisterXxxUI(...)` 인자나 DI로 주입받는다.
- 새 인터페이스/클래스를 추가하기 전에 기존 것을 확장해 재사용할 수 있는지 먼저 확인한다(예: 편성 요약은 새 인터페이스를 만들지 않고 기존 `IFormationReader`를 재사용).

## 명명 규칙

- 인터페이스: `I` 접두사. Placeholder 구현체: `Placeholder` 접두사. 순수 표시 담당 컴포넌트: `XxxView` 접미사. 화면 단위 조율자: `XxxPanel`. UI 요소 ID 상수 모음: `XxxUIElementIds`.

## 최적화

- ID 조회는 `Dictionary<string, T>`로 O(1) 처리하고(`panelsById`, `elementsById`), 조회 대상은 씬 로드 시 한 번만 수집한다. 매 프레임 `GetComponentInChildren`/`Find` 등으로 UI 트리를 훑지 않는다.
- 입력 반응은 `Update()` 폴링이 아니라 이벤트/콜백(`onClick`, `IScrollHandler.OnScroll` 등)으로 처리한다.
- UI 오브젝트는 매번 `Destroy`+`Instantiate`하지 않고 get-or-create로 재사용한다(인스톨러, 슬롯/아이콘 렌더링 공통).

## 주석/문체

- 클래스/메서드 상단 요약 주석은 한국어로, "무엇을 하는지"보다 "왜 이런 구조인지(비직관적인 제약·이유)"를 우선 적는다. 자명한 내용은 적지 않는다.
