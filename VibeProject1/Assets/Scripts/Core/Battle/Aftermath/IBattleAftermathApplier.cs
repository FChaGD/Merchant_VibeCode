namespace Game.Core
{
    /// <summary>
    /// 전투 결과를 화물·보유 마차·대열·상행 시간에 한 번에 반영한다(설계 79번 §5). 결과 팝업 전에 호출해야 팝업이 반영 결과를 보여 준다
    /// (기획 78번 §4-5). 승리면 consequence = null. 포로·사망이면 아무것도 하지 않고 default를 돌려준다.
    /// </summary>
    public interface IBattleAftermathApplier
    {
        BattleAftermathSummary Apply(BattleResult result, DefeatConsequence? consequence);
    }
}
