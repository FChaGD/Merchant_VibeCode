namespace Game.Core
{
    /// <summary>
    /// 시설 화면 버튼의 비활성 사유 문구(Docs/Refactor/2026-10-26_공통.md D-1). 사유는 버튼 툴팁으로 보인다(상행 시작·숙소 휴식과 같은 방식) -
    /// 버튼 활성과 툴팁 문구가 같은 계산에서 나오도록 판정 → 문구를 한곳에 모았다. null = 사유 없음(버튼 활성, 툴팁 표시 안 함).
    /// </summary>
    public static class FacilityBlockReasons
    {
        public const string SoldOut = "품절";
        public const string InsufficientFunds = "재화가 부족합니다.";
        public const string NoSpace = "상단 물류품에 놓을 공간이 없습니다. 마차 공간을 정리해주세요.";
        public const string AlreadyHired = "이미 고용한 용병입니다.";
        public const string HasCargo = "화물이 실린 마차는 매각할 수 없습니다.";
        public const string LastWagon = "마지막 마차는 매각할 수 없습니다.";
        public const string RepairNotNeeded = "수리할 필요 없음";
        public const string RepairInsufficientGold = "골드 부족";
        // 서비스가 없을 때(인스톨러 미실행) - 수리·매각 모두 사유를 보인다(사용자 결정 2026-10-26).
        public const string RepairUnavailable = "수리 불가능 개체";
        public const string SaleUnavailable = "매각 불가능한 개체";

        public static string For(ShopPurchaseCheck check) => check switch
        {
            ShopPurchaseCheck.SoldOut => SoldOut,
            ShopPurchaseCheck.InsufficientFunds => InsufficientFunds,
            ShopPurchaseCheck.NoSpace => NoSpace,
            _ => null,
        };

        public static string For(MercenaryHireCheck check) => check switch
        {
            MercenaryHireCheck.InsufficientFunds => InsufficientFunds,
            MercenaryHireCheck.AlreadyHired => AlreadyHired,
            _ => null,
        };

        public static string For(CaravanAssetPurchaseCheck check) => check switch
        {
            CaravanAssetPurchaseCheck.SoldOut => SoldOut,
            CaravanAssetPurchaseCheck.InsufficientFunds => InsufficientFunds,
            _ => null,
        };

        // 미보유(NotOwned)는 사유 없이 막는다 - 보유 개체를 보는 화면에서만 매각하므로 실제로는 사라진 직후의 순간뿐이다.
        public static string For(CaravanAssetSaleCheck check) => check switch
        {
            CaravanAssetSaleCheck.HasCargo => HasCargo,
            CaravanAssetSaleCheck.LastWagon => LastWagon,
            _ => null,
        };

        // 수리 서비스가 없으면(null) RepairUnavailable(수리비 칸은 "값 없음").
        public static string ForRepair(CaravanAssetRepairCheck? check) => check switch
        {
            CaravanAssetRepairCheck.NotDamaged => RepairNotNeeded,
            CaravanAssetRepairCheck.InsufficientFunds => RepairInsufficientGold,
            null => RepairUnavailable,
            _ => null,
        };

        // 손상 없음은 사유 없이 비활성(기획 90번 §4-16).
        public static string ForRepairAll(CaravanAssetRepairCheck check) => check == CaravanAssetRepairCheck.InsufficientFunds ? RepairInsufficientGold : null;
    }
}
