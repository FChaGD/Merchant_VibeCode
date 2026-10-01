#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 도로 모드가 아닐 때의 끝점(도시·관문) 드래그: 커서를 따라 모델 위치를 옮기고(표시는 모델 이동 이벤트로 TripMapPresenter가
    /// 갱신), 지도 밖에서 놓으면 삭제한다 - 관문은 짝과 도로까지 함께 지워진다(기획 68번 §3.5). 이 클래스는 선을 모른다(SRP).
    /// mapView.Viewport/Content는 패널 최초 활성화 전 null이라 매번 다시 읽는다.
    /// </summary>
    internal class MoveCityDragBehavior : ICityDragBehavior
    {
        private readonly IWorldMapEditor editor;
        private readonly TripMapView mapView;

        private MapNodeId? dragging;

        public MoveCityDragBehavior(IWorldMapEditor editor, TripMapView mapView)
        {
            this.editor = editor;
            this.mapView = mapView;
        }

        public void OnDragBegin(TripMapMarkerView marker, PointerEventData eventData) => dragging = marker.Node;

        public void OnDragUpdate(PointerEventData eventData)
        {
            if (dragging == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(mapView.Content, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                editor.MoveNode(dragging.Value, localPoint);
            }
        }

        public void OnDragEnd(PointerEventData eventData)
        {
            if (dragging == null) return;

            var node = dragging.Value;
            dragging = null;
            var insideMap = RectTransformUtility.RectangleContainsScreenPoint(mapView.Viewport, eventData.position, eventData.pressEventCamera);
            if (insideMap) return;

            // 지도 밖 드롭 = 삭제.
            if (node.IsCity) editor.RemoveCity(node.Id);
            else editor.RemoveGate(node.Id);
        }
    }
}
#endif
