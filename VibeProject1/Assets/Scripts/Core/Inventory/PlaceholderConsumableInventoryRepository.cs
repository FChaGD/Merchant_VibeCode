using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 전투 소모품 인벤토리 - 공통 동작은 PlaceholderFixedGridInventoryRepository(Docs/설계/44번 §4.2). 실제 아이템 데이터
    /// 시스템이 생기면 대체/제거 대상.
    /// </summary>
    public sealed class PlaceholderConsumableInventoryRepository : PlaceholderFixedGridInventoryRepository, IConsumableInventoryRepository
    {
        // 초기 배치 품목은 실제 테이블(Consumable.xlsx)의 "placeholder-consumable-*" 행.
        private static readonly (string itemId, int x, int y)[] PlaceholderInitialItems =
        {
            ("placeholder-consumable-1", 0, 0),
            ("placeholder-consumable-2", 2, 1),
            ("placeholder-consumable-3", 4, 3),
            ("placeholder-consumable-4", 1, 3),
        };

        protected override int Width => 5;
        protected override int Height => 4;
        protected override IReadOnlyList<(string itemId, int x, int y)> InitialItems => PlaceholderInitialItems;

        public override void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<IConsumableInventoryRepository>(this);
    }
}
