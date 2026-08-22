using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 그리드 타일 1칸. 점유 유닛 아이콘을 담는 컨테이너이자 드롭 대상이다.
    /// 실제 배치 규칙 판단은 FormationPanel이 담당하며, 이 클래스는 드롭 이벤트를 그대로 중계한다.
    /// </summary>
    public class FormationSlotView : MonoBehaviour, IDropHandler
    {
        [SerializeField] private Transform iconContainer;

        private Action<int> onDropped;

        public int SlotIndex { get; private set; }
        public FormationUnitIconView CurrentIcon { get; private set; }
        public Transform IconContainer => iconContainer != null ? iconContainer : transform;

        public void Initialize(int slotIndex, Action<int> dropped)
        {
            SlotIndex = slotIndex;
            onDropped = dropped;
        }

        public void SetIcon(FormationUnitIconView icon)
        {
            CurrentIcon = icon;
        }

        public void OnDrop(PointerEventData eventData)
        {
            onDropped?.Invoke(SlotIndex);
        }
    }
}
