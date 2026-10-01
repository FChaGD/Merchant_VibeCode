using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 상행 준비 지도의 정식 표시(Docs/설계/69번 §5) - 지역 하나만 그리고, 도시 클릭은 도착지 지정, 관문 클릭·지역 드롭다운은 지역
    /// 전환을 한다. 예전엔 이 일이 디버그 조율자 안에 있어 빌드에서 지도가 비어 있었다. 편집(배치·이동·도로)은 모르고, 디버그 편집이
    /// 드래그 핸들러를 주입할 때만 마커 드래그가 동작한다.
    ///
    /// 지역을 바꿀 때 마커·선을 파괴·생성하지 않고 풀에서 재사용한다(CLAUDE.md 최적화). 지도 콘텐츠(TripMapView.Content)는 패널이
    /// 처음 켜질 때 Awake로 생기므로, 그리기는 Show(패널이 켜진 뒤)부터 한다.
    /// </summary>
    public sealed class TripMapPresenter : IDisposable
    {
        private const string MissingText = "값 없음";

        private readonly TripMapView mapView;
        private readonly TripMapMarkerView cityMarkerPrefab;
        private readonly TripMapMarkerView gateMarkerPrefab;
        private readonly TripRoadLineView linePrefab;
        private readonly TMP_Dropdown regionDropdown;
        private readonly TripLocationInfoView originInfoView;
        private readonly TripLocationInfoView destinationInfoView;
        private readonly IWorldMapReader map;
        private readonly ITripRouteReader routes;
        private readonly ITripCurrentLocationReader currentLocation;
        private readonly ITripDestinationAssigner destination;

        private readonly List<TripMapMarkerView> cityPool = new();
        private readonly List<TripMapMarkerView> gatePool = new();
        private readonly List<TripRoadLineView> linePool = new();
        private readonly Dictionary<MapNodeId, TripMapMarkerView> markersByNode = new();
        private readonly Dictionary<(MapNodeId, MapNodeId), TripRoadLineView> linesByRoad = new();
        private readonly List<int> dropdownRegionIds = new();

        private bool isShown;
        private Action<TripMapMarkerView, PointerEventData> editBeginDrag;
        private Action<PointerEventData> editDrag;
        private Action<PointerEventData> editEndDrag;
        private Action<MapNodeId, MapNodeId> editRoadDoubleClicked;

        public int ViewedRegionId { get; private set; }
        public TripMapView MapView => mapView;
        public event Action<int> ViewedRegionChanged;

        public TripMapPresenter(TripMapView mapView, TripMapMarkerView cityMarkerPrefab, TripMapMarkerView gateMarkerPrefab, TripRoadLineView linePrefab,
            TMP_Dropdown regionDropdown, TripLocationInfoView originInfoView, TripLocationInfoView destinationInfoView,
            IWorldMapReader map, ITripRouteReader routes, ITripCurrentLocationReader currentLocation, ITripDestinationAssigner destination)
        {
            this.mapView = mapView;
            this.cityMarkerPrefab = cityMarkerPrefab;
            this.gateMarkerPrefab = gateMarkerPrefab;
            this.linePrefab = linePrefab;
            this.regionDropdown = regionDropdown;
            this.originInfoView = originInfoView;
            this.destinationInfoView = destinationInfoView;
            this.map = map;
            this.routes = routes;
            this.currentLocation = currentLocation;
            this.destination = destination;

            map.Changed += HandleMapChanged;
            map.NodeMoved += HandleNodeMoved;
            destination.Changed += RefreshRolesAndInfo;
            currentLocation.Changed += RefreshRolesAndInfo;
            if (regionDropdown != null)
            {
                regionDropdown.onValueChanged.RemoveAllListeners();
                regionDropdown.onValueChanged.AddListener(HandleDropdownChanged);
            }
        }

        /// <summary>디버그 편집이 마커 드래그·선 더블클릭 동작을 붙인다(설계 69번 §6). 정식 표시만 쓸 때는 부르지 않는다.</summary>
        public void SetEditHandlers(Action<TripMapMarkerView, PointerEventData> beginDrag, Action<PointerEventData> drag, Action<PointerEventData> endDrag, Action<MapNodeId, MapNodeId> roadDoubleClicked)
        {
            editBeginDrag = beginDrag;
            editDrag = drag;
            editEndDrag = endDrag;
            editRoadDoubleClicked = roadDoubleClicked;
            if (isShown) Render();
        }

        /// <summary>패널을 열 때(패널이 켜진 뒤) - 현재 위치 도시의 지역을 띄운다(기획 68번 §4-5).</summary>
        public void Show()
        {
            isShown = true;
            ViewedRegionId = RegionOfCity(currentLocation.CurrentCityId) ?? FirstRegionId();
            Render();
            ViewedRegionChanged?.Invoke(ViewedRegionId);
        }

        public void Hide() => isShown = false;

        public void ShowRegion(int regionId)
        {
            if (!map.TryGetRegion(regionId, out _)) return;
            ViewedRegionId = regionId;
            if (isShown) Render();
            ViewedRegionChanged?.Invoke(regionId);
        }

        public bool TryGetMarker(MapNodeId node, out TripMapMarkerView marker) => markersByNode.TryGetValue(node, out marker);

        /// <summary>디버그 도로 그리기의 미리보기 선 - 풀과 별개로 하나 만든다.</summary>
        public TripRoadLineView CreateStandaloneLine()
        {
            var line = UnityEngine.Object.Instantiate(linePrefab, mapView.Content);
            line.SetRaycastTarget(false);
            return line;
        }

        public Vector2 MarkerSize => cityMarkerPrefab != null ? ((RectTransform)cityMarkerPrefab.transform).sizeDelta : new Vector2(48f, 48f);

        public void Dispose()
        {
            map.Changed -= HandleMapChanged;
            map.NodeMoved -= HandleNodeMoved;
            destination.Changed -= RefreshRolesAndInfo;
            currentLocation.Changed -= RefreshRolesAndInfo;
        }

        // ==================== 그리기 ====================

        private void Render()
        {
            RefreshDropdown();
            RefreshRolesAndInfo();
            if (mapView.Content == null) return;

            if (!map.TryGetRegion(ViewedRegionId, out _)) ViewedRegionId = FirstRegionId();
            markersByNode.Clear();
            linesByRoad.Clear();

            var cityIndex = 0;
            foreach (var city in map.GetCities(ViewedRegionId))
            {
                var marker = Take(cityPool, cityMarkerPrefab, cityIndex++);
                PrepareMarker(marker, MapNodeId.City(city.Id), null, city.Position);
            }
            Release(cityPool, cityIndex);

            var gateIndex = 0;
            foreach (var gate in map.GetGates(ViewedRegionId))
            {
                var marker = Take(gatePool, gateMarkerPrefab, gateIndex++);
                PrepareMarker(marker, MapNodeId.Gate(gate.Id), TargetRegionName(gate), gate.Position);
            }
            Release(gatePool, gateIndex);

            var lineIndex = 0;
            foreach (var (a, b) in map.GetRoads(ViewedRegionId))
            {
                if (!markersByNode.TryGetValue(a, out var markerA) || !markersByNode.TryGetValue(b, out var markerB)) continue;
                var line = Take(linePool, linePrefab, lineIndex++);
                // 선은 늘 마커 뒤에 그린다 - 아이콘을 가리지 않게.
                line.transform.SetAsFirstSibling();
                line.SetRaycastTarget(editRoadDoubleClicked != null);
                line.SetEndpoints(markerA.RectTransform.anchoredPosition, markerB.RectTransform.anchoredPosition);
                var roadA = a;
                var roadB = b;
                line.Initialize(editRoadDoubleClicked != null ? () => editRoadDoubleClicked(roadA, roadB) : null);
                linesByRoad[(a, b)] = line;
            }
            Release(linePool, lineIndex);

            RefreshRoleVisuals();
        }

        private void PrepareMarker(TripMapMarkerView marker, MapNodeId node, string label, Vector2 position)
        {
            marker.Bind(node, label);
            marker.SetAnchoredPosition(position);
            marker.transform.SetAsLastSibling();
            marker.SetHandlers(HandleMarkerClicked, editBeginDrag, editDrag, editEndDrag);
            markersByNode[node] = marker;
        }

        private T Take<T>(List<T> pool, T prefab, int index) where T : Component
        {
            if (index >= pool.Count) pool.Add(UnityEngine.Object.Instantiate(prefab, mapView.Content));
            var item = pool[index];
            item.gameObject.SetActive(true);
            return item;
        }

        private static void Release<T>(List<T> pool, int used) where T : Component
        {
            for (var i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }

        private void HandleMapChanged()
        {
            if (isShown) Render();
            else RefreshDropdown();
        }

        // 드래그 중 매 프레임 불린다 - 그 끝점의 선만 고친다.
        private void HandleNodeMoved(MapNodeId node, Vector2 position)
        {
            if (!isShown || !markersByNode.TryGetValue(node, out var marker)) return;
            marker.SetAnchoredPosition(position);
            foreach (var other in map.GetConnectedNodes(node))
            {
                if (!markersByNode.TryGetValue(other, out var otherMarker)) continue;
                if (linesByRoad.TryGetValue((node, other), out var line)) line.SetEndpoints(position, otherMarker.RectTransform.anchoredPosition);
                else if (linesByRoad.TryGetValue((other, node), out line)) line.SetEndpoints(otherMarker.RectTransform.anchoredPosition, position);
            }
        }

        // ==================== 입력 ====================

        private void HandleMarkerClicked(MapNodeId node)
        {
            if (node.IsCity)
            {
                destination.HandleCityClicked(node.Id, routes);
                return;
            }

            // 관문은 도착지가 아니라 통로 - 짝 관문이 있는 지역으로 지도를 바꾼다(기획 68번 §4-4).
            if (map.TryGetGate(node.Id, out var gate) && map.TryGetGate(gate.PairGateId, out var pair)) ShowRegion(pair.RegionId);
        }

        private void HandleDropdownChanged(int index)
        {
            if (index >= 0 && index < dropdownRegionIds.Count && dropdownRegionIds[index] != ViewedRegionId) ShowRegion(dropdownRegionIds[index]);
        }

        private void RefreshDropdown()
        {
            if (regionDropdown == null) return;

            dropdownRegionIds.Clear();
            var options = new List<TMP_Dropdown.OptionData>();
            foreach (var region in map.Regions)
            {
                dropdownRegionIds.Add(region.Id);
                options.Add(new TMP_Dropdown.OptionData(region.Name));
            }
            regionDropdown.ClearOptions();
            regionDropdown.AddOptions(options);
            regionDropdown.SetValueWithoutNotify(Mathf.Max(0, dropdownRegionIds.IndexOf(ViewedRegionId)));
            regionDropdown.RefreshShownValue();
        }

        // ==================== 역할 강조·정보 패널 ====================

        private void RefreshRolesAndInfo()
        {
            originInfoView.Show(BuildLocationInfo(currentLocation.CurrentCityId));
            if (destination.DestinationCityId is { } destinationId) destinationInfoView.Show(BuildLocationInfo(destinationId));
            else destinationInfoView.Clear();
            RefreshRoleVisuals();
        }

        // 현재 위치·도착지가 보이는 지역에 있을 때만 강조된다 - 다른 지역 도시는 마커가 없다(기획 68번 §4-7).
        private void RefreshRoleVisuals()
        {
            foreach (var pair in markersByNode)
            {
                TripRole? role = null;
                if (pair.Key.IsCity && pair.Key.Id == currentLocation.CurrentCityId) role = TripRole.Origin;
                else if (pair.Key.IsCity && pair.Key.Id == destination.DestinationCityId) role = TripRole.Destination;
                pair.Value.SetRoleVisual(role);
            }
        }

        // 이름이 없는 도시(갓 배치한 도시 등)는 기존 자리표시자로 폴백한다. 지역 이름을 함께 붙인다(기획 68번 §4-6).
        private ITripLocationInfo BuildLocationInfo(int cityId)
        {
            var icon = cityMarkerPrefab != null ? cityMarkerPrefab.Icon : null;
            if (!map.TryGetCity(cityId, out var city))
            {
                return new PlaceholderTripLocationInfo(cityId, $"디버그 도시 {cityId}", MissingText, icon);
            }

            var name = string.IsNullOrEmpty(city.Name) ? $"디버그 도시 {cityId}" : city.Name;
            var regionName = map.TryGetRegion(city.RegionId, out var region) ? region.Name : MissingText;
            var description = string.IsNullOrEmpty(city.Description) ? MissingText : city.Description;
            return new PlaceholderTripLocationInfo(cityId, $"{name} ({regionName})", description, icon);
        }

        private string TargetRegionName(WorldGate gate)
            => map.TryGetGate(gate.PairGateId, out var pair) && map.TryGetRegion(pair.RegionId, out var region) ? region.Name : MissingText;

        private int? RegionOfCity(int cityId) => map.TryGetCity(cityId, out var city) ? city.RegionId : null;

        private int FirstRegionId() => map.Regions.Count > 0 ? map.Regions[0].Id : 0;
    }
}
