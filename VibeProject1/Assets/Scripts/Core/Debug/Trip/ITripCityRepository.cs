#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버그 도시 배치/이동/삭제를 담당하는 쓰기 인터페이스. CityRemoved는 드래그 삭제/전체 삭제 등
    /// 삭제 경로가 여러 곳이라, 뷰 정리와 경로 연쇄 삭제를 이벤트 하나로 일원화하기 위해 존재한다.
    /// </summary>
    public interface ITripCityRepository
    {
        string Add(Vector2 mapPosition);
        void UpdatePosition(string cityId, Vector2 mapPosition);
        void Remove(string cityId);
        void Clear();
        event Action<string> CityRemoved;
    }
}
#endif
