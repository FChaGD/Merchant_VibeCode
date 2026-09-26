using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 상단주 개인 물품 인벤토리 - 공통 동작은 PlaceholderFixedGridInventoryRepository(Docs/설계/44번 §4.2). 실제 아이템 데이터
    /// 시스템이 생기면 대체/제거 대상.
    /// </summary>
    public sealed class PlaceholderPersonalItemInventoryRepository : PlaceholderFixedGridInventoryRepository, IPersonalItemInventoryRepository
    {
        // 초기 배치 품목은 실제 테이블(PersonalItem.xlsx)의 "placeholder-personal-*" 행.
        private static readonly (string itemId, int x, int y)[] PlaceholderInitialItems =
        {
            ("placeholder-personal-1", 0, 0),
            ("placeholder-personal-2", 2, 0),
            ("placeholder-personal-3", 1, 1),
            ("placeholder-personal-4", 2, 2),
        };

        protected override int Width => 3;
        protected override int Height => 3;
        protected override IReadOnlyList<(string itemId, int x, int y)> InitialItems => PlaceholderInitialItems;

        public override void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<IPersonalItemInventoryRepository>(this);
    }
}
