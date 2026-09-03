using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 지역 시스템이 아직 없어, 상행 준비 UI를 테스트하기 위한 임시 장소 데이터.
    /// 실제 지역 데이터 모델이 생기면 대체된다.
    /// </summary>
    public class PlaceholderTripLocationInfo : ITripLocationInfo
    {
        public int Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public Sprite Icon { get; }

        public PlaceholderTripLocationInfo(int id, string displayName, string description, Sprite icon)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Icon = icon;
        }
    }
}
