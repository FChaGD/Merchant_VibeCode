using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마구간 구매 후보(Docs/설계/81번 §3.5). 마을 재고의 마차 → 시설 행을 남은 수량만큼 펼친다 - 화면이 재고 1대를 한 행으로 보여 주고
    /// 산 행이 사라지는 규칙(기획 80번 §3-4)을 목록 길이로 그대로 표현하기 위함이다. 시설 Id가 마구간이 아니면 빈 목록.
    /// </summary>
    public class CaravanAssetCandidateProvider : MonoBehaviour, ICaravanAssetCandidateReader, IManagedComponent
    {
        private static readonly TownStockCategory[] Order = { TownStockCategory.Wagon, TownStockCategory.Facility };

        private ITownStockReader stockReader;
        private ICaravanAssetCatalogReader catalog;

        public event Action OnCandidatesChanged;

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<ICaravanAssetCandidateReader>(this);

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            ITownStockReader reader = null;
            ICaravanAssetCatalogReader assetCatalog = null;
            registrar?.TryResolve(out reader);
            registrar?.TryResolve(out assetCatalog);
            Bind(reader, assetCatalog);
        }

        // 테스트가 DI 없이 재고·카탈로그를 연결하는 진입점이기도 하다.
        public void Bind(ITownStockReader reader, ICaravanAssetCatalogReader assetCatalog)
        {
            if (stockReader != null) stockReader.OnStockChanged -= ForwardChanged;
            stockReader = reader;
            catalog = assetCatalog;
            if (stockReader != null) stockReader.OnStockChanged += ForwardChanged;
        }

        private void OnDestroy()
        {
            if (stockReader != null) stockReader.OnStockChanged -= ForwardChanged;
        }

        public IReadOnlyList<CaravanAssetProfile> GetCandidates(int cityId, string facilityId)
        {
            if (stockReader == null || catalog == null || facilityId != TownFacilityIds.Stable) return Array.Empty<CaravanAssetProfile>();

            var candidates = new List<CaravanAssetProfile>();
            foreach (var category in Order)
            {
                foreach (var line in stockReader.GetLines(cityId, category))
                {
                    if (!catalog.TryGet(line.ItemId, out var profile)) continue;
                    for (var i = 0; i < line.Remaining; i++) candidates.Add(profile);
                }
            }
            return candidates;
        }

        private void ForwardChanged() => OnCandidatesChanged?.Invoke();
    }
}
