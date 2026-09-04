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
    /// 의존하므로 이 파일도 함께 Core/Debug/Trip에 있다 - 다만 도착지 지정 로직(TripDestinationAssigner)
    /// 자체는 ITripRouteReader/ITripCurrentLocationReader에만 의존하는 정식 클래스라(Core/UI/Trip,
    /// 전역 DI로 주입받는다 - 설계 21번 §2.2) 실제 지역/경로 데이터 시스템이 생기면 이 코디네이터만
    /// 그 시스템에 맞는 새 코디네이터로 대체하면 되고 TripDestinationAssigner는 그대로 둔다.
    /// </summary>
    internal class TripMapInteractionCoordinator
    {
        private readonly ITripCityRepository cityRepository = new InMemoryTripCityRepository();
        private readonly ITripRouteRepository routeRepository = new InMemoryTripRouteRepository();
        private readonly TripDebugRoadModeController roadMode = new TripDebugRoadModeController();

        private readonly Dictionary<int, TripDebugCityMarkerView> markersByCityId = new();
        private readonly Dictionary<(int, int), TripDebugRoadLineView> linesByRouteKey = new();

        private TripMapView mapView;
        private TripDebugCityMarkerView markerPrefab;
        private TripDebugRoadLineView linePrefab;
        private TripLocationInfoView originInfoView;
        private TripLocationInfoView destinationInfoView;
        private ITripCurrentLocationReader currentLocationReader;
        private ITripDestinationAssigner destinationAssigner;
        private Image dragGhost;
        private Sprite cityIconSprite;
        private Vector2 markerBaseSize;
        private TripCityStringsTableAsset cityStringsTable;

        private MoveCityDragBehavior moveBehavior;
        private DrawRoadDragBehavior drawRoadBehavior;
        private ICityDragBehavior activeDragBehavior;

        public void Bind(
            TripMapView mapView,
            TripDebugCityMarkerView markerPrefab,
            TripDebugCityPaletteView paletteView,
            TripDebugRoadLineView linePrefab,
            TripDebugRoadToggleView roadToggleView,
            Button cityBulkDeleteButton,
            Button roadBulkDeleteButton,
            Button saveButton,
            Transform rootCanvasTransform,
            TripLocationInfoView originInfoView,
            TripLocationInfoView destinationInfoView,
            ITripCurrentLocationReader currentLocationReader,
            ITripDestinationAssigner destinationAssigner)
        {
            this.mapView = mapView;
            this.markerPrefab = markerPrefab;
            this.linePrefab = linePrefab;
            this.originInfoView = originInfoView;
            this.destinationInfoView = destinationInfoView;
            this.currentLocationReader = currentLocationReader;
            this.destinationAssigner = destinationAssigner;

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

            saveButton.onClick.RemoveAllListeners();
            saveButton.onClick.AddListener(() => TripCityMapPersistence.Save(cityRepository, routeRepository));

            cityRepository.CityRemoved += HandleCityMarkerRemoved;
            cityRepository.CityRemoved += cityId => routeRepository.RemoveAllRoutesFor(cityId); // 도시 삭제 시 연결선 연쇄 삭제
            cityRepository.CityRemoved += destinationAssigner.HandleCityDeleted; // 도시 삭제 시 도착지 배정만 해제(기획 16번 §6)
            routeRepository.RouteAdded += HandleRouteAdded;
            routeRepository.RouteRemoved += HandleRouteRemoved;

            destinationAssigner.Changed += RefreshDestinationDisplay;
            currentLocationReader.Changed += RefreshDestinationDisplay; // 방어적 구독 - 지금은 패널이 열려있는 동안 바뀔 일이 없지만 비용이 거의 없다.

            // 불러오기는 여기서 하지 않는다 - mapView.Content/Viewport는 TripMapView.Awake 이전엔
            // null인데, 이 상행 준비 UI 패널은 씬에 비활성 상태로 배치돼 Awake가 최초 활성화
            // (TripPanel.Open) 시점까지 지연된다(TripMapView.cs 주석 참고). Bind()는 그보다 먼저
            // (RegisterTripUI, 패널이 아직 비활성) 실행되므로, 여기서 마커를 생성하면 Content가 null이라
            // 부모 없는 오브젝트로 만들어져 화면에 나타나지 않는다 - 실제로 겪은 버그(2026-09-03).
            // EnsureSavedMapLoaded()를 TripPanel.Open() 이후(패널 활성화 후)에 호출해야 한다.
        }

        private bool hasLoadedSavedMap;

        /// <summary>
        /// 저장된 지도가 있으면 도시/경로/이름을 그대로 복원한다 - TripPanel.Open()이 패널을 활성화한
        /// "뒤"에 호출해야 한다(위 Bind() 주석 참고). 패널을 여러 번 열어도 세션당 한 번만 불러오도록
        /// 가드한다(재호출 시 같은 도시를 중복 인스턴스화하는 것을 막기 위함).
        /// </summary>
        public void EnsureSavedMapLoaded()
        {
            if (hasLoadedSavedMap)
            {
                return;
            }
            hasLoadedSavedMap = true;

            TripCityMapPersistence.TryLoadStrings(out cityStringsTable); // 없어도(임포트 전) null로 두고 계속 진행 - BuildLocationInfo가 폴백한다.

            if (TripCityMapPersistence.TryLoad(out var asset))
            {
                // 도착지 배정은 저장 대상이 아니라서(기획 15번 §2) 불러온 직후에도 미배정 상태로 시작한다.
                foreach (var city in asset.Cities)
                {
                    cityRepository.AddWithId(city.CityId, city.MapPosition);
                    CreateMarker(city.CityId, city.MapPosition);
                }

                foreach (var route in asset.Routes)
                {
                    routeRepository.TryAddRoute(route.CityIdA, route.CityIdB);
                }
            }

            // 출발지 패널은 클릭 없이도 항상 "현재 위치"를 보여줘야 한다(기획 §5) - 저장된 지도 유무와
            // 무관하게 항상 최초 1회 직접 갱신한다.
            RefreshDestinationDisplay();
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

        private void CreateMarker(int cityId, Vector2 position)
        {
            var marker = Object.Instantiate(markerPrefab, mapView.Content);
            marker.Bind(cityId);
            marker.SetAnchoredPosition(position);
            marker.SetHandlers(HandleMarkerClicked, HandleMarkerBeginDrag, HandleMarkerDrag, HandleMarkerEndDrag);
            markersByCityId[cityId] = marker;
        }

        // road-mode 여부와 무관하게 항상 호출된다 - 클릭은 드래그와 별개 이벤트이기 때문이다(04번 4.2절).
        private void HandleMarkerClicked(int cityId) => destinationAssigner.HandleCityClicked(cityId, routeRepository);

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

        private int? ResolveCityUnderPointer(PointerEventData eventData)
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
        // 드래그 중 매 프레임 호출되므로 전체 노선을 훑지 않는다.
        private void RefreshLinesForCity(int cityId, Vector2 newPosition)
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

        private void HandleCityMarkerRemoved(int cityId)
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

        private void HandleRouteAdded(int cityIdA, int cityIdB)
        {
            var line = CreateLineInstance();
            if (markersByCityId.TryGetValue(cityIdA, out var markerA) && markersByCityId.TryGetValue(cityIdB, out var markerB))
            {
                line.SetEndpoints(markerA.RectTransform.anchoredPosition, markerB.RectTransform.anchoredPosition);
            }
            line.Initialize(() => routeRepository.RemoveRoute(cityIdA, cityIdB));
            linesByRouteKey[RouteKey(cityIdA, cityIdB)] = line;
        }

        private void HandleRouteRemoved(int cityIdA, int cityIdB)
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

        private static (int, int) RouteKey(int a, int b) => a <= b ? (a, b) : (b, a);

        // 도착지 배정이 바뀔 때마다(지정/취소/변경/삭제로 인한 해제) destinationAssigner.Changed가,
        // "현재 위치"가 바뀔 때(상행 도착)마다 currentLocationReader.Changed가 이 메서드를 부른다.
        private void RefreshDestinationDisplay()
        {
            // 출발지 패널은 항상 "현재 위치"를 표시한다(기획 §5) - Clear 분기 없음.
            originInfoView.Show(BuildLocationInfo(currentLocationReader.CurrentCityId));
            ShowOrClear(destinationInfoView, destinationAssigner.DestinationCityId);
            RefreshMarkerRoleVisuals();
        }

        private void ShowOrClear(TripLocationInfoView view, int? cityId)
        {
            if (cityId == null)
            {
                view.Clear();
                return;
            }

            view.Show(BuildLocationInfo(cityId.Value));
        }

        // 엑셀 String 시트(TripCityStrings)에 실제 이름/설명이 있으면 그걸 쓴다(기획 15번 §8.4, 설계
        // 20번 §9.6) - 창작 금지 원칙과 반대로, 있는 데이터는 실데이터를 쓴다. 아직 이름을 안 붙인
        // 도시(갓 드래그 배치 등)는 기존처럼 자동 생성 placeholder로 폴백한다(03번 4.2절).
        private ITripLocationInfo BuildLocationInfo(int cityId)
        {
            if (cityStringsTable != null && cityStringsTable.TryGetEntry(cityId, out var entry))
            {
                return new PlaceholderTripLocationInfo(cityId, entry.Name, entry.Description, cityIconSprite);
            }

            return new PlaceholderTripLocationInfo(cityId, $"디버그 도시 {cityId}", "값 없음", cityIconSprite);
        }

        private void RefreshMarkerRoleVisuals()
        {
            foreach (var pair in markersByCityId)
            {
                TripRole? role = null;
                if (pair.Key == currentLocationReader.CurrentCityId)
                {
                    role = TripRole.Origin;
                }
                else if (pair.Key == destinationAssigner.DestinationCityId)
                {
                    role = TripRole.Destination;
                }

                pair.Value.SetRoleVisual(role);
            }
        }
    }
}
#endif
