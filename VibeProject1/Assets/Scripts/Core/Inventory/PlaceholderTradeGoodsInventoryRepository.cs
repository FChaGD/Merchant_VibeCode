using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 그리드 총 크기가 고정이 아니라 보유 마차 수 × 마차당 칸수로 계산된다(Docs/기획/31번 §3.3,
    /// 설계 32번 §4.2). 마차 자체의 스탯 시스템이 없어 ICaravanRosterProvider의
    /// FormationUnitKind.Wagon 재고 수(기획 11번 - 카테고리당 5개, "재고 수량" 의미)를 보유 마차 수
    /// Placeholder로 쓴다 - 새 마차 데이터 소스를 따로 만들지 않는다. PlaceholderCellsPerWagon도
    /// 마차 스탯 기획 전까지의 잠정 상수 - 실제 마차 스탯 시스템이 생기면 이 클래스를 통째로 대체한다.
    ///
    /// 골드 상자(재화 500=1×1칸, 기획 31번 §3.4)는 이제 아이템 테이블의 평범한 한 행("gold-box" Id)이다
    /// (Docs/기획/33번 §3.4, 설계 35번 §7.1) - 데이터(아이콘/크기/표시명)는 다른 아이템과 동일하게
    /// 테이블에서 오고, 지갑 연동이라는 행동만 이 저장소가 Id 문자열로 식별해 특수 처리한다.
    /// </summary>
    public class PlaceholderTradeGoodsInventoryRepository : MonoBehaviour, ITradeGoodsInventoryRepository, IManagedComponent
    {
        private const int PlaceholderCellsPerWagon = 8;
        private const int MaxGridColumns = 10; // 총 칸수를 가로 10칸 기준으로 접어 세로를 늘리는 임시 배치 규칙
        private const int GoldBoxCurrencyValue = 500;
        private const string GoldBoxItemId = "gold-box";

        // 상단 물류품 팝업의 드래그/교환/회전 검증용 초기 배치(Docs/기획/39번 §3.5/§4.4). 품목은 실제
        // 아이템 테이블(TradeGoods.xlsx)의 "placeholder-*" 행 - 실제 아이템 획득 시스템이 생기면 이 목록과
        // 테이블의 placeholder 행을 함께 제거한다. 골드 상자는 지갑 차감이 걸려 있어 넣지 않는다.
        private static readonly (string itemId, int x, int y)[] PlaceholderInitialItems =
        {
            ("placeholder-1x1", 0, 0),
            ("placeholder-1x1", 0, 1),
            ("placeholder-2x1", 1, 0),
            ("placeholder-1x2", 3, 0),
            ("placeholder-2x2", 4, 0),
        };

        [SerializeField] private ItemDefinitionTableAsset itemTable;
        [SerializeField] private ItemStringTableAsset itemStrings;

        private ICaravanRosterProvider caravanRosterProvider;
        private IPlayerCurrencyWallet currencyWallet;
        private InventoryGrid grid;
        private TableItemCatalog catalog;

        public int GridWidth => grid.Width;
        public int GridHeight => grid.Height;
        public IReadOnlyCollection<InventoryItemInstance> Items => grid.Items;
        public IReadOnlyList<InventoryItemInstance> StagedItems => grid.StagedItems;
        public event Action OnChanged;

        public IReadOnlyList<IInventoryItemDefinition> CatalogItems => catalog.CatalogItems;
        public bool TryGetDefinition(string id, out IInventoryItemDefinition definition) => catalog.TryGetDefinition(id, out definition);

        // 전당포 등 향후 변환 트리거(범위 밖, 31번 §4)가 골드 상자를 직접 놓을 수 있도록 정의 자체를 노출.
        // 테이블에 "gold-box" 행이 아직 없으면(초기 콘텐츠 미기재) false를 그대로 돌려준다.
        public bool TryGetGoldBoxDefinition(out IInventoryItemDefinition definition) => catalog.TryGetDefinition(GoldBoxItemId, out definition);

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<ITradeGoodsInventoryRepository>(this);

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            caravanRosterProvider = registrar.Resolve<ICaravanRosterProvider>();
            registrar.TryResolve(out currencyWallet); // 선택적 의존성 - 없으면 골드 상자 배치만 불가.
            catalog = new TableItemCatalog(itemTable, itemStrings);

            var wagonCount = caravanRosterProvider.GetRoster().Count(unit => unit.Kind == FormationUnitKind.Wagon);
            var totalCells = wagonCount * PlaceholderCellsPerWagon;
            var width = Mathf.Min(totalCells, MaxGridColumns);
            var height = width == 0 ? 0 : Mathf.CeilToInt(totalCells / (float)width);
            grid = new InventoryGrid(width, height);
            PlaceholderInventorySeeder.PlaceAll(grid, catalog, PlaceholderInitialItems, nameof(PlaceholderTradeGoodsInventoryRepository));
        }

        public bool TryGetItemAt(GridPosition position, out InventoryItemInstance item) => grid.TryGetAt(position, out item);

        public bool TryPlaceItem(IInventoryItemDefinition definition, GridPosition position, out InventoryItemInstance placed)
        {
            placed = default;
            var isGoldBox = definition != null && definition.Id == GoldBoxItemId;

            // 골드 상자는 재화 지갑에서 먼저 차감해야 성립한다 - 아이템만 놓이고 재화가 안 줄면 재화가
            // 복제되는 버그가 된다(기획 31번 §3.4).
            if (isGoldBox && (currencyWallet == null || !currencyWallet.TrySpend(GoldBoxCurrencyValue)))
            {
                return false;
            }

            if (!grid.TryPlace(definition, position, out placed))
            {
                if (isGoldBox) currencyWallet.Add(GoldBoxCurrencyValue); // 배치 실패 롤백
                return false;
            }

            OnChanged?.Invoke();
            return true;
        }

        // 재배치는 인벤토리 유입/유출이 아니므로 골드 상자도 지갑을 건드리지 않는다(Docs/설계/40번 §3.2).
        public bool TryApplyPlacements(IReadOnlyList<ItemPlacement> placements)
        {
            if (!grid.TryApplyPlacements(placements)) return false;

            OnChanged?.Invoke();
            return true;
        }

        public bool CanApplyPlacements(IReadOnlyList<ItemPlacement> placements) => grid.CanApplyPlacements(placements);

        public bool RemoveItem(string instanceId)
        {
            var isGoldBox = grid.TryFind(instanceId, out var item) && item.Definition.Id == GoldBoxItemId;
            if (!grid.Remove(instanceId)) return false;

            // 골드 상자 제거=환급. 역변환 UI 자체는 범위 밖이지만(31번 §4), 최소한 인벤토리에서
            // 사라지면 재화가 증발하지 않게는 한다.
            if (isGoldBox) currencyWallet?.Add(GoldBoxCurrencyValue);

            OnChanged?.Invoke();
            return true;
        }
    }
}
