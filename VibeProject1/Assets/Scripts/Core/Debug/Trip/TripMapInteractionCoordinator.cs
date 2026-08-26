#if UNITY_EDITOR
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 상행 준비 UI 지도 위 상호작용(도시 배치/경로 연결 - 디버그, 출발/도착 지정 - 정식)의 배선을
    /// 전담하는 조율자. TripPanel이 이 배선까지 직접 들고 있으면 책임이 비대해지므로 분리했다
    /// (SRP - UIManager/PanelNavigationStack 분리와 동일한 판단). 세션 동안 유지돼야 하는 도시/경로/
    /// 배정 데이터를 이 클래스가 직접 소유하는 순수 C# 클래스다 - TripPanel과 생명주기가 같은
    /// (Bootstrap 씬, 영속) 필드로 보관되므로 별도 MonoBehaviour나 전역 DI 없이도 세션 내내 유지된다.
    ///
    /// 클래스 전체가 디버그 도시/경로 저장소(InMemoryTripCityRepository/InMemoryTripRouteRepository)에
    /// 의존하므로 이 파일도 함께 Core/Debug/Trip에 있다 - 다만 내부에서 생성하는 TripOriginDestinationAssigner
    /// 자체는 ITripRouteReader에만 의존하는 정식 클래스라(Core/UI/Trip) 실제 지역/경로 데이터 시스템이
    /// 생기면 이 코디네이터만 그 시스템에 맞게 다시 짜면 되고 TripOriginDestinationAssigner는 그대로 둔다.
    /// </summary>
    internal class TripMapInteractionCoordinator
    {
        private readonly ITripCityRepository cityRepository = new InMemoryTripCityRepository();
        private readonly ITripRouteRepository routeRepository = new InMemoryTripRouteRepository();
        private readonly TripDebugRoadModeController roadMode = new TripDebugRoadModeController();
        private readonly TripOriginDestinationAssigner assigner;

        private readonly Dictionary<string, TripDebugCityMarkerView> markersByCityId = new();
        private readonly Dictionary<string, TripDebugRoadLineView> linesByRouteKey = new();

        private TripMapView mapView;
        private TripDebugCityMarkerView markerPrefab;
        private TripDebugRoadLineView linePrefab;
        private TripLocationInfoView originInfoView;
        private TripLocationInfoView destinationInfoView;
        private Image dragGhost;
        private Sprite cityIconSprite;
        private Vector2 markerBaseSize;

        private MoveCityDragBehavior moveBehavior;
        private DrawRoadDragBehavior drawRoadBehavior;
        private ICityDragBehavior activeDragBehavior;

        public TripMapInteractionCoordinator()
        {
            assigner = new TripOriginDestinationAssigner(routeRepository);
        }

        /// <summary>TripPanel이 "상행 시작" 게이팅(둘 다 배정돼야 활성화, 02번 5절)에 쓴다.</summary>
        public ITripOriginDestinationReader OriginDestinationReader => assigner;

        /// <summary>
        /// 상행 준비 UI "종료" 버튼 전용 초기화. 배치 UI를 왕복할 때는 배정이 유지돼야 하므로
        /// TripPanel.Open()에서는 호출하지 않는다 - 오직 종료(닫기) 시에만 호출한다.
        /// </summary>
        public void ResetOriginDestination() => assigner.Reset();

        public void Bind(
            TripMapView mapView,
            TripDebugCityMarkerView markerPrefab,
            TripDebugCityPaletteView paletteView,
            TripDebugRoadLineView linePrefab,
            TripDebugRoadToggleView roadToggleView,
            Button cityBulkDeleteButton,
            Button roadBulkDeleteButton,
            Transform rootCanvasTransform,
            TripLocationInfoView originInfoView,
            TripLocationInfoView destinationInfoView)
        {
            this.mapView = mapView;
            this.markerPrefab = markerPrefab;
            this.linePrefab = linePrefab;
            this.originInfoView = originInfoView;
            this.destinationInfoView = destinationInfoView;

            moveBehavior = new MoveCityDragBehavior(cityRepository, mapView);
            moveBehavior.Moved += RefreshLinesForCity;
            drawRoadBehavior = new DrawRoadDragBehavior(routeRepository, mapView, CreateLineInstance, ResolveCityUnderPointer);

            CreateDragGhost(paletteView.Icon, rootCanvasTransform);
            paletteView.Initialize(HandlePaletteBeginDrag, HandlePaletteDrag, HandlePaletteEndDrag);

            roadToggleView.Initialize(() => roadMode.Toggle());
            roadToggleView.SetActiveVisual(roadMode.IsRoadModeActive);
            roadMode.Changed += isActive =>
            {
                roadToggleView.SetActiveVisual(isActive);
                cityBulkDeleteButton.interactable = !isActive; // road-mode 중에는 도시 삭제 전면 불가(개별/전체)
            };

            cityBulkDeleteButton.onClick.RemoveAllListeners();
            cityBulkDeleteButton.onClick.AddListener(() =>
            {
                if (!roadMode.IsRoadModeActive)
                {
                    cityRepository.Clear();
                }
            });

            roadBulkDeleteButton.onClick.RemoveAllListeners();
            roadBulkDeleteButton.onClick.AddListener(() => routeRepository.Clear());

            cityRepository.CityRemoved += HandleCityMarkerRemoved;
            cityRepository.CityRemoved += cityId => routeRepository.RemoveAllRoutesFor(cityId); // 도시 삭제 시 연결선 연쇄 삭제
            cityRepository.CityRemoved += assigner.HandleCityDeleted; // 도시 삭제 시 그 역할만 해제(반대편 유지, 02번 3.1절)
            routeRepository.RouteAdded += HandleRouteAdded;
            routeRepository.RouteRemoved += HandleRouteRemoved;

            originInfoView.SetPanelClickHandler(() => assigner.HandlePanelClicked(TripRole.Origin));
            destinationInfoView.SetPanelClickHandler(() => assigner.HandlePanelClicked(TripRole.Destination));
            assigner.Changed += RefreshOriginDestinationDisplay;
        }

        private void CreateDragGhost(Sprite icon, Transform rootCanvasTransform)
        {
            // 배치된 마커는 지도 content의 자식이라 확대/축소 배율을 그대로 물려받아 커지고 작아지는데,
            // 이 고스트는 루트 캔버스 자식이라(팔레트 밖 지도 영역 밖에서도 보여야 하므로 content 밖에
            // 둘 수밖에 없다) 그 배율을 자동으로 물려받지 못한다. 그래서 드래그를 시작할 때마다
            // 마커 프리팹의 실제 크기 x 현재 줌 배율로 직접 맞춰준다(HandlePaletteBeginDrag 참고).
            markerBaseSize = markerPrefab != null ? ((RectTransform)markerPrefab.transform).sizeDelta : new Vector2(48f, 48f);
            cityIconSprite = icon; // 정보 패널에 표시할 아이콘도 동일한 플레이스홀더 스프라이트를 쓴다(03번 4.2절)

            if (rootCanvasTransform == null)
            {
                return;
            }

            var go = new GameObject("TripDebugCityDragGhost", typeof(RectTransform));
            go.transform.SetParent(rootCanvasTransform, false);
            ((RectTransform)go.transform).sizeDelta = markerBaseSize;

            var image = go.AddComponent<Image>();
            image.sprite = icon;
            image.raycastTarget = false;
            image.preserveAspect = true;
            go.SetActive(false);

            dragGhost = image;
        }

        private void HandlePaletteBeginDrag(PointerEventData eventData)
        {
            if (dragGhost == null)
            {
                return;
            }

            var currentZoom = mapView.Content != null ? mapView.Content.localScale.x : 1f;
            dragGhost.rectTransform.sizeDelta = markerBaseSize * currentZoom;

            dragGhost.gameObject.SetActive(true);
            dragGhost.transform.SetAsLastSibling();
            dragGhost.transform.position = eventData.position;
        }

        private void HandlePaletteDrag(PointerEventData eventData)
        {
            if (dragGhost != null)
            {
                dragGhost.transform.position = eventData.position;
            }
        }

        private void HandlePaletteEndDrag(PointerEventData eventData)
        {
            if (dragGhost != null)
            {
                dragGhost.gameObject.SetActive(false);
            }

            var insideMap = RectTransformUtility.RectangleContainsScreenPoint(mapView.Viewport, eventData.position, eventData.pressEventCamera);
            if (!insideMap)
            {
                return; // 무효 드롭 = 배치 취소
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapView.Content, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                return;
            }

            var cityId = cityRepository.Add(localPoint);
            CreateMarker(cityId, localPoint);
        }

        private void CreateMarker(string cityId, Vector2 position)
        {
            var marker = Object.Instantiate(markerPrefab, mapView.Content);
            marker.Bind(cityId);
            marker.SetAnchoredPosition(position);
            marker.SetHandlers(HandleMarkerClicked, HandleMarkerBeginDrag, HandleMarkerDrag, HandleMarkerEndDrag);
            markersByCityId[cityId] = marker;
        }

        // road-mode 여부와 무관하게 항상 호출된다 - 클릭은 드래그와 별개 이벤트이기 때문이다(04번 4.2절).
        private void HandleMarkerClicked(string cityId) => assigner.HandleCityClicked(cityId);

        private void HandleMarkerBeginDrag(TripDebugCityMarkerView marker, PointerEventData eventData)
        {
            activeDragBehavior = roadMode.IsRoadModeActive ? (ICityDragBehavior)drawRoadBehavior : moveBehavior;
            activeDragBehavior.OnDragBegin(marker, eventData);
        }

        private void HandleMarkerDrag(PointerEventData eventData) => activeDragBehavior?.OnDragUpdate(eventData);

        private void HandleMarkerEndDrag(PointerEventData eventData)
        {
            activeDragBehavior?.OnDragEnd(eventData);
            activeDragBehavior = null;
        }

        private string ResolveCityUnderPointer(PointerEventData eventData)
        {
            var target = eventData.pointerCurrentRaycast.gameObject;
            if (target == null)
            {
                return null;
            }

            var marker = target.GetComponentInParent<TripDebugCityMarkerView>();
            return marker != null ? marker.CityId : null;
        }

        // 연결된 도시만 routeRepository의 인접 정보(RemoveAllRoutesFor와 같은 자료구조)로 바로 조회한다 -
        // 드래그 중 매 프레임 호출되므로 전체 노선을 훑거나 키 문자열을 매번 분해하지 않는다.
        private void RefreshLinesForCity(string cityId, Vector2 newPosition)
        {
            foreach (var otherId in routeRepository.GetConnectedCityIds(cityId))
            {
                if (!linesByRouteKey.TryGetValue(RouteKey(cityId, otherId), out var line))
                {
                    continue;
                }

                if (markersByCityId.TryGetValue(otherId, out var otherMarker))
                {
                    line.SetEndpoints(newPosition, otherMarker.RectTransform.anchoredPosition);
                }
            }
        }

        private void HandleCityMarkerRemoved(string cityId)
        {
            if (!markersByCityId.TryGetValue(cityId, out var marker))
            {
                return;
            }

            if (marker != null)
            {
                Object.Destroy(marker.gameObject);
            }
            markersByCityId.Remove(cityId);
        }

        private void HandleRouteAdded(string cityIdA, string cityIdB)
        {
            var line = CreateLineInstance();
            if (markersByCityId.TryGetValue(cityIdA, out var markerA) && markersByCityId.TryGetValue(cityIdB, out var markerB))
            {
                line.SetEndpoints(markerA.RectTransform.anchoredPosition, markerB.RectTransform.anchoredPosition);
            }
            line.Initialize(() => routeRepository.RemoveRoute(cityIdA, cityIdB));
            linesByRouteKey[RouteKey(cityIdA, cityIdB)] = line;
        }

        private void HandleRouteRemoved(string cityIdA, string cityIdB)
        {
            var key = RouteKey(cityIdA, cityIdB);
            if (!linesByRouteKey.TryGetValue(key, out var line))
            {
                return;
            }

            if (line != null)
            {
                Object.Destroy(line.gameObject);
            }
            linesByRouteKey.Remove(key);
        }

        // 연결선은 항상 도시 아이콘보다 뒤에 그려야 아이콘을 가리지 않는다 - 새로 생기는 마커는 항상
        // content의 마지막 형제로 붙으므로(Object.Instantiate 기본 동작), 선을 첫 형제로 보내두면
        // 생성 순서와 무관하게 "선은 뒤, 아이콘은 앞" 관계가 항상 성립한다.
        private TripDebugRoadLineView CreateLineInstance()
        {
            var line = Object.Instantiate(linePrefab, mapView.Content);
            line.transform.SetAsFirstSibling();
            return line;
        }

        private static string RouteKey(string a, string b) => string.CompareOrdinal(a, b) < 0 ? $"{a}|{b}" : $"{b}|{a}";

        // 배정이 바뀔 때마다(지정/취소/교환/삭제로 인한 해제) assigner.Changed가 이 메서드를 부른다.
        // 상태 머신의 모든 전이가 결국 클릭에서 비롯되므로, 여기서 갱신하는 것만으로 "정보 패널은
        // 클릭이 유일한 트리거"(02번 3.1절) 요구사항이 자연히 성립한다.
        private void RefreshOriginDestinationDisplay()
        {
            ShowOrClear(originInfoView, assigner.OriginCityId);
            ShowOrClear(destinationInfoView, assigner.DestinationCityId);
            RefreshMarkerRoleVisuals();
        }

        private void ShowOrClear(TripLocationInfoView view, string cityId)
        {
            if (string.IsNullOrEmpty(cityId))
            {
                view.Clear();
                return;
            }

            view.Show(BuildLocationInfo(cityId));
        }

        // 배치된 도시는 실제 지역 데이터가 없으므로 표시 정보는 자동 생성한 placeholder 값을 쓴다
        // (03번 4.2절) - 기존 PlaceholderTripLocationInfo를 그대로 재사용한다(새 클래스 불필요).
        private ITripLocationInfo BuildLocationInfo(string cityId)
        {
            return new PlaceholderTripLocationInfo(cityId, $"디버그 도시 {ExtractSequence(cityId)}", "값 없음", cityIconSprite);
        }

        private static string ExtractSequence(string cityId)
        {
            var dashIndex = cityId.LastIndexOf('-');
            return dashIndex >= 0 ? cityId[(dashIndex + 1)..] : cityId;
        }

        private void RefreshMarkerRoleVisuals()
        {
            foreach (var pair in markersByCityId)
            {
                TripRole? role = null;
                if (pair.Key == assigner.OriginCityId)
                {
                    role = TripRole.Origin;
                }
                else if (pair.Key == assigner.DestinationCityId)
                {
                    role = TripRole.Destination;
                }

                pair.Value.SetRoleVisual(role);
            }
        }
    }
}
#endif
