namespace Game.Core
{
    /// <summary>
    /// 숙소 휴식 일수 범위·총 비용·차단 사유(기획 94 4-1·4-2·4-11). 비용 부족은 진행 중이 아니라 확정 단계에서만 막으므로
    /// 진행 중 부족은 생기지 않는다(진행 중에는 HUD 입력도 막아 개인 골드가 바뀌지 않는다, 설계 103번 §3).
    /// </summary>
    public static class InnRestPlan
    {
        public const int MinDays = 1;
        public const int MaxDays = 30;
        // 스펙에 문구가 없어 제작 시 정했다(2026-10-21) - 비용은 개인 골드에서만 낸다(기획 94 §3.1).
        public const string InsufficientGoldText = "개인 골드가 부족합니다.";

        public static int ClampDays(int days) => days < MinDays ? MinDays : days > MaxDays ? MaxDays : days;

        public static int TotalCost(int days, int costPerDay) => days * costPerDay;

        /// <summary>휴식할 수 없는 이유. 없으면 null - 총 비용이 개인 골드와 같으면 허용한다.</summary>
        public static string BlockReason(int days, int costPerDay, int personalGold) => TotalCost(days, costPerDay) > personalGold ? InsufficientGoldText : null;
    }
}
