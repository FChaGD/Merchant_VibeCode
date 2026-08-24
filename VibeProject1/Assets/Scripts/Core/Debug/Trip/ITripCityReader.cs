#if UNITY_EDITOR
using System.Collections.Generic;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 배치된 디버그 도시를 읽기만 하는 소비자를 위한 인터페이스(ISP) - IFormationReader/Repository 분리와 동일한 패턴.
    /// </summary>
    public interface ITripCityReader
    {
        IReadOnlyList<TripCity> GetAll();
        bool TryGet(string cityId, out TripCity city);
    }
}
#endif
