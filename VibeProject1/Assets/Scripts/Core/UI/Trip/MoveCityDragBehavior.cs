using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// road-mode off일 때의 도시 드래그: 커서를 따라 이동하고, 지도 밖에서 놓으면 삭제, 안에서 놓으면
    /// 새 위치를 저장소에 반영한다. Moved 이벤트는 드래그 중 실시간으로 연결선 끝점을 갱신할 수 있게
    /// 코디네이터에 알리기 위한 것 - 이 클래스 자신은 연결선의 존재를 모른다(SRP).
    ///
    /// mapView.Viewport/Content를 생성자에서 미리 꺼내 캐시하지 않고 매번 다시 읽는다 - 상행 준비 UI
    /// 패널은 씬에 비활성 상태로 저장돼 있어 TripMapView.Awake()가 최초 오픈 시점까지 지연되는데,
    /// 이 배선(TripMapInteractionCoordinator.Bind)은 그보다 먼저(RegisterTripUI) 실행되므로 생성자
    /// 시점 값을 캐시하면 영원히 null이 박힌다.
    /// </summary>
    internal class MoveCityDragBehavior : ICityDragBehavior
    {
        private readonly ITripCityRepository cityRepository;
        private readonly TripMapView mapView;

        private TripDebugCityMarkerView draggingMarker;

        public event Action<string, Vector2> Moved;

        public MoveCityDragBehavior(ITripCityRepository cityRepository, TripMapView mapView)
        {
            this.cityRepository = cityRepository;
            this.mapView = mapView;
        }

        public void OnDragBegin(TripDebugCityMarkerView marker, PointerEventData eventData)
        {
            draggingMarker = marker;
        }

        public void OnDragUpdate(PointerEventData eventData)
        {
            if (draggingMarker == null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(mapView.Content, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                draggingMarker.SetAnchoredPosition(localPoint);
                Moved?.Invoke(draggingMarker.CityId, localPoint);
            }
        }

        public void OnDragEnd(PointerEventData eventData)
        {
            if (draggingMarker == null)
            {
                return;
            }

            var insideMap = RectTransformUtility.RectangleContainsScreenPoint(mapView.Viewport, eventData.position, eventData.pressEventCamera);
            if (insideMap)
            {
                cityRepository.UpdatePosition(draggingMarker.CityId, draggingMarker.RectTransform.anchoredPosition);
            }
            else
            {
                cityRepository.Remove(draggingMarker.CityId); // 지도 밖 드롭 = 삭제
            }

            draggingMarker = null;
        }
    }
}
