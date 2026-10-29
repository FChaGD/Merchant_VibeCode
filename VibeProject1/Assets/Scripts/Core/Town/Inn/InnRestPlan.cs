using System;

namespace Game.Core
{
    /// <summary>
    /// 숙소 휴식 일수 범위·총 비용·차단 사유(기획 94 4-1·4-2·4-11). 결제는 개인 골드가 모자라면 골드 상자를 꺼내 쓰므로(설계 109 §4.5) 차단 판정도
    /// 같은 기준(지불 가능 여부)을 받는다 - 판정과 실제 결제가 다르면 시작은 됐는데 첫날 결제가 실패한다.
    /// </summary>
    public static class InnRestPlan
    {
        public const int MinDays = 1;
        public const int MaxDays = 30;
        public const string InsufficientGoldText = "골드가 부족합니다.";

        public static int ClampDays(int days) => days < MinDays ? MinDays : days > MaxDays ? MaxDays : days;

        public static int TotalCost(int days, int costPerDay) => days * costPerDay;

        /// <summary>휴식할 수 없는 이유. 없으면 null. 판정 함수가 없으면 막는다(결제 기준을 모름).</summary>
        public static string BlockReason(int days, int costPerDay, Func<int, bool> canAfford)
            => canAfford != null && canAfford(TotalCost(days, costPerDay)) ? null : InsufficientGoldText;
    }
}
