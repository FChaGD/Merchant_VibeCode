using System;

namespace Game.Core
{
    /// <summary>
    /// 재화 조회 전용 계약. 표시만 하는 소비자(재화 계열 UI 표시 영역, Docs/기획/26번 §3.3)는
    /// 이 인터페이스만 의존해 IPlayerCurrencyWallet의 증감 조작에 접근하지 못하게 한다.
    /// </summary>
    public interface IPlayerCurrencyReader
    {
        int CurrentAmount { get; }
        int Capacity { get; }
        event Action<int> OnAmountChanged; // 변경 후 값을 전달 - 폴링 대신 이벤트로 UI 갱신
    }
}
