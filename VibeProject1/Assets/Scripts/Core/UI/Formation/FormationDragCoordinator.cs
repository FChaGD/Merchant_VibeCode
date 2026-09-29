using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 배치 UI의 드래그 상태 추적과 드래그 고스트 표시만 전담한다(TripMapInteractionCoordinator와
    /// 같은 이유로 FormationPanel에서 분리 - SRP). 드롭 시 실제로 무엇을 반영할지(교체/스왑/취소
    /// 판단)는 더 이상 여기서 하지 않는다 - Hub/Field 배치 UI가 그 판단을 각자 다르게 내려야 해서
    /// (Docs/설계/25번 §2.2) 호출자(FormationGridEditor)에게 위임했다. 이 클래스는 "지금 무엇을
    /// 드래그 중인지"와 고스트 아이콘 표시만 안다.
    /// </summary>
    internal class FormationDragCoordinator
    {
        private FormationUnitIconView dragGhostPrefab;
        private Canvas rootCanvas;
        // 격자 아이콘의 현재 월드 크기 - 확대/축소로 칸이 바뀌어도 고스트가 놓일 아이콘과 같은 크기로 보이게 한다.
        private Func<float> ghostWorldSizeProvider;

        private FormationUnitIconView dragGhost;

        public IFormationUnit DraggedUnit { get; private set; }
        public int? DraggedFromSlot { get; private set; }
        public bool DropHandled { get; private set; }
        // 이동 중인 활동의 도착 고스트를 드래그하는 세 번째 드래그 종류(기획 21번, 설계 26번 §5.4) -
        // DraggedFromSlot==null인 팔레트 드래그와 구분하기 위한 별도 플래그.
        public bool IsRedirect { get; private set; }

        public void Rebind(FormationUnitIconView dragGhostPrefab, Canvas rootCanvas, Func<float> ghostWorldSizeProvider)
        {
            this.dragGhostPrefab = dragGhostPrefab;
            this.rootCanvas = rootCanvas;
            this.ghostWorldSizeProvider = ghostWorldSizeProvider;
        }

        public void CancelActiveDrag()
        {
            if (dragGhost != null)
            {
                dragGhost.gameObject.SetActive(false);
            }

            DraggedUnit = null;
            DraggedFromSlot = null;
            DropHandled = false;
            IsRedirect = false;
        }

        public void BeginFromPalette(IFormationUnit unit, PointerEventData eventData) => BeginDrag(unit, null, eventData, isRedirect: false);

        // 슬롯 인덱스가 가리키는 유닛이 무엇인지는 더 이상 이 클래스가 조회하지 않는다 - 호출자가
        // 이미 해석한 IFormationUnit을 그대로 넘긴다(레이아웃 조회 책임을 호출자에게 넘김).
        public void BeginFromGrid(IFormationUnit unit, int originSlotIndex, PointerEventData eventData) => BeginDrag(unit, originSlotIndex, eventData, isRedirect: false);

        // 도착 고스트 드래그 시작(기획 21번) - 픽업할 "슬롯"이 없으므로 DraggedFromSlot은 null로
        // 두고 IsRedirect로 팔레트 드래그와 구분한다.
        public void BeginRedirect(IFormationUnit unit, PointerEventData eventData) => BeginDrag(unit, null, eventData, isRedirect: true);

        // 이동 중인 유닛의 "출발 슬롯" 아이콘을 집어서 시작하는 재조정 드래그(기획 21번, 2026-09-07
        // 예외처리 확정) - 도착 고스트 드래그와 달리 실제로 픽업한 그리드 슬롯이 있으므로
        // DraggedFromSlot을 채워 아이콘 숨김/복원 등 기존 그리드 드래그 UX를 그대로 재사용하면서도
        // IsRedirect=true로 표시해 드롭/취소 처리는 재조정 규칙(HandleRedirectMove, 빈 곳 드롭=유지)을
        // 따르게 한다.
        public void BeginRedirectFromGrid(IFormationUnit unit, int originSlotIndex, PointerEventData eventData) => BeginDrag(unit, originSlotIndex, eventData, isRedirect: true);

        private void BeginDrag(IFormationUnit unit, int? originSlotIndex, PointerEventData eventData, bool isRedirect)
        {
            DraggedUnit = unit;
            DraggedFromSlot = originSlotIndex;
            DropHandled = false;
            IsRedirect = isRedirect;

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
            ApplyGhostSize();
            dragGhost.gameObject.SetActive(true);
            dragGhost.transform.SetAsLastSibling();
            UpdateGhostPosition(eventData);
        }

        // 드래그 시작 시점의 격자 배율로 한 번만 맞춘다 - 드래그 중에는 휠 입력이 격자로 가지 않아 배율이 바뀌지 않는다.
        private void ApplyGhostSize()
        {
            var worldSize = ghostWorldSizeProvider?.Invoke() ?? 0f;
            var canvasScale = rootCanvas != null ? rootCanvas.transform.lossyScale.x : 1f;
            if (worldSize <= 0f || canvasScale <= 0f) return;

            var ghostRect = (RectTransform)dragGhost.transform;
            var size = worldSize / canvasScale;
            ghostRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
            ghostRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);
        }

        public void UpdateGhostPosition(PointerEventData eventData)
        {
            if (dragGhost != null)
            {
                dragGhost.transform.position = eventData.position;
            }
        }

        // 드롭이 실제로 처리됐음을 기록한다 - EndDrag가 "취소(빈 곳에 드롭)"인지 구분하는 데 쓴다.
        public void MarkDropHandled() => DropHandled = true;

        // 드래그가 끝날 때(취소/이동/팔레트 배치 전부 포함) 고스트를 숨기고 상태를 리셋한다. 리셋
        // 직전 상태를 스냅샷으로 반환하므로, 호출자는 이 메서드 호출 전에 값을 따로 읽어둘 필요가 없다.
        public (IFormationUnit unit, int? fromSlot, bool wasHandled) EndDrag()
        {
            if (dragGhost != null)
            {
                dragGhost.gameObject.SetActive(false);
            }

            var snapshot = (DraggedUnit, DraggedFromSlot, DropHandled);
            DraggedUnit = null;
            DraggedFromSlot = null;
            DropHandled = false;
            IsRedirect = false;
            return snapshot;
        }
    }
}
