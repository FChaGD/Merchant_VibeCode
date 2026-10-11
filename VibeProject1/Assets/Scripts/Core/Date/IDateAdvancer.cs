namespace Game.Core
{
    /// <summary>게임 날짜 진행(Docs/설계/85번 §3.1). 지금 소비자는 상행 날짜 정산과 디버그뿐이다 - 시간을 쓰는 행동이 늘면 여기 소비자만 추가한다.</summary>
    public interface IDateAdvancer
    {
        void AdvanceDays(int days);
    }
}
