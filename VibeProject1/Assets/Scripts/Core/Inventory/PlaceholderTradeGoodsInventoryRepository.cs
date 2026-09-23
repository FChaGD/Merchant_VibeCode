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
    /// 골드 상자(재화 500=1×1칸, 기획 31번 §3.4)는 아이템 카탈로그가 없는 지금 유일하게 존재가
    /// 확정된 특수 아이템이라 이 저장소가 직접 소유하고 IPlayerCurrencyWallet과 연동한다.
    /// </summary>
    public class PlaceholderTradeGoodsInventoryRepository : MonoBehaviour, ITradeGoodsInventoryRepository, IManagedComponent
    {
        private const int PlaceholderCellsPerWagon = 8;
        private const int MaxGridColumns = 10; // 총 칸수를 가로 10칸 기준으로 접어 세로를 늘리는 임시 배치 규칙
        private const int GoldBoxCurrencyValue = 500;

        [SerializeField] private Sprite goldBoxIcon;

        private ICaravanRosterProvider caravanRosterProvider;
        private IPlayerCurrencyWallet currencyWallet;
        private IInventoryItemDefinition goldBoxDefinition;
        private InventoryGrid grid;

        public int GridWidth => grid.Width;
        public int GridHeight => grid.Height;
        public IReadOnlyCollection<InventoryItemInstance> Items => grid.Items;
        public event Action OnChanged;

        // 전당포 등 향후 변환 트리거(범위 밖, 31번 §4)가 골드 상자를 직접 놓을 수 있도록 정의 자체를 노출.
        public IInventoryItemDefinition GoldBoxDefinition => goldBoxDefinition;

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<ITradeGoodsInventoryRepository>(this);

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            caravanRosterProvider = registrar.Resolve<ICaravanRosterProvider>();
            registrar.TryResolve(out currencyWallet); // 선택적 의존성 - 없으면 골드 상자 배치만 불가.
            goldBoxDefinition = new GoldBoxItemDefinition(goldBoxIcon);

            var wagonCount = caravanRosterProvider.GetRoster().Count(unit => unit.Kind == FormationUnitKind.Wagon);
            var totalCells = wagonCount * PlaceholderCellsPerWagon;
            var width = Mathf.Min(totalCells, MaxGridColumns);
            var height = width == 0 ? 0 : Mathf.CeilToInt(totalCells / (float)width);
            grid = new InventoryGrid(width, height);
        }

        public bool TryGetItemAt(GridPosition position, out InventoryItemInstance item) => grid.TryGetAt(position, out item);

        public bool TryPlaceItem(IInventoryItemDefinition definition, GridPosition position, out InventoryItemInstance placed)
        {
            placed = default;
            var isGoldBox = ReferenceEquals(definition, goldBoxDefinition);

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

        public bool RemoveItem(string instanceId)
        {
            var isGoldBox = grid.TryFind(instanceId, out var item) && ReferenceEquals(item.Definition, goldBoxDefinition);
            if (!grid.Remove(instanceId)) return false;

            // 골드 상자 제거=환급. 역변환 UI 자체는 범위 밖이지만(31번 §4), 최소한 인벤토리에서
            // 사라지면 재화가 증발하지 않게는 한다.
            if (isGoldBox) currencyWallet?.Add(GoldBoxCurrencyValue);

            OnChanged?.Invoke();
            return true;
        }
    }
}
