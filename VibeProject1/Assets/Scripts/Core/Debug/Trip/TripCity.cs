#if UNITY_EDITOR
using UnityEngine;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버그로 배치된 도시 1개의 식별자와 지도 콘텐츠 로컬 좌표(팬/줌 기준점인 content 중심 기준).
    /// Id는 정수다(기획 15번 §8.2, 설계 20번 §9) - "정식" 코드(TripDestinationAssigner 등)까지
    /// 포함해 도시 Id 체계 전체를 int로 통일했다.
    /// </summary>
    public readonly struct TripCity
    {
        public int Id { get; }
        public Vector2 MapPosition { get; }

        public TripCity(int id, Vector2 mapPosition)
        {
            Id = id;
            MapPosition = mapPosition;
        }
    }
}
#endif
