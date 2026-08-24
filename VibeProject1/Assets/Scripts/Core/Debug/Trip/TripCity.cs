#if UNITY_EDITOR
using UnityEngine;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버그로 배치된 도시 1개의 식별자와 지도 콘텐츠 로컬 좌표(팬/줌 기준점인 content 중심 기준).
    /// </summary>
    public readonly struct TripCity
    {
        public string Id { get; }
        public Vector2 MapPosition { get; }

        public TripCity(string id, Vector2 mapPosition)
        {
            Id = id;
            MapPosition = mapPosition;
        }
    }
}
#endif
