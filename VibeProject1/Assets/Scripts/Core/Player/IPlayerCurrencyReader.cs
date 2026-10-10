using System;

namespace Game.Core
{
    /// <summary>
    /// 개인 골드 조회 전용 계약. 표시만 하는 소비자는 이 인터페이스만 의존해 증감 조작에 접근하지 못하게 한다.
    /// 적재 골드·소지 가능량은 이 계약이 아니라 골드 보유 조회(IGoldHoldingsReader, 설계 83번 §3.3)가 담당한다.
    /// </summary>
    public interface IPlayerCurrencyReader
    {
        int CurrentAmount { get; }
        // 개인 소유 가능량(기획 82번 A1). 상한이 아니라 "출발 시 이 값을 넘는 개인 골드는 버려진다"는 기준값이다.
        int PersonalLimit { get; }
        event Action<int> OnAmountChanged; // 변경 후 값을 전달 - 폴링 대신 이벤트로 UI 갱신
    }
}
