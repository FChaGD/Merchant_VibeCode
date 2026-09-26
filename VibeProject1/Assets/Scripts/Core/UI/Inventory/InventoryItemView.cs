using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 아이템 1개(점유 칸 크기의 사각형 + 이름). 그리드·임시 보관 목록·드래그 고스트가 공용으로 쓴다.
    /// 클릭·드래그 이벤트는 직접 처리하지 않고 SetHandlers로 주입된 델리게이트에 그대로 위임한다 -
    /// 판정(이동/교환/회전)은 InventoryItemDragController/InventoryPopupPanel이 담당한다(FormationUnitIconView와 같은 방식).
    /// </summary>
    public class InventoryItemView : MonoBehaviour,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float DimmedAlpha = 0.35f;

        [SerializeField] private Image background;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Outline selectionOutline;

        private Color baseColor = Color.white;
        private Action<InventoryItemView> onClicked;
        private Action<InventoryItemView, PointerEventData> onBeginDrag;
        private Action<PointerEventData> onDrag;
        private Action<PointerEventData> onEndDrag;

        public InventoryItemInstance Item { get; private set; }
        public RectTransform RectTransform => (RectTransform)transform;

        public void Bind(InventoryItemInstance item, Color color)
        {
            Item = item;
            baseColor = color;
            if (background != null) background.color = color;
            if (label != null) label.text = item.Definition?.DisplayName ?? "값 없음";
            SetSelected(false);
        }

        public void SetHandlers(
            Action<InventoryItemView> clickHandler,
            Action<InventoryItemView, PointerEventData> beginDragHandler,
            Action<PointerEventData> dragHandler,
            Action<PointerEventData> endDragHandler)
        {
            onClicked = clickHandler;
            onBeginDrag = beginDragHandler;
            onDrag = dragHandler;
            onEndDrag = endDragHandler;
        }

        public void SetSelected(bool selected)
        {
            if (selectionOutline != null) selectionOutline.enabled = selected;
        }

        // 드래그 중 원래 자리의 아이템을 반투명하게 남겨 "어디서 집었는지"를 보여준다.
        public void SetDimmed(bool dimmed)
        {
            if (background == null) return;
            var color = baseColor;
            if (dimmed) color.a *= DimmedAlpha;
            background.color = color;
        }

        public void SetRaycastTarget(bool raycastTarget)
        {
            if (background != null) background.raycastTarget = raycastTarget;
        }

        /// <summary>
        /// 포인터가 가리키는 아이템 내부 칸(현재 회전 기준, x = 오른쪽, y = 아래)을 구한다 - 드래그 시작 시
        /// "잡은 칸"을 정하는 데 쓴다(Docs/기획/39번 §3.4-1). 크기가 제각각인 임시 보관 목록에서도 같게
        /// 동작하도록 칸 크기를 뷰 자신의 크기 ÷ 점유 칸 수로 계산한다.
        /// </summary>
        public GridPosition GetLocalCell(PointerEventData eventData)
        {
            var rect = RectTransform.rect;
            if (Item.Definition == null
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, eventData.pressPosition, eventData.pressEventCamera, out var local))
            {
                return new GridPosition(0, 0);
            }

            var cellWidth = rect.width / Item.Width;
            var cellHeight = rect.height / Item.Height;
            var x = Mathf.Clamp(Mathf.FloorToInt((local.x - rect.xMin) / cellWidth), 0, Item.Width - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt((rect.yMax - local.y) / cellHeight), 0, Item.Height - 1);
            return new GridPosition(x, y);
        }

        public void OnPointerClick(PointerEventData eventData) => onClicked?.Invoke(this);

        public void OnBeginDrag(PointerEventData eventData) => onBeginDrag?.Invoke(this, eventData);

        public void OnDrag(PointerEventData eventData) => onDrag?.Invoke(eventData);

        public void OnEndDrag(PointerEventData eventData) => onEndDrag?.Invoke(eventData);
    }
}
