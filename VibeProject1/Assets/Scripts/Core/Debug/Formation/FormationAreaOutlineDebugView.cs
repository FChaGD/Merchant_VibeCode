#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 마을 정비창 격자에 마차 영역·시설 영역의 외곽선을 그린다(2026-09-29). 시설 자리 제한(마차 영역 안만)과 마차 연결 판정을
    /// 눈으로 확인하려는 용도다. HubFormationPanel과 같은 UIManager 오브젝트에 붙여 GetComponent로 찾게 한다(매니저 종속 하위 컴포넌트 규칙).
    /// 칸이 영역 안이고 이웃 칸이 영역 밖인 변만 모아 FormationAreaOutlineGraphic(그래픽 하나)에 넘긴다 - 칸마다 오브젝트를 붙이던 첫 구현은
    /// 렉을 만들어 교체했다. 시설 선은 마차 선 안쪽에 그려 두 영역 경계가 겹쳐도 둘 다 보이게 한다.
    /// 설치/제거는 FormationAreaOutlineDebugInstaller. 걷어낼 때는 Remove 메뉴를 먼저 실행한 뒤 이 파일·FormationAreaOutlineGraphic·
    /// IFormationAreaOutline·설치기와 HubFormationPanel/FormationGridEditor의 연결 부분, DebugToolsBulkInstaller의 두 줄을 지운다.
    /// </summary>
    public class FormationAreaOutlineDebugView : MonoBehaviour, IFormationAreaOutline
    {
        private const float Thickness = 0.08f; // 칸 크기 대비 선 두께
        private static readonly Color WagonColor = new(0.1f, 0.35f, 1f, 1f);
        private static readonly Color FacilityColor = new(0.75f, 0.15f, 0.85f, 1f);

        private readonly List<FormationAreaOutlineGraphic.Edge> edgeBuffer = new();
        private FormationAreaOutlineGraphic graphic;

        public void Render(FormationArea area, FormationGridView grid)
        {
            if (area == null || grid == null || grid.ContentRect == null) return;

            if (graphic == null || !graphic.IsBoundTo(grid))
            {
                if (graphic != null) Destroy(graphic.gameObject);
                graphic = FormationAreaOutlineGraphic.Create(grid, Thickness);
            }

            edgeBuffer.Clear();
            foreach (var slotIndex in grid.VisibleSlots.Keys)
            {
                CollectEdges(area, area.IsWagonCell, slotIndex, WagonColor, 0f);
                CollectEdges(area, area.IsFacilityCell, slotIndex, FacilityColor, Thickness);
            }
            graphic.SetEdges(edgeBuffer);
        }

        private void CollectEdges(FormationArea area, Func<int, bool> contains, int slotIndex, Color color, float inset)
        {
            var column = slotIndex % area.ColumnCount;
            var row = slotIndex / area.ColumnCount;
            if (!contains(slotIndex)) return;

            bool In(int c, int r) => c >= 0 && c < area.ColumnCount && r >= 0 && r < area.RowCount && contains(r * area.ColumnCount + c);

            if (!In(column - 1, row)) edgeBuffer.Add(new(slotIndex, FormationAreaOutlineGraphic.Side.Left, color, inset));
            if (!In(column + 1, row)) edgeBuffer.Add(new(slotIndex, FormationAreaOutlineGraphic.Side.Right, color, inset));
            if (!In(column, row - 1)) edgeBuffer.Add(new(slotIndex, FormationAreaOutlineGraphic.Side.Top, color, inset)); // 행은 아래로 증가
            if (!In(column, row + 1)) edgeBuffer.Add(new(slotIndex, FormationAreaOutlineGraphic.Side.Bottom, color, inset));
        }
    }
}
#endif
