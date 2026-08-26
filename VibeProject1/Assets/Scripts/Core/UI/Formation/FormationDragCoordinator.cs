using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 배치 UI의 드래그 앤 드롭 배치 판단(교체/스왑/취소)과 드래그 고스트 표시를 전담한다
    /// (TripMapInteractionCoordinator와 같은 이유로 FormationPanel에서 분리 - SRP,
    /// Docs/Refactor 참고). 배치 데이터(FormationLayout)와 유닛 조회(unitsById)는 FormationPanel이
    /// 계속 소유하며, 매 호출마다 인자로 받는다 - 디버그 리사이즈로 currentLayout이 통째로 교체돼도
    /// 항상 최신 값을 참조하기 위해 내부에 캐시하지 않는다.
    /// </summary>
    internal class FormationDragCoordinator
    {
        private FormationUnitIconView dragGhostPrefab;
        private Canvas rootCanvas;

        private FormationUnitIconView dragGhost;
        private IFormationUnit draggedUnit;
        private int? draggedFromSlot;
        private bool dropHandled;

        public void Rebind(FormationUnitIconView dragGhostPrefab, Canvas rootCanvas)
        {
            this.dragGhostPrefab = dragGhostPrefab;
            this.rootCanvas = rootCanvas;
        }

        public void CancelActiveDrag()
        {
            if (dragGhost != null)
            {
                dragGhost.gameObject.SetActive(false);
            }

            draggedUnit = null;
            draggedFromSlot = null;
            dropHandled = false;
        }

        public void BeginFromPalette(IFormationUnit unit, PointerEventData eventData)
        {
            BeginDrag(unit, null, eventData);
        }

        public void BeginFromGrid(int originSlotIndex, FormationLayout layout, IReadOnlyDictionary<string, IFormationUnit> unitsById, PointerEventData eventData)
        {
            var unitId = layout.GetUnitId(originSlotIndex);
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
                dragGhost = UnityEngine.Object.Instantiate(dragGhostPrefab, rootCanvas.transform);
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

        public void UpdateGhostPosition(PointerEventData eventData)
        {
            if (dragGhost != null)
            {
                dragGhost.transform.position = eventData.position;
            }
        }

        public void HandleSlotDropped(int targetSlotIndex, FormationLayout layout, Action<int> refreshSlot)
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

                var targetUnitId = layout.GetUnitId(targetSlotIndex);
                if (string.IsNullOrEmpty(targetUnitId))
                {
                    layout.SetUnitId(targetSlotIndex, draggedUnit.Id);
                    layout.Clear(sourceIndex);
                }
                else
                {
                    layout.Swap(sourceIndex, targetSlotIndex);
                }

                // sourceIndex는 아직 드래그 중인 아이콘이 점유하고 있으므로 여기서 갱신하지 않는다.
                // 실제 갱신은 드래그가 끝나는 HandleIconEndDrag에서 처리한다.
                refreshSlot(targetSlotIndex);
            }
            else
            {
                // 팔레트에서 시작한 배치 - 기존 점유 유닛은 슬롯 표시에서만 해제된다(상행 관리 데이터 삭제 아님).
                layout.SetUnitId(targetSlotIndex, draggedUnit.Id);
                refreshSlot(targetSlotIndex);
            }
        }

        public void HandleIconEndDrag(FormationLayout layout, Action<int> refreshSlot)
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
                    layout.Clear(draggedFromSlot.Value);
                }

                // 원본 슬롯의 아이콘 파괴/갱신은 반드시 여기(드래그가 실제로 끝나는 시점)에서 한다.
                // OnDrop 시점(HandleSlotDropped)에는 이 아이콘이 아직 드래그 중인 오브젝트라, 거기서
                // 갱신하면 뒤이은 OnEndDrag 호출이 씹혀 드래그 상태가 초기화되지 않는 문제가 있었다.
                refreshSlot(draggedFromSlot.Value);
            }

            draggedUnit = null;
            draggedFromSlot = null;
        }
    }
}
