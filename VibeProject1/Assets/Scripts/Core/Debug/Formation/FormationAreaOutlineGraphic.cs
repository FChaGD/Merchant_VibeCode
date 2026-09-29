#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 대열 외곽선을 그래픽 하나로 그린다(FormationAreaOutlineDebugView가 격자 콘텐츠 아래에 만든다). 처음에는 칸마다 선 오브젝트를
    /// 붙였으나 화면을 채운 칸(1,550칸) × 10개 = 약 15,500개 오브젝트가 스크롤·확대 때마다 위치 갱신·마스크 컬링 대상이 되어 렉이 생겼다
    /// (2026-09-29 실전 확인) - 경계 변만 사각형으로 모아 메시 하나로 그린다.
    /// 좌표계: 콘텐츠와 같은 크기·피벗으로 늘려 두어, 칸의 localPosition + rect를 그대로 이 그래픽의 로컬 좌표로 쓴다. 칸 좌표가 바뀌는
    /// 확대/축소·범위 변경 때만 다시 그린다(드래그 스크롤은 콘텐츠째 움직여 다시 그릴 필요 없음).
    /// </summary>
    public class FormationAreaOutlineGraphic : MaskableGraphic
    {
        public enum Side { Left, Right, Top, Bottom }

        public readonly struct Edge
        {
            public readonly int SlotIndex;
            public readonly Side Side;
            public readonly Color Color;
            public readonly float InsetRatio;

            public Edge(int slotIndex, Side side, Color color, float insetRatio)
            {
                SlotIndex = slotIndex;
                Side = side;
                Color = color;
                InsetRatio = insetRatio;
            }
        }

        private readonly List<Edge> edges = new();
        private FormationGridView grid;
        private float thicknessRatio;

        public static FormationAreaOutlineGraphic Create(FormationGridView grid, float thicknessRatio)
        {
            var content = grid.ContentRect;
            // CanvasRenderer를 명시적으로 붙인다 - 이 UGUI 버전은 Graphic을 AddComponent해도 CanvasRenderer를 자동으로 붙이지 않아, 없으면 RectMask2D
            // 클리핑에서 매 프레임 예외가 나 캔버스 갱신 전체가 멈춘다(2026-09-29 실전 확인 - 정비창이 회색, 팔레트 사라짐).
            var go = new GameObject("DebugAreaOutline", typeof(RectTransform), typeof(CanvasRenderer), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().ignoreLayout = true; // 콘텐츠의 GridLayoutGroup이 칸으로 배치하지 않게
            var rect = (RectTransform)go.transform;
            rect.SetParent(content, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = content.pivot;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var graphic = go.AddComponent<FormationAreaOutlineGraphic>();
            graphic.raycastTarget = false; // 칸의 드롭을 가리지 않게
            graphic.thicknessRatio = thicknessRatio;
            graphic.grid = grid;
            grid.ViewChanged += graphic.SetVerticesDirty;
            return graphic;
        }

        public bool IsBoundTo(FormationGridView target) => grid == target;

        public void SetEdges(List<Edge> source)
        {
            edges.Clear();
            edges.AddRange(source);
            transform.SetAsLastSibling(); // 유닛 아이콘 위에
            SetVerticesDirty();
        }

        protected override void OnDestroy()
        {
            if (grid != null) grid.ViewChanged -= SetVerticesDirty;
            base.OnDestroy();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (grid == null) return;

            foreach (var edge in edges)
            {
                if (!grid.VisibleSlots.TryGetValue(edge.SlotIndex, out var slot)) continue;

                var slotRect = (RectTransform)slot.transform;
                var origin = (Vector2)slotRect.localPosition;
                var min = origin + slotRect.rect.min;
                var max = origin + slotRect.rect.max;
                var size = max.x - min.x;
                var inset = size * edge.InsetRatio;
                var thickness = size * thicknessRatio;
                min += new Vector2(inset, inset);
                max -= new Vector2(inset, inset);

                var quad = edge.Side switch
                {
                    Side.Left => Rect.MinMaxRect(min.x, min.y, min.x + thickness, max.y),
                    Side.Right => Rect.MinMaxRect(max.x - thickness, min.y, max.x, max.y),
                    Side.Top => Rect.MinMaxRect(min.x, max.y - thickness, max.x, max.y),
                    _ => Rect.MinMaxRect(min.x, min.y, max.x, min.y + thickness),
                };
                AddQuad(vh, quad, edge.Color);
            }
        }

        private static void AddQuad(VertexHelper vh, Rect quad, Color32 color)
        {
            var start = vh.currentVertCount;
            vh.AddVert(new Vector3(quad.xMin, quad.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(quad.xMin, quad.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(quad.xMax, quad.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(quad.xMax, quad.yMin), color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
#endif
