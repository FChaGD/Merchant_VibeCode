namespace Game.Core
{
    /// <summary>
    /// 마을(도시)별 시설 제공 여부. 시설 단위로만 판단하고 카테고리 제공 여부는 소비자가 파생한다
    /// (제공 시설이 1개 이상이면 카테고리 노출) - 데이터 원천을 하나로 둬 "카테고리는 보이는데 들어가면
    /// 빈 화면" 같은 불일치를 구조적으로 막는다(Docs/설계/37번 §5.1).
    /// </summary>
    public interface ITownFacilityAvailabilityReader
    {
        bool IsFacilityAvailable(int cityId, string facilityId);
    }
}
