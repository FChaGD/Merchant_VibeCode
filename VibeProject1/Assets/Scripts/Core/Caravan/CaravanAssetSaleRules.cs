namespace Game.Core
{
    /// <summary>
    /// 마구간 매각 판정과 매각가(기획 94 4-19·4-20, Docs/설계/103번 §4.2). 판정 순서는 미보유 → 화물 → 마지막 마차.
    /// 마지막 마차 제한은 마차에만 적용한다 - 마차가 0대가 되면 상행을 떠날 수 없기 때문이고, 시설은 수와 무관하다.
    /// </summary>
    public static class CaravanAssetSaleRules
    {
        public static CaravanAssetSaleCheck Evaluate(bool owned, bool hasCargo, FormationUnitKind kind, int ownedWagonCount)
        {
            if (!owned) return CaravanAssetSaleCheck.NotOwned;
            if (hasCargo) return CaravanAssetSaleCheck.HasCargo;
            if (kind == FormationUnitKind.Wagon && ownedWagonCount <= 1) return CaravanAssetSaleCheck.LastWagon;
            return CaravanAssetSaleCheck.Available;
        }

        /// <summary>구매가 × 비율, 내림.</summary>
        public static int SalePrice(int purchasePrice, float ratio) => (int)System.Math.Floor(purchasePrice * (double)ratio);
    }
}
