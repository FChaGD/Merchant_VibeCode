namespace Game.Core
{
    /// <summary>
    /// 마구간 매각 판정과 매각가(기획 94 4-19·4-20, Docs/설계/103번 §4.2·104번 §2). 판정 순서는 미보유 → 화물 → 마지막 마차.
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

        /// <summary>구매가 × 비율 × 내구도 비율(0~1로 자름), 내림(Docs/설계/104번 §2).</summary>
        public static int SalePrice(int purchasePrice, float ratio, float durabilityRatio = 1f)
        {
            var durability = System.Math.Clamp((double)durabilityRatio, 0d, 1d);
            return (int)System.Math.Floor(purchasePrice * (double)ratio * durability);
        }

        /// <summary>현재 ÷ 최대. 기록이 없거나 최대가 0 이하면 1(감가 없음).</summary>
        public static float DurabilityRatio(bool hasRecord, float current, int max) => hasRecord && max > 0 ? current / max : 1f;
    }
}
