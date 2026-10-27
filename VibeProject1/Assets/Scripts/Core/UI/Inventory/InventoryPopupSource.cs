using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 팝업 1종을 만드는 데 필요한 것 - 저장소는 조회(IInventoryReader)와 정리 조작(IInventoryArrangement)을 같은 객체가 구현하지만,
    /// 패널이 요구하는 계약만 드러나도록 따로 담는다.
    /// </summary>
    internal readonly struct InventoryPopupSource
    {
        public readonly InventoryPopupSpec Spec;
        public readonly IInventoryReader Reader;
        public readonly IInventoryArrangement Arrangement;

        public InventoryPopupSource(InventoryPopupSpec spec, IInventoryReader reader, IInventoryArrangement arrangement)
        {
            Spec = spec;
            Reader = reader;
            Arrangement = arrangement;
        }
    }

    /// <summary>
    /// "팝업 스펙 → 저장소" 대응표(Docs/Refactor/2026-10-26_씬전용.md F-2, SE-2). 배선 코드에 종류별 조회 줄·추가 줄이 흩어져 있던 것을 한 곳으로 모았다 -
    /// 팝업을 늘리면 이 표에 한 줄. 인벤토리 데이터 시스템이 아직 Placeholder라 저장소는 선택적이다 - 없으면 그 팝업만 경고 후 빠진다.
    /// </summary>
    internal static class HubInventorySources
    {
        public static List<InventoryPopupSource> Resolve(IDependencyResolver registrar)
        {
            var sources = new List<InventoryPopupSource>();
            Add<ITradeGoodsInventoryRepository>(sources, registrar, InventoryPopupSpecs.TradeGoods);
            Add<IEquipmentInventoryRepository>(sources, registrar, InventoryPopupSpecs.Equipment);
            Add<IConsumableInventoryRepository>(sources, registrar, InventoryPopupSpecs.Consumable);
            Add<IPersonalItemInventoryRepository>(sources, registrar, InventoryPopupSpecs.PersonalItem);
            return sources;
        }

        private static void Add<TRepository>(List<InventoryPopupSource> sources, IDependencyResolver registrar, InventoryPopupSpec spec)
            where TRepository : class, IInventoryRepository, IInventoryArrangement
        {
            if (!registrar.TryResolve<TRepository>(out var repository))
            {
                Debug.LogWarning($"{typeof(TRepository).Name}가 연결되어 있지 않아 '{spec.Title}' 팝업을 등록하지 못했다.");
                return;
            }
            sources.Add(new InventoryPopupSource(spec, repository, repository));
        }
    }
}
