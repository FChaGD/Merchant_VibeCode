namespace Game.Core
{
    /// <summary>
    /// 숙소 휴식의 실제 상태 변경 창구(Docs/설계/103번 §4.1). UI는 1일 비용을 읽고 시작·경과·종료만 알린다 -
    /// 날짜 진행·비용 청구·회복은 구현체(system 브랜치)가 맡는다.
    /// </summary>
    public interface IInnRestService
    {
        int CostPerDay { get; }
        void BeginRest(int plannedDays);
        void AdvanceDay();
        void EndRest(int restedDays);
    }
}
