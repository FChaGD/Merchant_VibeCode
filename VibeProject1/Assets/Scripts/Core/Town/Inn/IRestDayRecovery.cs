namespace Game.Core
{
    /// <summary>휴식 1일 성립 시 회복(기획 108 §3.1 - 지략·행정력 각 3). 자원 구현 때 구현체를 등록한다 - 지금은 없음(설계 109 §12).</summary>
    public interface IRestDayRecovery
    {
        void RecoverOneDay();
    }
}
