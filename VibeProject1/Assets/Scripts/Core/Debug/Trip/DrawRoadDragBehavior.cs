#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 도로 모드일 때의 끝점 드래그: 위치는 옮기지 않고 커서를 따라가는 미리보기 선을 그린다. 다른 끝점(도시·관문) 위에서 놓으면
    /// 도로를 요청하고, 그 외에서 놓으면 취소한다. 같은 지역 끝점끼리만·자기 자신 제외 같은 규칙은 모델(IWorldMapEditor.TryAddRoad)이
    /// 지킨다(설계 69번 §9-4). 미리보기 선은 첫 드래그 때 지연 생성한다(패널 최초 활성화 전 Content가 null).
    /// </summary>
    internal class DrawRoadDragBehavior : ICityDragBehavior
    {
        private readonly IWorldMapEditor editor;
        private readonly TripMapView mapView;
        private readonly Func<TripRoadLineView> createLine;
        private readonly Func<PointerEventData, MapNodeId?> resolveNodeUnderPointer;

        private TripRoadLineView previewLine;
        private MapNodeId? origin;
        private Vector2 originPosition;

        public DrawRoadDragBehavior(IWorldMapEditor editor, TripMapView mapView, Func<TripRoadLineView> createLine, Func<PointerEventData, MapNodeId?> resolveNodeUnderPointer)
        {
            this.editor = editor;
            this.mapView = mapView;
            this.createLine = createLine;
            this.resolveNodeUnderPointer = resolveNodeUnderPointer;
        }

        public void OnDragBegin(TripMapMarkerView marker, PointerEventData eventData)
        {
            if (previewLine == null)
            {
                previewLine = createLine();
                previewLine.gameObject.SetActive(false);
            }

            origin = marker.Node;
            originPosition = marker.RectTransform.anchoredPosition;
            // 드래그 중인 선은 지금 다루는 대상이라 맨 앞에 보인다.
            previewLine.transform.SetAsLastSibling();
            previewLine.gameObject.SetActive(true);
            previewLine.SetEndpoints(originPosition, originPosition);
        }

        public void OnDragUpdate(PointerEventData eventData)
        {
            if (origin == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(mapView.Content, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                previewLine.SetEndpoints(originPosition, localPoint);
            }
        }

        public void OnDragEnd(PointerEventData eventData)
        {
            if (previewLine != null) previewLine.gameObject.SetActive(false);
            if (origin == null) return;

            var target = resolveNodeUnderPointer(eventData);
            if (target != null) editor.TryAddRoad(origin.Value, target.Value);
            origin = null;
        }
    }
}
#endif
