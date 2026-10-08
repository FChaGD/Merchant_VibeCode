using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 보유 마차·시설 읽기 전용 계약(Docs/설계/64번 §5). 상단 물류품 저장소는 "보유 마차가 무엇이고 언제 늘었는지"만 알면 되므로
    /// 구매 조작(IOwnedCaravanAssetRoster)을 노출하지 않는다(ISP). 목록은 보유한 순서(시작 보유 → 구매 순)다 - 물류품 팝업의
    /// 마차 순서(기획 63번 §3.2)가 이 순서를 따른다. 목록의 Id는 개체 Id(발급 Id, Docs/설계/81번 §5.1)라 카탈로그를 직접 조회하지 말고
    /// TryGetOwned로 종류 정보와 현재 번호를 얻는다.
    /// </summary>
    public interface IOwnedCaravanAssetReader
    {
        IReadOnlyList<string> GetOwnedIds(FormationUnitKind kind);

        /// <summary>보유 중인 개체의 종류 정보와 현재 번호(같은 종류 안 보유 순서 + 1). 미보유면 false.</summary>
        bool TryGetOwned(string instanceId, out OwnedCaravanAsset asset);
        event Action OnOwnedChanged;
    }
}
