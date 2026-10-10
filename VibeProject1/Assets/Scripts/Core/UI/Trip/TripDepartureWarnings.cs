using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 출발 확인 경고 목록(Docs/설계/83번 §6.3). 경고마다 독립 판정하고(기획 82번 E4) 표시 순서는 배치 유닛 0 → 골드다(사용자 결정 2026-10-10) -
    /// 유닛 0은 출발 의사 자체를 다시 묻는 경고라 먼저, 골드 버림은 비가역 손실이라 출발 직전 마지막에 둔다.
    /// 개인 소유 가능량은 호출 시점 값을 받는다 - 값이 바뀌어도 문구가 따라가게 상수로 두지 않는다.
    /// </summary>
    public static class TripDepartureWarnings
    {
        public const string EmptyFormation = "상단에 배치된 유닛이 없습니다. 이대로 습격받으면 위험합니다. 그래도 출발하시겠습니까?";

        public static string FormatGold(int personalLimit, int excessGold)
            => $"개인 소유 가능량 {personalLimit:N0}을 넘는 {excessGold:N0}골드가 버려집니다. 마차에 적재하거나 은행에 보관하지 않은 골드는 버려집니다. 그래도 출발하시겠습니까?";

        public static List<string> Collect(bool hasPlacedUnit, int excessGold, int personalLimit)
        {
            var warnings = new List<string>(2);
            if (!hasPlacedUnit) warnings.Add(EmptyFormation);
            if (excessGold > 0) warnings.Add(FormatGold(personalLimit, excessGold));
            return warnings;
        }
    }
}
