namespace Game.Core
{
    /// <summary>
    /// "현재 위치"를 변경하는 쓰기 인터페이스(기획 16번 §4). 상행 도착 판정이 성립하는 시점
    /// (FieldUIController.HandleArrived)에만 호출된다.
    /// </summary>
    public interface ITripCurrentLocationRepository : ITripCurrentLocationReader
    {
        void SetCurrentCity(int cityId);
    }
}
