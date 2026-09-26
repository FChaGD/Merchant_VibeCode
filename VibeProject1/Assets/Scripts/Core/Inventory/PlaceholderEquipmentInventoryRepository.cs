using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 장비 인벤토리 - 공통 동작은 PlaceholderFixedGridInventoryRepository(Docs/설계/44번 §4.2). 실제 아이템 데이터
    /// 시스템이 생기면 대체/제거 대상.
    /// </summary>
    public sealed class PlaceholderEquipmentInventoryRepository : PlaceholderFixedGridInventoryRepository, IEquipmentInventoryRepository
    {
        // 초기 배치 품목은 실제 테이블(Equipment.xlsx)의 "placeholder-equipment-*" 행.
        private static readonly (string itemId, int x, int y)[] PlaceholderInitialItems =
        {
            ("placeholder-equipment-1", 0, 0),
            ("placeholder-equipment-2", 3, 1),
            ("placeholder-equipment-3", 5, 3),
            ("placeholder-equipment-4", 1, 3),
        };

        protected override int Width => 6;
        protected override int Height => 4;
        protected override IReadOnlyList<(string itemId, int x, int y)> InitialItems => PlaceholderInitialItems;

        public override void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<IEquipmentInventoryRepository>(this);
    }
}
