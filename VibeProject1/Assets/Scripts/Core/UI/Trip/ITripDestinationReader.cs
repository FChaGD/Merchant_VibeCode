using System;

namespace Game.Core
{
    /// <summary>
    /// 현재 도착지 배정 상태를 읽기만 하는 소비자를 위한 인터페이스(ISP). Changed는 배정이 바뀔 때마다
    /// (지정/취소/변경/삭제로 인한 해제 등) 발생한다.
    /// </summary>
    public interface ITripDestinationReader
    {
        int? DestinationCityId { get; }
        bool IsAssigned { get; }
        event Action Changed;
    }
}
