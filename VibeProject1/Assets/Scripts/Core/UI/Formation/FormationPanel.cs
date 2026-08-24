using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using Game.Core.DebugTools;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 배치(Formation) UI를 조율한다. Hub뿐 아니라 배치 UI 화면 요소를 갖춘 콘텐츠 씬(Field 포함)이
    /// 로드될 때마다 RegisterFormationUI가 다시 호출되어 그 씬의 요소로 재바인딩된다 - 화면 요소 자체는
    /// 콘텐츠 씬에 속해 있어 씬이 바뀌면 파괴되기 때문이다. 팔레트/그리드/정보패널 사이의 드래그 앤 드롭
    /// 상호작용과 세션 중 배치 상태(FormationLayout)를 소유하며, 적용 버튼을 눌렀을 때만
    /// IFormationRepository에 반영한다. 적용 없이 닫으면 세션 상태를 그냥 버린다 — 다음에 열 때 항상
    /// repository에서 다시 불러오므로 별도의 되돌리기 로직 없이 "마지막 적용 상태로 복귀"가 성립한다.
    /// </summary>
    public class FormationPanel : MonoBehaviour, IFormationPanel
    {
        [SerializeField] private FormationUnitIconView dragGhostPrefab;

        public string PanelId => UIPanelIds.Formation;

        private GameObject panelRoot;
        private FormationPaletteView paletteView;
        private FormationGridView gridView;
        private FormationInfoPanelView infoPanelView;
#if UNITY_EDITOR
        // Core/Debug/Formation의 그리드 크기 디버그 패널 연동 지점 - 이 필드와 TryBind/Open의 짝, 그리고
        // HandleDebugApply/ResizeGrid/ResizeSlotSize 메서드까지가 디버그 전용 구간이다. Core/Debug/Formation
        // 폴더를 지울 때는 이 #if UNITY_EDITOR 블록들도 함께 지운다(DEBUG_FEATURES.md 참고).
        private FormationGridDebugView debugView;
#endif
        private Button applyButton;
        private Button closeButton;
        private Canvas rootCanvas;

        private ICaravanRosterProvider rosterProvider;
        private IFormationRepository repository;
        private IUIManager uiManager;

        private FormationLayout currentLayout;
        private readonly Dictionary<string, IFormationUnit> unitsById = new();

        private FormationUnitIconView dragGhost;
        private IFormationUnit draggedUnit;
        private int? draggedFromSlot;
        private bool dropHandled;

        public void RegisterFormationUI(ICaravanRosterProvider rosterProvider, IFormationRepository repository, IUIManager uiManager, string sceneName)
        {
            this.rosterProvider = rosterProvider;
            this.repository = repository;
            this.uiManager = uiManager;

            // 배치 UI 화면 요소는 콘텐츠 씬(Hub/Field 등) 안에 있어 그 씬이 언로드되면 함께 파괴된다.
            // 다른 콘텐츠 씬이 로드될 때마다 이 메서드가 다시 호출되어 그 씬의 사본으로 재바인딩한다
            // (panelRoot 등 이전 바인딩은 이미 파괴된 참조이므로 그냥 덮어써도 안전하다).
            var contentScene = SceneManager.GetSceneByName(sceneName);
            if (!contentScene.IsValid())
            {
                Debug.LogWarning($"'{sceneName}' 씬을 찾을 수 없어 Formation UI를 등록하지 못했다.");
                return;
            }

            SceneUIRoot sceneUIRoot = null;
            foreach (var rootObject in contentScene.GetRootGameObjects())
            {
                sceneUIRoot = rootObject.GetComponentInChildren<SceneUIRoot>(true);
                if (sceneUIRoot != null)
                {
                    break;
                }
            }

            if (sceneUIRoot == null)
            {
                Debug.LogWarning($"'{sceneName}' 씬에서 {nameof(SceneUIRoot)}를 찾을 수 없다.");
                return;
            }

            if (!TryBind(sceneUIRoot))
            {
                return;
            }

            rootCanvas = panelRoot.GetComponentInParent<Canvas>()?.rootCanvas;

            applyButton.onClick.RemoveAllListeners();
            applyButton.onClick.AddListener(HandleApply);

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => uiManager.Close(PanelId));

            panelRoot.SetActive(false);
        }

        private bool TryBind(SceneUIRoot sceneUIRoot)
        {
            if (!sceneUIRoot.TryGetElement<Transform>(FormationUIElementIds.PanelRoot, out var rootTransform))
            {
                WarnMissing(FormationUIElementIds.PanelRoot);
                return false;
            }
            panelRoot = rootTransform.gameObject;

            if (!sceneUIRoot.TryGetElement<FormationPaletteView>(FormationUIElementIds.PaletteRoot, out paletteView))
            {
                WarnMissing(FormationUIElementIds.PaletteRoot);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<FormationGridView>(FormationUIElementIds.GridRoot, out gridView))
            {
                WarnMissing(FormationUIElementIds.GridRoot);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<FormationInfoPanelView>(FormationUIElementIds.InfoPanelRoot, out infoPanelView))
            {
                WarnMissing(FormationUIElementIds.InfoPanelRoot);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<Button>(FormationUIElementIds.ApplyButton, out applyButton))
            {
                WarnMissing(FormationUIElementIds.ApplyButton);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<Button>(FormationUIElementIds.CloseButton, out closeButton))
            {
                WarnMissing(FormationUIElementIds.CloseButton);
                return false;
            }

#if UNITY_EDITOR
            // 디버그 패널은 보조 기능이라 없어도 나머지 배치 UI는 정상 동작해야 한다 - 없으면 조용히 건너뛴다.
            sceneUIRoot.TryGetElement<FormationGridDebugView>(FormationUIElementIds.DebugPanelRoot, out debugView);
#endif

            return true;
        }

        private static void WarnMissing(string id)
        {
            Debug.LogWarning($"Formation UI에서 '{id}' 요소를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라.");
        }

        public void Open()
        {
            if (panelRoot == null)
            {
                return;
            }

            var roster = rosterProvider?.GetRoster() ?? Array.Empty<IFormationUnit>();
            unitsById.Clear();
            foreach (var unit in roster)
            {
                unitsById[unit.Id] = unit;
            }

            currentLayout = BuildInitialLayout();

            paletteView.SetRoster(roster, HandleUnitIconClicked, HandlePaletteIconBeginDrag, HandleIconDrag, HandleIconEndDrag);
            gridView.Initialize(HandleSlotDropped, HandleUnitIconClicked, HandleGridIconBeginDrag, HandleIconDrag, HandleIconEndDrag);

            RefreshAllSlots();
            infoPanelView.Clear();
#if UNITY_EDITOR
            debugView?.Initialize(gridView.ColumnCount, gridView.RowCount, gridView.SlotSize, HandleDebugApply);
#endif

            panelRoot.SetActive(true);
        }

        // 순수 "숨기기"만 한다. 상행 준비 UI 등으로 되돌아가는 네비게이션은 UIManager.Close(PanelId)의 책임이므로
        // 버튼 등 외부에서 패널을 닫을 때는 이 메서드를 직접 호출하지 말고 반드시 uiManager.Close(PanelId)를 거칠 것.
        public void Close()
        {
            if (panelRoot == null)
            {
                return;
            }

            panelRoot.SetActive(false);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 열(X)/행(Y) 수를 런타임에 조절한다(디버깅 UI 연동 지점, HandleDebugApply의 유일한 호출부).
        /// 기존 배치는 가능한 범위까지 유지한다.
        /// </summary>
        private void ResizeGrid(int columns, int rows)
        {
            if (gridView == null || currentLayout == null)
            {
                return;
            }

            gridView.SetGridDimensions(columns, rows);

            var resized = new FormationLayout(Mathf.Max(0, columns), Mathf.Max(0, rows));
            var copyCount = Mathf.Min(resized.SlotCount, currentLayout.SlotCount);
            for (var i = 0; i < copyCount; i++)
            {
                resized.SetUnitId(i, currentLayout.GetUnitId(i));
            }

            currentLayout = resized;
            RefreshAllSlots();
        }

        /// <summary>
        /// 타일 1칸의 가로/세로 크기를 런타임에 조절한다(디버깅 UI 연동 지점, HandleDebugApply의 유일한 호출부).
        /// </summary>
        private void ResizeSlotSize(Vector2 size)
        {
            gridView?.SetSlotSize(size);
        }

        private void HandleDebugApply(int columns, int rows, Vector2 size)
        {
            // 크기를 먼저 반영해야 열/행 변경으로 새로 생성되는 타일도 같은 크기로 만들어진다.
            ResizeSlotSize(size);
            ResizeGrid(columns, rows);
        }
#endif

        private FormationLayout BuildInitialLayout()
        {
            if (repository != null && repository.TryLoadCurrent(out var saved))
            {
                // 저장된 배치의 그리드 모양(열/행 수)을 이 씬의 그리드 모양 기준으로 삼는다 - 콘텐츠
                // 씬마다 별도 화면 요소를 갖고 있어(클래스 요약 주석 참고) 그리드 모양이 씬마다 따로
                // 어긋날 수 있었다. 저장된 데이터가 기준이 되면 어느 씬에서 열어도 같은 모양으로 보인다.
                gridView.SetGridDimensions(saved.ColumnCount, saved.RowCount);
                return saved.Clone();
            }

            return new FormationLayout(gridView.ColumnCount, gridView.RowCount);
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

        private void HandleUnitIconClicked(IFormationUnit unit) => infoPanelView.Show(unit);

        private void HandlePaletteIconBeginDrag(IFormationUnit unit, FormationUnitIconView icon, PointerEventData eventData)
        {
            BeginDrag(unit, null, eventData);
        }

        private void HandleGridIconBeginDrag(int originSlotIndex, FormationUnitIconView icon, PointerEventData eventData)
        {
            var unitId = currentLayout.GetUnitId(originSlotIndex);
            if (string.IsNullOrEmpty(unitId) || !unitsById.TryGetValue(unitId, out var unit))
            {
                return;
            }

            BeginDrag(unit, originSlotIndex, eventData);
        }

        private void BeginDrag(IFormationUnit unit, int? originSlotIndex, PointerEventData eventData)
        {
            draggedUnit = unit;
            draggedFromSlot = originSlotIndex;
            dropHandled = false;

            if (dragGhost == null && dragGhostPrefab != null && rootCanvas != null)
            {
                dragGhost = Instantiate(dragGhostPrefab, rootCanvas.transform);
                dragGhost.SetRaycastTarget(false);
            }

            if (dragGhost == null)
            {
                return;
            }

            dragGhost.Bind(unit);
            dragGhost.gameObject.SetActive(true);
            dragGhost.transform.SetAsLastSibling();
            UpdateGhostPosition(eventData);
        }

        private void HandleIconDrag(PointerEventData eventData)
        {
            UpdateGhostPosition(eventData);
        }

        private void UpdateGhostPosition(PointerEventData eventData)
        {
            if (dragGhost != null)
            {
                dragGhost.transform.position = eventData.position;
            }
        }

        private void HandleIconEndDrag(PointerEventData eventData)
        {
            if (dragGhost != null)
            {
                dragGhost.gameObject.SetActive(false);
            }

            if (draggedFromSlot.HasValue)
            {
                if (!dropHandled)
                {
                    // 타일/팔레트가 아닌 곳에 드롭 = 배치 취소(슬롯 비움).
                    currentLayout.Clear(draggedFromSlot.Value);
                }

                // 원본 슬롯의 아이콘 파괴/갱신은 반드시 여기(드래그가 실제로 끝나는 시점)에서 한다.
                // OnDrop 시점(HandleSlotDropped)에는 이 아이콘이 아직 드래그 중인 오브젝트라, 거기서
                // 파괴하면 뒤이은 OnEndDrag 호출이 씹혀 드래그 상태가 초기화되지 않는 문제가 있었다
                // (고스트가 안 사라지고, 다음 드래그에 이전 유닛/슬롯 정보가 남아 엉뚱하게 재배치됨).
                RefreshSlot(draggedFromSlot.Value);
            }

            draggedUnit = null;
            draggedFromSlot = null;
        }

        private void HandleSlotDropped(int targetSlotIndex)
        {
            if (draggedUnit == null)
            {
                return;
            }

            dropHandled = true;

            if (draggedFromSlot.HasValue)
            {
                var sourceIndex = draggedFromSlot.Value;
                if (sourceIndex == targetSlotIndex)
                {
                    return;
                }

                var targetUnitId = currentLayout.GetUnitId(targetSlotIndex);
                if (string.IsNullOrEmpty(targetUnitId))
                {
                    currentLayout.SetUnitId(targetSlotIndex, draggedUnit.Id);
                    currentLayout.Clear(sourceIndex);
                }
                else
                {
                    currentLayout.Swap(sourceIndex, targetSlotIndex);
                }

                // sourceIndex는 아직 드래그 중인 아이콘이 점유하고 있으므로 여기서 파괴하지 않는다.
                // 실제 갱신은 드래그가 끝나는 HandleIconEndDrag에서 처리한다.
                RefreshSlot(targetSlotIndex);
            }
            else
            {
                // 팔레트에서 시작한 배치 - 기존 점유 유닛은 슬롯 표시에서만 해제된다(상행 관리 데이터 삭제 아님).
                currentLayout.SetUnitId(targetSlotIndex, draggedUnit.Id);
                RefreshSlot(targetSlotIndex);
            }
        }

        private void RefreshSlot(int index)
        {
            var unitId = currentLayout.GetUnitId(index);
            IFormationUnit unit = null;
            if (!string.IsNullOrEmpty(unitId))
            {
                unitsById.TryGetValue(unitId, out unit);
            }

            gridView.RenderSlot(index, unit);
        }

        private void RefreshAllSlots()
        {
            for (var i = 0; i < currentLayout.SlotCount; i++)
            {
                RefreshSlot(i);
            }
        }
    }
}
