namespace Game.Core
{
    /// <summary>
    /// 캐릭터 고용, 마차/시설 구매, 상품 구매 등 재화를 소모/획득시키는 소비자를 위한 계약
    /// (Docs/기획/23번 §3.1 용도). Apply류 저장이 아니라 즉시 반영되는 지갑 모델이다.
    /// </summary>
    public interface IPlayerCurrencyWallet : IPlayerCurrencyReader
    {
        // 상한을 넘는 만큼은 손실된다(23번 §3.1 "상한 초과 시 처리") - 실제로 반영된 양을 반환해
        // 호출자가 손실분을 알 수 있게 한다.
        int Add(int amount);

        // 부족하면 아무 것도 차감하지 않고 false. 소요시간 증가 등 대체 페널티는 두지 않는다(22번 §3.3
        // 소진 처리 원칙과 같은 계열).
        bool TrySpend(int amount);
    }
}
