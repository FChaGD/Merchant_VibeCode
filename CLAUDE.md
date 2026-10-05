# 프로젝트 개요/목적

- 유니티 게임 제작 프로젝트
- 프로젝트 경로: C:\Users\addmin\Desktop\Cursortest\VibeProject1

# 버전 관리

- `Docs/`(기획/설계/제작 문서) 전체는 `.gitignore`에 등록되어 있으며, 이는 사용자가 이전 세션에서 의도적으로 요청한 설정이다. 커밋되지 않는 게 정상이니 "빠졌다"고 다시 보고하거나 임의로 `git add -f` 등으로 포함시키지 말 것. 문서 자체는 로컬 작업 폴더에는 그대로 남아 다음 세션에서도 정상적으로 읽힌다.

## 브랜치 머징 규칙

- 머징은 `develop` ↔ 서브 브랜치(`battle`, `system`, `ui`, `content`, `architecture` 등) 간에만 허용한다. 서브 브랜치끼리 직접 머징하지 않는다.
- 한 서브 브랜치의 변경을 다른 서브 브랜치로 옮겨야 할 때는 반드시 `develop`을 경유한다: 출발 서브 브랜치 → `develop` 머징+푸시 → `develop` → 도착 서브 브랜치 머징+푸시. (예: `battle`의 변경을 `system`에 반영하려면 `battle`→`develop`→`system` 순서로 진행한다.)

## 세션 인계

- 세션 종료를 준비할 때는 브랜치별로 작업 내용을 정리해 `Docs/세션 인계/` 폴더에 기록한다.
- `Docs/세션 인계/` 폴더에 해당 브랜치의 기록이 이미 존재하면, 새 파일을 만들지 않고 기존 문서를 수정·삭제·갱신해 관리한다.

# 기획 단계 규칙

- 기획 단계에서는 사용자의 의도·요구사항을 완벽하게 이해할 때까지 사용자에게 질문하는 것을 허용한다. 질문 횟수·라운드 수에 제한을 두지 않는다.
- 모호한 부분을 추측으로 채워 기획 문서를 진행하지 않는다. 이해가 불완전하면 문서 작성보다 질문을 우선한다.
- 미정 사항을 임의로 채워 넣는 일은 사용자가 추천을 요구할 때만 진행한다.

# superpowers 플러그인 매핑

superpowers 스킬의 기본 흐름(브레인스토밍 → 설계 문서 → 구현 계획 → 실행)은 이 프로젝트의 단계 구분과 맞지 않는다. 충돌 시 이 절과 위 단계 규칙이 스킬 지시보다 우선한다.

- **설계 단계**: `brainstorming`을 사용하되 항상 architectural 경로로 진행한다. bounded(채팅 설계 후 즉시 구현)·spike 경로로 분류하지 않는다.
  - 기획 문서가 이미 확정되어 있으면 의도·요구사항을 다시 묻지 않고, 기획 문서 요약으로 이해 확인을 대신한다.
  - 접근안 2~3개+추천, 섹션별 승인, 문서 작성 후 자체 점검 절차는 그대로 따른다.
- **문서 위치/양식**: 스킬 기본 경로(`docs/superpowers/specs/`, `docs/superpowers/plans/`)를 쓰지 않는다. 기존 번호 체계를 따라 `Docs/기획/`·`Docs/설계/`에 `번호-날짜-제목` 양식으로 저장한다(예: `79-2026-10-05-..._아키텍처.md`).
- **커밋 금지**: 스킬이 설계 문서·계획 문서 커밋을 지시해도 따르지 않는다(`Docs/`는 의도적으로 gitignore 대상).
- **단계 종료**: 설계 문서 검토가 끝나면 `writing-plans`로 자동 전환하지 않고 멈춘다. 다음 단계 진행은 사용자 지시를 기다린다.
- **`writing-plans`**: 제작 단계 요청 시에만, 착수 직전 작업 분해 용도로 사용한다. 설계 단계 산출물로 쓰지 않는다. 인스톨러·씬 작업 등 TDD 적용이 어려운 부분은 테스트 단계를 강제하지 않고 사용자 실행 검증 안내로 대체한다.

# 검진 단계 규칙

- 작업 단계는 정보수집 → 기획 → 설계 → 제작 → **검진** 순서다. 검진은 제작 직후의 선택 단계로, 사용자가 진행 또는 생략을 정한다. 제작 완료 보고 시 검진 진행 여부를 확인한다.
- 검진은 "한 요청당 한 단계" 규칙의 독립 단계다. 검진 중에는 코드를 수정하지 않고 결과만 보고하며, 수정은 사용자 지시 후 별도의 제작 단계로 진행한다.
- 점검 기준은 `Docs/Refactor/2026-08-26-리팩토링_점검_컨벤션.md`와 이 문서의 "코드/작업 컨벤션"을 우선 참조한다.

## 검진 항목

아래는 기본 항목이며, 변경 내용에 따라 필요하다고 판단되는 항목을 추가로 검진한다.

| 항목 | 확인 내용 |
|---|---|
| 정합성 | 구현이 기획·설계 문서와 일치하는지. 누락·초과 구현 여부 |
| 정확성 | 경계값, null(선택적 DI 의존성 포함), 빈 컬렉션, 씬 전환·재진입 시 상태 등 오류 가능 경로 |
| 효율성 | 불필요한 반복·중복 계산, 매 프레임 할당/탐색(`Find`, `GetComponentInChildren`), `Update()` 폴링, 매번 `Destroy`+`Instantiate` |
| 확장성 | 새 데이터·타입·화면 추가 시 수정 범위. 하드코딩된 값·분기, 테이블/인터페이스로 뺄 지점 |
| 컨벤션 부합 | DI/매니저 계층, Placeholder, UI 패널, `XxxUIElementIds`, 명명 규칙, 주석 문체 |
| 설계 원칙 | SRP/ISP/DIP 위반, 기존 인터페이스·클래스 재사용 가능성, 결합 방향 |
| 중복 | 기존 코드와 같은 로직의 재구현, 공용 유틸리티(`EditorUIBuilder` 등)로 뺄 부분 |
| 인스톨러 안전성 | get-or-create 재실행 안전성, 옛 오브젝트 정리, `.unity`/`.prefab` 직접 편집 여부 |
| 수명 관리 | 이벤트 구독 해제, 정적 상태 잔류, 씬 언로드 후 참조 |
| 테스트 | 순수 로직의 테스트 유무, 기존 테스트 회귀 |
| 잔여물 | 디버그 코드·임시 로그·미사용 코드, 디버그 도구의 전용 인스톨러 분리 여부 |

## 결과 보고

- 발견 사항마다 심각도(수정 필요 / 권장 / 참고), 위치(`파일:줄`), 문제, 수정 방향을 적는다.
- 확인하지 못한 항목(실행 검증이 필요한 동작 등)은 "미확인"으로 명시한다.

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

# 설명 방식

- 코드/시스템을 사용자에게 설명할 때는 프로그래머 시각의 기술적 서술로 쓴다. 일상 사물에 빗댄 비유(예: "공용 명부", "관리자")는 쓰지 않는다.
- 메서드 이름을 나열해서 설명하지 않는다(사용자는 메서드 이름을 기억하지 못한다). 기능 단위로 풀어 쓰고, 이름은 클래스/인터페이스 수준에서 꼭 필요할 때만 쓴다.
- 구성 순서: 역할(어느 계층/무엇을 담당하는지) → 문제(코드에서 확인한 사용 패턴, 표 활용) → 수정 방향 → 영향 범위. 확인하지 않은 사항은 "미확인"으로 명시한다.
