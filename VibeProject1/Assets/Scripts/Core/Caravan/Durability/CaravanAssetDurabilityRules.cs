using System;

namespace Game.Core
{
    /// <summary>
    /// 마차·시설 내구도의 표시·손상·수리비 규칙(Docs/기획/90번 §3-9·§4-4·§4-15, 설계 91 §3.3). 마구간 목록·개별 정보·디버그·이벤트 결과 줄이
    /// 같은 숫자를 보여야 해서 한 곳에 둔다. 손상 판정을 표시값 기준으로 하는 이유: "200/200"으로 보이는 개체에 수리비가 붙거나 색이 바뀌지 않게.
    /// </summary>
    public static class CaravanAssetDurabilityRules
    {
        // 수리비 계수(기획 90번 §3-9) - 종류별 계수 열이 생기면 CoefficientOf만 바꾼다.
        public const float DefaultRepairCoefficient = 0.5f;
        // 부동소수 오차(예: 50.0000001)가 올림에서 1골드로 커지지 않게 이 자릿수에서 먼저 반올림한다.
        private const int RoundingDigits = 4;

        /// <summary>표시값 - 정수 올림(0.4 → 1). 살아 있는 개체가 0으로 보이지 않게 한다(기획 90번 §4-4).</summary>
        public static int Display(float value) => (int)Math.Ceiling(Math.Round((double)value, RoundingDigits));

        public static bool IsDamaged(float current, int max) => Display(current) < max;

        public static float CoefficientOf(CaravanAssetProfile profile) => DefaultRepairCoefficient;

        /// <summary>개별 수리비 = 올림(가격 × 깎인 비율 × 계수). 손상 없으면 0(기획 90번 §3-9·§4-15).</summary>
        public static int RepairCost(int price, float current, int max, float coefficient)
        {
            if (max <= 0 || !IsDamaged(current, max)) return 0;
            var missing = Math.Min(Math.Max((double)max - current, 0d), max);
            var raw = price * (missing / max) * coefficient;
            return (int)Math.Ceiling(Math.Round(raw, RoundingDigits));
        }
    }
}
