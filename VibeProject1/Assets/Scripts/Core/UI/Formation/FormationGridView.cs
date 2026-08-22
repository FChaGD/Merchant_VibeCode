using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 배치 UI의 그리드 타일 영역. 슬롯 개수는 인스펙터 또는 SetSlotCount로 조절 가능하며,
    /// 화면 밖 슬롯은 ScrollRect와 좌우 버튼으로 탐색한다. 실제 배치 데이터(FormationLayout)는
    /// FormationPanel이 소유하며, 이 클래스는 렌더링과 드래그/드롭 이벤트 중계만 담당한다.
    /// </summary>
    public class FormationGridView : MonoBehaviour
    {
        [SerializeField] private Transform slotContent;
        [SerializeField] private FormationSlotView slotPrefab;
        [SerializeField] private FormationUnitIconView occupantIconPrefab;
        [SerializeField, Min(0)] private int slotCount = 8;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Button scrollLeftButton;
        [SerializeField] private Button scrollRightButton;
        [SerializeField] private float scrollStep = 0.2f;

        private readonly List<FormationSlotView> slots = new();

        private Action<int> onSlotDropped;
        private Action<IFormationUnit> onIconClicked;
        private Action<int, FormationUnitIconView, PointerEventData> onIconBeginDrag;
        private Action<PointerEventData> onIconDrag;
        private Action<PointerEventData> onIconEndDrag;

        public int SlotCount => slotCount;

        public void Initialize(
            Action<int> slotDropped,
            Action<IFormationUnit> iconClicked,
            Action<int, FormationUnitIconView, PointerEventData> iconBeginDrag,
            Action<PointerEventData> iconDrag,
            Action<PointerEventData> iconEndDrag)
        {
            onSlotDropped = slotDropped;
            onIconClicked = iconClicked;
            onIconBeginDrag = iconBeginDrag;
            onIconDrag = iconDrag;
            onIconEndDrag = iconEndDrag;

            RebuildSlots();

            if (scrollLeftButton != null)
            {
                scrollLeftButton.onClick.RemoveAllListeners();
                scrollLeftButton.onClick.AddListener(() => Scroll(-scrollStep));
            }

            if (scrollRightButton != null)
            {
                scrollRightButton.onClick.RemoveAllListeners();
                scrollRightButton.onClick.AddListener(() => Scroll(scrollStep));
            }
        }

        /// <summary>
        /// 슬롯 개수를 런타임에 조절한다(디버깅 UI 연동 지점). 배치 데이터 재정렬은 호출자(FormationPanel)의 책임이다.
        /// </summary>
        public void SetSlotCount(int count)
        {
            slotCount = Mathf.Max(0, count);
            RebuildSlots();
        }

        public void RenderSlot(int index, IFormationUnit unit)
        {
            if (index < 0 || index >= slots.Count)
            {
                return;
            }

            var slot = slots[index];

            if (slot.CurrentIcon != null)
            {
                Destroy(slot.CurrentIcon.gameObject);
                slot.SetIcon(null);
            }

            if (unit == null)
            {
                return;
            }

            if (occupantIconPrefab == null)
            {
                Debug.LogWarning($"{nameof(FormationGridView)}에 {nameof(occupantIconPrefab)}가 지정되어 있지 않다.");
                return;
            }

            var icon = Instantiate(occupantIconPrefab, slot.IconContainer);
            icon.Bind(unit);

            var slotIndex = slot.SlotIndex;
            icon.SetHandlers(
                _ => onIconClicked?.Invoke(unit),
                (iconView, eventData) => onIconBeginDrag?.Invoke(slotIndex, iconView, eventData),
                eventData => onIconDrag?.Invoke(eventData),
                eventData => onIconEndDrag?.Invoke(eventData));

            slot.SetIcon(icon);
        }

        private void RebuildSlots()
        {
            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }
            slots.Clear();

            if (slotPrefab == null || slotContent == null)
            {
                Debug.LogWarning($"{nameof(FormationGridView)}에 {nameof(slotPrefab)} 또는 {nameof(slotContent)}가 지정되어 있지 않다.");
                return;
            }

            for (var i = 0; i < slotCount; i++)
            {
                var slot = Instantiate(slotPrefab, slotContent);
                slot.Initialize(i, onSlotDropped);
                slots.Add(slot);
            }
        }

        private void Scroll(float delta)
        {
            if (scrollRect == null)
            {
                return;
            }

            scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(scrollRect.horizontalNormalizedPosition + delta);
        }
    }
}
