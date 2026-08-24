#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// road-mode on일 때의 도시 드래그: 위치는 옮기지 않고 커서를 따라가는 임시 선을 그린다. 드래그를
    /// 다른 도시 위에서 놓으면 연결선을 생성하고, 그 외(빈 지도 등)에서 놓으면 취소한다. 자기 자신에게
    /// 돌아오는 드롭은 저장소의 TryAddRoute가 동일 id를 거부해 자연히 무시된다.
    ///
    /// 미리보기 선은 첫 드래그 시점에 지연 생성한다(createLine 팩토리) - Bind() 시점에 미리 만들어두면
    /// mapView.Content가 아직 null(패널이 비활성 상태라 TripMapView.Awake가 지연됨)일 때 인스턴스화돼
    /// 지도 바깥(부모 없음)에 잘못 생성된다. mapView.Viewport/Content 자체도 매번 새로 읽는다
    /// (MoveCityDragBehavior와 동일한 이유).
    /// </summary>
    internal class DrawRoadDragBehavior : ICityDragBehavior
    {
        private readonly ITripRouteRepository routeRepository;
        private readonly TripMapView mapView;
        private readonly Func<TripDebugRoadLineView> createLine;
        private readonly Func<PointerEventData, string> resolveCityUnderPointer;

        private TripDebugRoadLineView previewLine;
        private string originCityId;
        private Vector2 originLocalPosition;

        public DrawRoadDragBehavior(
            ITripRouteRepository routeRepository,
            TripMapView mapView,
            Func<TripDebugRoadLineView> createLine,
            Func<PointerEventData, string> resolveCityUnderPointer)
        {
            this.routeRepository = routeRepository;
            this.mapView = mapView;
            this.createLine = createLine;
            this.resolveCityUnderPointer = resolveCityUnderPointer;
        }

        public void OnDragBegin(TripDebugCityMarkerView marker, PointerEventData eventData)
        {
            if (previewLine == null)
            {
                previewLine = createLine();
                previewLine.SetRaycastTarget(false); // 커서를 따라가는 선 자신이 드롭 대상(도시) 레이캐스트를 가리면 안 된다
                previewLine.gameObject.SetActive(false);
            }

            originCityId = marker.CityId;
            originLocalPosition = marker.RectTransform.anchoredPosition;

            // 완성된 연결선은 도시 아이콘보다 뒤에 그리지만(CreateLineInstance), 드래그 중인 임시 선은
            // 지금 상호작용 중인 대상이라 항상 맨 앞에 보이는 게 자연스럽다.
            previewLine.transform.SetAsLastSibling();
            previewLine.gameObject.SetActive(true);
            previewLine.SetEndpoints(originLocalPosition, originLocalPosition);
        }

        public void OnDragUpdate(PointerEventData eventData)
        {
            if (originCityId == null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(mapView.Content, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                previewLine.SetEndpoints(originLocalPosition, localPoint);
            }
        }

        public void OnDragEnd(PointerEventData eventData)
        {
            if (previewLine != null)
            {
                previewLine.gameObject.SetActive(false);
            }

            if (originCityId == null)
            {
                return;
            }

            var targetCityId = resolveCityUnderPointer(eventData);
            if (targetCityId != null && targetCityId != originCityId)
            {
                routeRepository.TryAddRoute(originCityId, targetCityId);
            }
            // 도시가 아닌 곳에 드롭 = 취소, 아무 일도 하지 않는다.

            originCityId = null;
        }
    }
}
#endif
