using System;

namespace Game.Core
{
    /// <summary>
    /// 현재 출발지/도착지 배정 상태를 읽기만 하는 소비자를 위한 인터페이스(ISP). Changed는 배정이
    /// 바뀔 때마다(지정/취소/교환/삭제로 인한 해제 등) 발생해, 소비자가 상태를 폴링하지 않고도
    /// 정보 패널 표시나 "상행 시작" 게이팅을 갱신할 수 있게 한다.
    /// </summary>
    public interface ITripOriginDestinationReader
    {
        string OriginCityId { get; }
        string DestinationCityId { get; }
        bool IsBothAssigned { get; }
        event Action Changed;
    }
}
