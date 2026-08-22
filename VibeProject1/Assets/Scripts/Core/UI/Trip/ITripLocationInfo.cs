using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 준비 UI의 출발지/도착지 정보 패널이 표시할 장소 정보의 공통 계약.
    /// 실제 지역 데이터 모델은 지역 시스템이 설계되면 구현한다.
    /// </summary>
    public interface ITripLocationInfo
    {
        string Id { get; }
        string DisplayName { get; }
        string Description { get; }
        Sprite Icon { get; }
    }
}
