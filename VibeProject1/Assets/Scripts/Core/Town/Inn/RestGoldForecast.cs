namespace Game.Core
{
    /// <summary>
    /// 휴식 진행 화면의 예상 남은 골드(설계 109 §6.3). 시작 시점 고정값이 아니라 지금 잔액에서 남은 비용을 다시 계산한다 - 이벤트 골드 효과가 중간에 잔액을
    /// 바꾼다. 결제와 같은 순서(개인 → 골드 상자 인출)라 인출 상자 수는 지출 계약의 미리보기로 받는다. 지불 불가면 미리보기가 비어 개인 골드가 음수가 된다.
    /// </summary>
    public static class RestGoldForecast
    {
        public static (int personalAfter, int loadedAfter, bool insufficient) Compute(int personalGold, int loadedGold, int remainingCost, int withdrawnBoxes, int boxValue)
        {
            var withdrawnGold = withdrawnBoxes * boxValue;
            var personalAfter = personalGold + withdrawnGold - remainingCost;
            return (personalAfter, loadedGold - withdrawnGold, personalAfter < 0);
        }
    }
}
