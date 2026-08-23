namespace Game.Core
{
    /// <summary>
    /// 경로 연결(road-mode) on/off를 전환하는 쓰기 인터페이스.
    /// </summary>
    public interface ITripDebugRoadModeController : ITripDebugRoadModeReader
    {
        void Toggle();
    }
}
