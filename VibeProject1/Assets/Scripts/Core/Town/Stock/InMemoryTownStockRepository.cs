using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마을 재고의 Bootstrap 상주 저장소(Docs/설계/81번 §3.4). 로직은 TownStockLedger에 있고 이 클래스는 DI·테이블 배선·규모 경고만 맡는다.
    /// 읽기·차감 계약을 각각 등록한다 - 상위 타입 등록으로는 보조 계약이 조회되지 않는다.
    /// </summary>
    public class InMemoryTownStockRepository : MonoBehaviour, ITownStockReader, ITownStockConsumer, IManagedComponent
    {
        [SerializeField] private TownStockTableAsset stockTable;

        private TownStockLedger ledger = new(null, null);
        private ITownFacilityAvailabilityReader facilityAvailability;

        public event Action OnStockChanged;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ITownStockReader>(this);
            registrar.Register<ITownStockConsumer>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            facilityAvailability = null;
            registrar?.TryResolve(out facilityAvailability); // 선택적 - 없으면 규모 검사 없이 전부 판매
            if (stockTable == null)
            {
                Debug.LogWarning($"{nameof(InMemoryTownStockRepository)}: 재고 테이블이 배선되지 않아 모든 마을이 빈 상점이 된다(Play 후 Tools > Game > Build Bootstrap Scene).");
            }
            var entries = stockTable != null ? stockTable.Entries : Array.Empty<TownStockEntry>();
            ledger = new TownStockLedger(entries, (cityId, category) => IsSellable(entries, cityId, category));
        }

        public IReadOnlyList<TownStockLine> GetLines(int cityId, TownStockCategory category) => ledger.GetLines(cityId, category);

        public bool TryConsume(int cityId, TownStockCategory category, string itemId)
        {
            if (!ledger.TryConsume(cityId, category, itemId)) return false;
            OnStockChanged?.Invoke();
            return true;
        }

        // 행이 있는데 시설이 없으면 테이블 실수라 알린다. 행이 없는 조합은 조용히 넘긴다(경고 남발 방지).
        private bool IsSellable(IReadOnlyList<TownStockEntry> entries, int cityId, TownStockCategory category)
        {
            if (facilityAvailability == null) return true;
            var facilityId = TownStockCategories.FacilityOf(category);
            if (facilityAvailability.IsFacilityAvailable(cityId, facilityId)) return true;

            foreach (var entry in entries)
            {
                if (entry.CityId == cityId && entry.Category == category)
                {
                    Debug.LogWarning($"{nameof(InMemoryTownStockRepository)}: 마을 {cityId}에는 '{facilityId}' 시설이 없어 {category} 재고 행을 무시한다(TownStock.xlsx 또는 마을 규모 확인).");
                    break;
                }
            }
            return false;
        }
    }
}
