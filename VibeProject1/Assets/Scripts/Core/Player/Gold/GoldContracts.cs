using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 골드 보유 조회(Docs/설계/83번 §3.3) - HUD·변환 모달·상행 준비 UI용. 개인 골드는 지갑, 적재 골드·빈 칸은 교역품 저장소에서 온다.
    /// </summary>
    public interface IGoldHoldingsReader
    {
        int PersonalGold { get; }
        int PersonalLimit { get; }
        int LoadedGold { get; }
        int FreeCells { get; }
        int FreeSpaceGold { get; }
        int OwnedGold { get; }
        int CarryCapacity { get; }
        /// <summary>출발 시 버려질 골드 = max(0, 개인 골드 − 개인 소유 가능량).</summary>
        int ExcessGold { get; }
        /// <summary>지갑·교역품 저장소 중 어느 쪽이 바뀌어도 발생한다. 지출·변환처럼 여러 번 바꾸는 연산은 끝난 뒤 한 번만 발생한다.</summary>
        event Action Changed;
    }

    /// <summary>
    /// 골드 지출(Docs/설계/83번 §4.3) - 구매·고용 서비스용. 개인 골드를 먼저 쓰고 부족하면 골드 상자를 자동 인출한다.
    /// 판정과 확정이 같은 인출 대상을 쓰도록 미리보기를 함께 둔다(무역품 구매의 공간 판정, §5.2). 버튼 갱신을 위해 변경 이벤트도 노출한다.
    /// </summary>
    public interface IGoldSpender
    {
        int OwnedGold { get; }
        bool CanAfford(int amount);
        /// <summary>이 금액을 지출하면 인출될 골드 아이템 Id 목록(상태 변경 없음). 인출이 필요 없거나 지불 불가면 빈 목록.</summary>
        IReadOnlyList<string> PreviewWithdrawal(int amount);
        bool TrySpend(int amount);
        /// <summary>지출 후 지급 실패 롤백 - 개인 골드로만 돌려준다(인출한 상자는 복원하지 않음).</summary>
        void Refund(int amount);
        /// <summary>IGoldHoldingsReader.Changed와 같은 이벤트다 - 교역품 저장소 변경도 포함하므로 소비자가 저장소를 따로 구독할 필요가 없다.</summary>
        event Action Changed;
    }

    /// <summary>개인 골드 → 골드 상자 변환(Docs/설계/83번 §4.4) - 변환 모달용.</summary>
    public interface IGoldBoxConverter
    {
        int GoldBoxValue { get; }
        int MaxConvertibleBoxes { get; }
        /// <summary>"초과분 전부" 개수 = min(⌈(개인 골드 − 개인 소유 가능량) ÷ 가치⌉, 최대 변환 개수).</summary>
        int ExcessBoxes { get; }
        /// <summary>요청 개수를 최대치로 자른 뒤 변환하고 실제 변환 개수를 반환한다.</summary>
        int Convert(int boxCount);
    }

    /// <summary>출발 확정 시 초과분 버림(Docs/설계/83번 §4.5) - 상행 준비 UI용.</summary>
    public interface IGoldDepartureSettlement
    {
        /// <summary>개인 골드를 개인 소유 가능량으로 줄이고 버린 금액을 반환한다. 적재 골드는 건드리지 않는다.</summary>
        int DiscardExcess();
    }
}
