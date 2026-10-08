using System;

namespace Game.Core
{
    /// <summary>
    /// 마구간 구매 로직이 쓰는 로스터 조작만 모은 계약(ISP, Docs/설계/56번 §4). 용병의 IHiredCharacterRoster와 같은 모양이다.
    /// 정비창·전투는 이 계약을 모르고 기존 읽기 계약(ICaravanRosterProvider)만 쓴다.
    /// </summary>
    public interface IOwnedCaravanAssetRoster : IOwnedCaravanAssetReader
    {
        int CountOwnedOfKind(FormationUnitKind kind);

        /// <summary>종류 Id로 개체를 하나 새로 발급해 보유에 추가한다. 같은 종류도 몇 대든 추가된다(기획 80번 §3-3).</summary>
        bool TryAddOwned(string kindId, out string instanceId);
    }
}
