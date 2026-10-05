using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Hub에서 쓰는 배치(Formation) UI 조율자 - 로컬 편집 상태(currentLayout)를 들고 있다가 "적용"
    /// 버튼을 눌렀을 때만 IFormationRepository에 반영한다. 적용 없이 닫으면 세션 상태를 그냥 버린다 -
    /// 다음에 열 때 항상 repository에서 다시 불러오므로 별도의 되돌리기 로직 없이 "마지막 적용
    /// 상태로 복귀"가 성립한다(기존 FormationPanel 동작 그대로, Docs/설계/25번 §2.3 - Field는 이
    /// 모델을 쓰지 않고 FieldFormationPanel이 즉시 반영 모델로 대신 담당한다).
    /// 그리드/팔레트 렌더링과 드래그 이벤트 중계는 공용 로직(FormationGridEditor)에 위임하고, 이
    /// 클래스는 "드롭 시 무엇을 반영할지"(IFormationEditingHandler)와 적용 버튼만 담당한다.
    /// 배치·이동·제거는 마차 중심 대열 규칙(FormationAreaRules, Docs/기획/59번)으로 판정하고, 허용된 결과(자동 해제 포함)만 로컬 사본에 반영한다.
    /// </summary>
    public class HubFormationPanel : MonoBehaviour, IUIPanel, IFormationEditingHandler, ISceneLoadingTaskSource
    {
        [SerializeField] private FormationUnitIconView dragGhostPrefab;

        public string PanelId => UIPanelIds.Formation;

        private FormationGridEditor gridEditor;
        private Button applyButton;
        private Button closeButton;

        private IFormationRepository repository;
        private ICaravanRosterProvider rosterProvider;
        private IUIManager uiManager;
        // 디버그 핀(에디터 전용 도구, 같은 UIManager 오브젝트에 설치된 경우만) - 없으면 null.
        private IFormationDebugAreaSource debugAreaSource;

        private FormationLayout currentLayout;

        public void RegisterFormationUI(SceneUIRoot sceneUIRoot, ICaravanRosterProvider rosterProvider, IFormationRepository repository, IUnitConditionRepository conditionRepository, IUIManager uiManager)
        {
            this.repository = repository;
            this.rosterProvider = rosterProvider;
            this.uiManager = uiManager;
            debugAreaSource = GetComponent<IFormationDebugAreaSource>();

            gridEditor = new FormationGridEditor(this);
            if (!gridEditor.TryBind(sceneUIRoot, dragGhostPrefab))
            {
                return;
            }

            if (!sceneUIRoot.TryGetElement<Button>(FormationUIElementIds.ApplyButton, out applyButton))
            {
                Debug.LogWarning($"Formation UI에서 '{FormationUIElementIds.ApplyButton}' 요소를 찾을 수 없다.");
                return;
            }

            if (!sceneUIRoot.TryGetElement<Button>(FormationUIElementIds.CloseButton, out closeButton))
            {
                Debug.LogWarning($"Formation UI에서 '{FormationUIElementIds.CloseButton}' 요소를 찾을 수 없다.");
                return;
            }

            gridEditor.SetSources(rosterProvider, conditionRepository);
            // 대열 외곽선은 마을 정비창에만 표시한다(디버그 도구, 사용자 결정 2026-09-29) - 상행 정비창은 넘기지 않는다.
            gridEditor.SetAreaOutline(GetComponent<IFormationAreaOutline>());

            applyButton.onClick.RemoveAllListeners();
            applyButton.onClick.AddListener(HandleApply);

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => uiManager.Close(PanelId));
        }

        public void Open()
        {
            currentLayout = BuildInitialLayout();
            gridEditor.Open();
        }

        // 마을 로딩 중 칸 미리 생성(Docs/설계/67번 §4.4). UI 연결에 실패해 편집기가 없으면 아무것도 하지 않는다.
        public ContentSceneId LoadingScene => ContentSceneId.Hub;

        public IEnumerable<ISceneLoadingTask> GetLoadingTasks()
        {
            yield return new ActionSceneLoadingTask("상단 배치 준비 중", () =>
            {
                if (gridEditor == null) return;
                currentLayout = BuildInitialLayout();
                gridEditor.Prewarm();
            });
        }

        // 순수 "숨기기"만 한다. 상행 준비 UI 등으로 되돌아가는 네비게이션은 UIManager.Close(PanelId)의
        // 책임이므로 버튼 등 외부에서 패널을 닫을 때는 이 메서드를 직접 호출하지 말고 반드시
        // uiManager.Close(PanelId)를 거칠 것.
        public void Close() => gridEditor?.Close();

        private FormationLayout BuildInitialLayout()
        {
            if (repository != null && repository.TryLoadCurrent(out var saved))
            {
                return saved.Clone();
            }

            return FormationLayout.CreateDefault();
        }

        private void HandleApply()
        {
            if (repository == null)
            {
                Debug.LogWarning($"{nameof(IFormationRepository)}가 연결되어 있지 않아 배치를 상행에 적용하지 못했다.");
                return;
            }

            repository.Apply(currentLayout.Clone());
        }

        // IFormationEditingHandler 구현 - 전부 로컬 currentLayout만 건드린다(Apply 전까지 미반영).
        // "배경 진행 활동" 관련 멤버(IsUnitReserved/HandleRedirectMove/GetActiveActivities)는 Hub에
        // 개념 자체가 없어 IFormationActivityHandler로 분리됐고, Hub는 그 인터페이스를 구현하지
        // 않는다(ISP, Docs/Refactor/2026-09-08_공통.md §6.3 수정 G).
        public FormationLayout GetDisplayLayout() => currentLayout;

        public IReadOnlyList<FormationAreaPin> GetAreaPins() => debugAreaSource?.Pins;

        // 마차 자유 배치라 연결 가능한 칸까지 보여야 한다 - 상행 중 정리 모드와 같은 계산(FormationVisibleMargin).
        public int GetVisibleMarginCells() => FormationVisibleMargin.ForAnywhereWagons(rosterProvider);

        public bool ShowsInPalette(IFormationUnit unit) => true;

        // 연결 기준은 NoNewSplit(설계 79번 §9.3) - 전투로 끊어진 채 돌아온 대열을 한 번에 잇지 못해도 덩어리 수를 늘리지 않는 편집은 허용한다.
        // 연결된 상태에서는 RequireConnected와 결과가 같다(설계 79번 §5.5).
        // 점유 칸에 드롭하면 기존 유닛은 배치에서 빠진다(상행 관리 데이터 삭제 아님) - 빠지는 유닛이 마차면 연결 판정을 받는다.
        // 마을은 마차 자유 배치(기획 59번 §3.4) - 마차는 판 어디든, 다른 마차가 있으면 연결만 확인.
        public bool HandlePaletteDrop(IFormationUnit unit, int targetSlotIndex)
            => Commit(FormationAreaRules.Place(currentLayout, unit, targetSlotIndex, FormationAreaRules.LookupFrom(rosterProvider), GetAreaPins(), WagonPlacement.Anywhere, ConnectivityRule.NoNewSplit));

        // 목표 칸이 점유돼 있으면 맞바꾼다.
        public bool HandleGridMove(string unitId, int originSlotIndex, int targetSlotIndex)
            => Commit(FormationAreaRules.Move(currentLayout, originSlotIndex, targetSlotIndex, FormationAreaRules.LookupFrom(rosterProvider), GetAreaPins(), WagonPlacement.Anywhere, ConnectivityRule.NoNewSplit));

        public bool HandleRemove(string unitId, int slotIndex)
            => Commit(FormationAreaRules.Remove(currentLayout, slotIndex, FormationAreaRules.LookupFrom(rosterProvider), GetAreaPins(), ConnectivityRule.NoNewSplit));

        private bool Commit(FormationEditResult result)
        {
            if (!result.Accepted) return false;
            currentLayout = result.Layout;
            return true;
        }

#if UNITY_EDITOR
        // 핀은 적용 버튼과 무관하게 저장소에 바로 반영된다(디버그 도구). 핀 제거로 영역 밖이 된 유닛은 로컬 사본에서 해제한다.
        public bool HasDebugPinStore => debugAreaSource != null;

        public void HandleDebugPinAdd(int slotIndex, FormationAreaShape shape) => debugAreaSource?.AddOrReplace(new FormationAreaPin(slotIndex, shape));

        public bool HandleDebugPinRemove(int slotIndex)
        {
            if (debugAreaSource == null) return false;

            var remaining = new List<FormationAreaPin>(debugAreaSource.Pins);
            remaining.RemoveAll(p => p.SlotIndex == slotIndex);
            var result = FormationAreaRules.Validate(currentLayout.Clone(), FormationAreaRules.LookupFrom(rosterProvider), remaining);
            if (!result.Accepted) return false;

            debugAreaSource.Remove(slotIndex);
            currentLayout = result.Layout;
            return true;
        }
#endif
    }
}
