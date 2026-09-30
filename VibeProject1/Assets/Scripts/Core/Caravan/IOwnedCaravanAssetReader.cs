using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 보유 마차·시설 읽기 전용 계약(Docs/설계/64번 §5). 상단 물류품 저장소는 "보유 마차가 무엇이고 언제 늘었는지"만 알면 되므로
    /// 구매 조작(IOwnedCaravanAssetRoster)을 노출하지 않는다(ISP). 목록은 보유한 순서(시작 보유 → 구매 순)다 - 물류품 팝업의
    /// 마차 순서(기획 63번 §3.2)가 이 순서를 따른다.
    /// </summary>
    public interface IOwnedCaravanAssetReader
    {
        IReadOnlyList<string> GetOwnedIds(FormationUnitKind kind);
        event Action OnOwnedChanged;
    }
}
