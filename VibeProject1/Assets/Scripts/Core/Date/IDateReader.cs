using System;

namespace Game.Core
{
    /// <summary>게임 날짜 조회(Docs/설계/85번 §3.1). 진행 계약과 나눈 이유: 표시만 하는 소비자에게 진행 메서드를 노출하지 않는다(ISP).</summary>
    public interface IDateReader
    {
        GameDate Today { get; }
        event Action Changed;
    }
}
