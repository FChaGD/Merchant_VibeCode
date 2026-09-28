using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 마을 시설의 고용 후보 조회(Docs/설계/54번 §7.1). 무역품 판매 목록(ITownShopStockReader)과 같은 모양으로 마을 Id를 받는다 -
    /// 마을별 후보 차별화·후보 갱신 주기(기획 53번 §6)가 생기면 소비자 코드는 두고 구현체만 교체한다.
    /// </summary>
    public interface IMercenaryCandidateReader
    {
        IReadOnlyList<CharacterProfile> GetCandidates(int cityId, string facilityId);
    }
}
