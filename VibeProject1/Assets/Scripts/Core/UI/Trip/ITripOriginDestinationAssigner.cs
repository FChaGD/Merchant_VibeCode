namespace Game.Core
{
    /// <summary>
    /// 출발/도착 배정을 변경하는 쓰기 인터페이스(02번 기획 3.1/3.1.1절).
    /// </summary>
    public interface ITripOriginDestinationAssigner : ITripOriginDestinationReader
    {
        void HandleCityClicked(string cityId);
        void HandlePanelClicked(TripRole role);
        void HandleCityDeleted(string cityId);
    }
}
