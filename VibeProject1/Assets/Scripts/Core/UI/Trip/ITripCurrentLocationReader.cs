using System;

namespace Game.Core
{
    /// <summary>
    /// 플레이어가 현재 체류 중인 도시 Id를 읽기만 하는 소비자를 위한 인터페이스(ISP). 상행 준비 UI의
    /// 출발지는 더 이상 사용자가 지정하지 않고 이 값으로 자동 고정된다(기획 16번 §5).
    /// </summary>
    public interface ITripCurrentLocationReader
    {
        int CurrentCityId { get; }
        event Action Changed;
    }
}
