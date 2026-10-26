using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 정리 모드에서 붉게 표시할 칸 - 가장 큰 덩어리 외의 연결 칸(설계 79번 §8, 기획 78번 §4-18). 어느 쪽 마차를 옮겨야 하는지 보이게 하려는
    /// 표시라 연결돼 있으면(덩어리 1개 이하) 아무 칸도 고르지 않는다. 화면과 분리된 순수 계산으로 둬 GameObject 없이 테스트한다.
    /// </summary>
    internal static class FormationDisconnectedCells
    {
        public static readonly Color DefaultTint = new(0.9f, 0.3f, 0.3f, 0.5f);

        // 호출자가 들고 있는 사전을 비우고 다시 채운다 - 편집마다 새 사전을 만들지 않는다.
        public static void CollectTints(FormationArea area, Color tint, Dictionary<int, Color> into)
        {
            into.Clear();
            if (area == null || area.ComponentCount <= 1) return;

            var slotCount = area.ColumnCount * area.RowCount;
            for (var slot = 0; slot < slotCount; slot++)
            {
                var component = area.GetComponentOf(slot);
                if (component >= 0 && component != area.LargestComponentId) into[slot] = tint;
            }
        }
    }
}
