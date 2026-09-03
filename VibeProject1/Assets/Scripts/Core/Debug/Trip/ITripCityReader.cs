#if UNITY_EDITOR
using System.Collections.Generic;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버그로 배치된 도시 전체를 조회하는 읽기 전용 계약(Docs/설계/19번 §2.1) - 저장 기능(도시 지도
    /// 데이터 저장)만 필요로 하는 능력이라 정식 인터페이스(ITripRouteReader처럼 Core/UI/Trip에 두지
    /// 않음)로 승격하지 않고 디버그 폴더에 둔다. 정식 소비자가 생기면 그때 승격을 재검토한다.
    /// </summary>
    public interface ITripCityReader
    {
        IReadOnlyList<TripCity> GetAll();
    }
}
#endif
