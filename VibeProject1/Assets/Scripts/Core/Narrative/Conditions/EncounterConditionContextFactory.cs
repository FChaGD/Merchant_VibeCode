using System;

namespace Game.Core
{
    /// <summary>
    /// 판정 문맥 만들기(설계 87 §5.1). 대화(88)·이벤트(89) 진행자가 판정 순간에 부른다. 지역은 도시 → 지역 조회 함수로 받는다 - 지도 저장소 전체를
    /// 넘기지 않아 테스트에서 함수 하나로 대신할 수 있다. 구간이 없으면 장소 값은 0(장소 조건은 HasLeg로 거짓).
    /// </summary>
    public static class EncounterConditionContextFactory
    {
        public static EncounterConditionContext Create(GameDate today, bool hasLeg, TripLeg leg, Func<int, int> regionOfCity, float progress, IEncounterHistoryReader history,
            IQuestEncounterGate quests = null)
        {
            if (!hasLeg) return new EncounterConditionContext(today, false, 0, 0, 0, 0, progress, history, quests);
            return new EncounterConditionContext(today, true, leg.FromCityId, leg.ToCityId,
                regionOfCity(leg.FromCityId), regionOfCity(leg.ToCityId), progress, history, quests);
        }

        /// <summary>숙소 휴식 문맥(설계 109 §3.2) - 구간이 없어 상행 장소 조건은 거짓, 체류 도시가 없으면(Id 0 이하) `town`·`region`도 거짓.</summary>
        public static EncounterConditionContext CreateForTown(GameDate today, int cityId, Func<int, int> regionOfCity, IEncounterHistoryReader history)
        {
            var hasTown = cityId > 0;
            return new EncounterConditionContext(today, false, 0, 0, 0, 0, 0f, history, null, hasTown, hasTown ? cityId : 0, hasTown ? regionOfCity(cityId) : 0);
        }
    }
}
