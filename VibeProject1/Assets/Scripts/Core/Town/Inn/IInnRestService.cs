namespace Game.Core
{
    /// <summary>하루 처리 결과(설계 109 §4.3). 결제 부족이면 그날은 성립하지 않는다 - 날짜·회복 없음.</summary>
    public enum RestDaySettlement { Paid, InsufficientGold }

    /// <summary>
    /// 숙소 휴식의 실제 상태 변경 창구(Docs/설계/103번 §4.1, 109번 §4.3). UI는 1일 비용을 읽고 시작·하루 처리·종료만 알린다 -
    /// 결제·날짜 진행·회복은 구현체가 맡는다.
    /// </summary>
    public interface IInnRestService
    {
        int CostPerDay { get; }
        void BeginRest(int plannedDays);
        /// <summary>휴식 중이고 예정 일수가 남았다 - 하루 처리를 부를 수 있는 유일한 조건.</summary>
        bool CanSettleDay { get; }
        /// <summary>
        /// 그날 결제(개인 → 골드 상자). 성공 시 날짜 +1·회복. 실패 시 상태 변경 없음. CanSettleDay가 거짓일 때 부르면 호출 순서 오류라
        /// InvalidOperationException - 결과 값으로 돌려주면 "골드 부족"과 섞인다.
        /// </summary>
        RestDaySettlement SettleDay();
        void EndRest(int restedDays);
    }
}
