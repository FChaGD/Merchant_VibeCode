using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Hub 씬의 배치(Formation) UI를 조율한다. 팔레트/그리드/정보패널 사이의 드래그 앤 드롭 상호작용과
    /// 세션 중 배치 상태(FormationLayout)를 소유하며, 저장 버튼을 눌렀을 때만 IFormationRepository에 반영한다.
    /// 저장 없이 닫으면 세션 상태를 그냥 버린다 — 다음에 열 때 항상 repository에서 다시 불러오므로
    /// 별도의 되돌리기 로직 없이 "마지막 저장 상태로 복귀"가 성립한다.
    /// </summary>
    public class FormationPanel : MonoBehaviour, IFormationPanel
    {
        [SerializeField] private FormationUnitIconView dragGhostPrefab;

        public string PanelId => UIPanelIds.Formation;

        private GameObject panelRoot;
        private FormationPaletteView paletteView;
        private FormationGridView gridView;
        private FormationInfoPanelView infoPanelView;
        private Button saveButton;
        private Button closeButton;
        private Canvas rootCanvas;

        private ICaravanRosterProvider rosterProvider;
        private IFormationRepository repository;

        private FormationLayout currentLayout;
        private readonly Dictionary<string, IFormationUnit> unitsById = new();

        private FormationUnitIconView dragGhost;
        private IFormationUnit draggedUnit;
        private int? draggedFromSlot;
        private bool dropHandled;

        public void RegisterFormationUI(ICaravanRosterProvider rosterProvider, IFormationRepository repository)
        {
            this.rosterProvider = rosterProvider;
            this.repository = repository;

            var hubScene = SceneManager.GetSceneByName(SceneNames.Hub);
            if (!hubScene.IsValid())
            {
                Debug.LogWarning($"'{SceneNames.Hub}' 씬을 찾을 수 없어 Formation UI를 등록하지 못했다.");
                return;
            }

            SceneUIRoot sceneUIRoot = null;
            foreach (var rootObject in hubScene.GetRootGameObjects())
            {
                sceneUIRoot = rootObject.GetComponentInChildren<SceneUIRoot>(true);
                if (sceneUIRoot != null)
                {
                    break;
                }
            }

            if (sceneUIRoot == null)
            {
                Debug.LogWarning($"'{SceneNames.Hub}' 씬에서 {nameof(SceneUIRoot)}를 찾을 수 없다.");
                return;
            }

            if (!TryBind(sceneUIRoot))
            {
                return;
            }

            rootCanvas = panelRoot.GetComponentInParent<Canvas>()?.rootCanvas;

            saveButton.onClick.RemoveAllListeners();
            saveButton.onClick.AddListener(HandleSave);

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);

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

            if (!sceneUIRoot.TryGetElement<Button>(FormationUIElementIds.SaveButton, out saveButton))
            {
                WarnMissing(FormationUIElementIds.SaveButton);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<Button>(FormationUIElementIds.CloseButton, out closeButton))
            {
                WarnMissing(FormationUIElementIds.CloseButton);
                return false;
            }

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

            paletteView.SetRoster(roster, HandlePaletteIconClicked, HandlePaletteIconBeginDrag, HandleIconDrag, HandleIconEndDrag);
            gridView.Initialize(HandleSlotDropped, HandleGridIconClicked, HandleGridIconBeginDrag, HandleIconDrag, HandleIconEndDrag);

            RefreshAllSlots();
            infoPanelView.Clear();

            panelRoot.SetActive(true);
        }

        public void Close()
        {
            if (panelRoot == null)
            {
                return;
            }

            panelRoot.SetActive(false);
        }

        /// <summary>
        /// 슬롯 개수를 런타임에 조절한다(디버깅 UI 연동 지점). 기존 배치는 가능한 범위까지 유지한다.
        /// </summary>
        public void ResizeSlots(int slotCount)
        {
            if (gridView == null || currentLayout == null)
            {
                return;
            }

            gridView.SetSlotCount(slotCount);

            var resized = new FormationLayout(slotCount);
            var copyCount = Mathf.Min(slotCount, currentLayout.SlotCount);
            for (var i = 0; i < copyCount; i++)
            {
                resized.SetUnitId(i, currentLayout.GetUnitId(i));
            }

            currentLayout = resized;
            RefreshAllSlots();
        }

        private FormationLayout BuildInitialLayout()
        {
            var slotCount = gridView.SlotCount;

            if (repository != null && repository.TryLoadCurrent(out var saved))
            {
                var adjusted = new FormationLayout(slotCount);
                var copyCount = Mathf.Min(slotCount, saved.SlotCount);
                for (var i = 0; i < copyCount; i++)
                {
                    adjusted.SetUnitId(i, saved.GetUnitId(i));
                }
                return adjusted;
            }

            return new FormationLayout(slotCount);
        }

        private void HandleSave()
        {
            if (repository == null)
            {
                Debug.LogWarning($"{nameof(IFormationRepository)}가 연결되어 있지 않아 배치를 저장하지 못했다.");
                return;
            }

            repository.Save(currentLayout.Clone());
        }

        private void HandlePaletteIconClicked(IFormationUnit unit) => infoPanelView.Show(unit);

        private void HandleGridIconClicked(IFormationUnit unit) => infoPanelView.Show(unit);

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

            if (!dropHandled && draggedFromSlot.HasValue)
            {
                // 타일/팔레트가 아닌 곳에 드롭 = 배치 취소(슬롯 비움).
                currentLayout.Clear(draggedFromSlot.Value);
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

                RefreshSlot(sourceIndex);
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
