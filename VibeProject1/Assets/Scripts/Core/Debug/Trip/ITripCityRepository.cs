#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버그 도시 배치/이동/삭제를 담당하는 쓰기 인터페이스. CityRemoved는 드래그 삭제/전체 삭제 등
    /// 삭제 경로가 여러 곳이라, 뷰 정리와 경로 연쇄 삭제를 이벤트 하나로 일원화하기 위해 존재한다.
    /// </summary>
    public interface ITripCityRepository : ITripCityReader
    {
        int Add(Vector2 mapPosition);
        // 저장된 지도를 불러올 때 전용 - Id를 자동 생성하지 않고 그대로 보존한다(경로가 그 Id로
        // 도시를 참조하므로 Id가 바뀌면 연결이 끊긴다, Docs/설계/19번 §3).
        void AddWithId(int cityId, Vector2 mapPosition);
        void UpdatePosition(int cityId, Vector2 mapPosition);
        void Remove(int cityId);
        void Clear();
        event Action<int> CityRemoved;
    }
}
#endif
