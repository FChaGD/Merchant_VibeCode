#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 디버그 패널의 핀 아이콘. 끌어서 정비창 격자 칸에 놓으면 그 칸을 알린다(Docs/기획/59번 §4.5). 놓인 칸은 포인터 아래
    /// FormationSlotView로 판정한다 - 칸의 드롭 이벤트는 유닛 드래그(FormationGridEditor) 전용이라 핀 드롭을 섞지 않는다.
    /// </summary>
    public class FormationDebugPinHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private Action<int> onDroppedOnSlot;
        private RectTransform ghost;

        public void Initialize(Action<int> droppedOnSlot) => onDroppedOnSlot = droppedOnSlot;

        public void OnBeginDrag(PointerEventData eventData)
        {
            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) return;

            if (ghost == null)
            {
                var go = new GameObject("PinDragGhost", typeof(RectTransform), typeof(Image));
                ghost = (RectTransform)go.transform;
                ghost.SetParent(canvas.transform, false);
                ghost.sizeDelta = ((RectTransform)transform).rect.size;
                var image = go.GetComponent<Image>();
                image.color = GetComponent<Image>() != null ? GetComponent<Image>().color : Color.magenta;
                image.raycastTarget = false; // 포인터 아래 칸을 가리지 않게 한다
            }

            ghost.gameObject.SetActive(true);
            ghost.SetAsLastSibling();
            ghost.position = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (ghost != null) ghost.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (ghost != null) ghost.gameObject.SetActive(false);

            var target = eventData.pointerCurrentRaycast.gameObject;
            var slot = target != null ? target.GetComponentInParent<FormationSlotView>() : null;
            if (slot != null) onDroppedOnSlot?.Invoke(slot.SlotIndex);
        }
    }
}
#endif
