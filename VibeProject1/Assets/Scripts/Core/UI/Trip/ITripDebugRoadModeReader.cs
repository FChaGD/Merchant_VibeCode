using System;

namespace Game.Core
{
    /// <summary>
    /// 경로 연결(road-mode) on/off 상태를 읽기만 하는 소비자를 위한 인터페이스(ISP).
    /// </summary>
    public interface ITripDebugRoadModeReader
    {
        bool IsRoadModeActive { get; }
        event Action<bool> Changed;
    }
}
