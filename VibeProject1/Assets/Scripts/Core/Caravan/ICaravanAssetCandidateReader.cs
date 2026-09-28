using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 마구간의 구매 후보 조회(Docs/설계/56번 §6). 용병 후보(IMercenaryCandidateReader)와 같이 마을 Id를 받는다 - 마을별 판매
    /// 차별화가 생기면 소비자 코드는 두고 구현체만 교체한다.
    /// </summary>
    public interface ICaravanAssetCandidateReader
    {
        IReadOnlyList<CaravanAssetProfile> GetCandidates(int cityId, string facilityId);
    }
}
