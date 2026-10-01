using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 월드 지도 모델을 자산에서 만들어 전역 DI로 내놓는다(Docs/설계/69번 §4.3). Bootstrap 상주라 Hub를 오가도 지도 편집 상태(디버그)와
    /// 도달 판정이 유지된다. 편집 계약(IWorldMapEditor)은 에디터에서만 등록한다 - 빌드에서는 지도를 읽기만 한다.
    /// 모델은 RegisterSelf에서 만든다 - 다른 컴포넌트의 ResolveDependencies보다 먼저 준비돼 있어야 하기 때문이다.
    /// </summary>
    public class WorldMapProvider : MonoBehaviour, IManagedComponent
    {
        [SerializeField] private TripCityMapAsset cityMap;
        [SerializeField] private TripCityStringsTableAsset cityStrings;
        [SerializeField] private TripRegionStringsTableAsset regionStrings;

        public WorldMap Map { get; private set; }

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            Map = Build(cityMap, cityStrings, regionStrings);
            registrar.Register<IWorldMapReader>(Map);
            registrar.Register<ITripRouteReader>(Map);
#if UNITY_EDITOR
            registrar.Register<IWorldMapEditor>(Map);
#endif
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
        }

        public static WorldMap Build(TripCityMapAsset cityMap, TripCityStringsTableAsset cityStrings, TripRegionStringsTableAsset regionStrings)
        {
            var map = new WorldMap();
            if (cityMap == null)
            {
                Debug.LogWarning($"{nameof(WorldMapProvider)}: 지도 자산이 없어 빈 지도로 시작한다(Tools > Game > Build Bootstrap Scene).");
                return map;
            }

            foreach (var region in cityMap.Regions)
            {
                map.RestoreRegion(region.RegionId, FindRegionName(regionStrings, region.RegionId), region.Size);
            }
            foreach (var city in cityMap.Cities)
            {
                string name = null, description = null;
                if (cityStrings != null && cityStrings.TryGetEntry(city.CityId, out var entry))
                {
                    name = entry.Name;
                    description = entry.Description;
                }
                map.RestoreCity(city.CityId, city.RegionId, city.MapPosition, name, description, city.Scale);
            }
            foreach (var gate in cityMap.Gates)
            {
                map.RestoreGate(gate.GateId, gate.RegionId, gate.MapPosition, gate.PairGateId);
            }
            foreach (var road in cityMap.Roads)
            {
                map.RestoreRoad(road.A, road.B, road.Difficulty > 0 ? road.Difficulty : TripTravelSettings.DefaultRoadDifficulty);
            }
            return map;
        }

        private static string FindRegionName(TripRegionStringsTableAsset strings, int regionId)
        {
            if (strings == null) return null;
            foreach (var entry in strings.Entries)
            {
                if (entry.Id == regionId) return entry.Name;
            }
            return null;
        }
    }
}
