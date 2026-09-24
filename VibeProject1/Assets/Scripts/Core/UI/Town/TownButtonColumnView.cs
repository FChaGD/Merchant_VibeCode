using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Hub 좌측 버튼 열(슬롯 4칸)의 배치만 담당한다. 루트 depth의 카테고리 열과 카테고리 depth의 시설 열이
    /// 같은 슬롯을 공유하므로 이 클래스를 함께 쓴다(Docs/설계/37번 §5.2~5.3).
    /// VerticalLayoutGroup을 쓰지 않는 이유: Hub 캔버스는 ConstantPixelSize라 레이아웃 그룹의 픽셀 기반
    /// 크기로는 다른 Hub 버튼(앵커 비율 기반)과 해상도별 크기가 어긋나고, "뒤로 가기는 마지막 슬롯 고정"
    /// 규칙도 표현되지 않는다. 그래서 기존 버튼과 같은 앵커 비율을 직접 계산한다.
    /// </summary>
    public class TownButtonColumnView : MonoBehaviour
    {
        // 좌측 열 x 범위와 버튼 크기는 기존 상단 배치/방향성 지시 버튼 크기. 최초 배치(슬롯 1 윗변 0.8002,
        // 간격 0.02)가 위로 치우쳐 보여 간격을 절반(0.01)으로, 열 전체를 버튼 1개 높이만큼 아래로
        // 내렸다(사용자 확정, 2026-09-24).
        private const int SlotCount = 4;
        private const float MinX = 0.105f;
        private const float MaxX = 0.241f;
        private const float FirstSlotTopY = 0.6848f;
        private const float SlotHeight = 0.1154f;
        private const float SlotGap = 0.01f;

        /// <summary>
        /// visibleInOrder를 슬롯 1부터 위에서 차례로 채우고, pinnedBottom이 있으면 마지막 슬롯에 고정한다.
        /// 이 열의 직계 자식 중 두 인자에 없는 것은 전부 끈다.
        /// </summary>
        public void Arrange(IReadOnlyList<GameObject> visibleInOrder, GameObject pinnedBottom = null)
        {
            var usableSlots = pinnedBottom == null ? SlotCount : SlotCount - 1;
            if (visibleInOrder.Count > usableSlots)
            {
                Debug.LogWarning($"{name}: 표시할 버튼 {visibleInOrder.Count}개가 슬롯 {usableSlots}칸을 넘는다. 넘친 버튼은 표시되지 않는다 - 슬롯 확장 여부를 결정해야 한다(Docs/설계/37번 §5.3).", this);
            }

            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
            }

            for (var i = 0; i < visibleInOrder.Count && i < usableSlots; i++)
            {
                PlaceInSlot(visibleInOrder[i], i);
            }

            if (pinnedBottom != null)
            {
                PlaceInSlot(pinnedBottom, SlotCount - 1);
            }
        }

        private static void PlaceInSlot(GameObject button, int slotIndex)
        {
            var top = FirstSlotTopY - slotIndex * (SlotHeight + SlotGap);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(MinX, top - SlotHeight);
            rect.anchorMax = new Vector2(MaxX, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            button.SetActive(true);
        }
    }
}
