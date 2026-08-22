using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 배치 UI에서 팔레트 아이콘/그리드 점유 아이콘/드래그 고스트로 공용 사용되는 유닛 아이콘.
    /// 클릭·드래그 이벤트는 직접 처리하지 않고 SetHandlers로 주입된 델리게이트에 그대로 위임한다 —
    /// 실제 배치 규칙 판단(교체/스왑/취소)은 FormationPanel이 담당한다.
    /// </summary>
    public class FormationUnitIconView : MonoBehaviour,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image iconImage;

        public IFormationUnit Unit { get; private set; }

        private Action<FormationUnitIconView> onClicked;
        private Action<FormationUnitIconView, PointerEventData> onBeginDrag;
        private Action<PointerEventData> onDrag;
        private Action<PointerEventData> onEndDrag;

        public void Bind(IFormationUnit unit)
        {
            Unit = unit;
            if (iconImage != null)
            {
                iconImage.sprite = unit?.Icon;
            }
        }

        public void SetHandlers(
            Action<FormationUnitIconView> clickHandler,
            Action<FormationUnitIconView, PointerEventData> beginDragHandler,
            Action<PointerEventData> dragHandler,
            Action<PointerEventData> endDragHandler)
        {
            onClicked = clickHandler;
            onBeginDrag = beginDragHandler;
            onDrag = dragHandler;
            onEndDrag = endDragHandler;
        }

        public void SetRaycastTarget(bool enabled)
        {
            if (iconImage != null)
            {
                iconImage.raycastTarget = enabled;
            }
        }

        public void OnPointerClick(PointerEventData eventData) => onClicked?.Invoke(this);

        public void OnBeginDrag(PointerEventData eventData) => onBeginDrag?.Invoke(this, eventData);

        public void OnDrag(PointerEventData eventData) => onDrag?.Invoke(eventData);

        public void OnEndDrag(PointerEventData eventData) => onEndDrag?.Invoke(eventData);
    }
}
