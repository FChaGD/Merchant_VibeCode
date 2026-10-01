using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 지도 콘텐츠 좌표(pivot=(0.5,0.5)라 지도 중심 기준, 음수 포함)와 엑셀에서 사람이 보는 정규화 좌표(좌하단 (0,0) ~ 우상단
    /// (지도 가로, 지도 세로), 항상 양수)를 상호 변환한다(Docs/기획/15번 §7.2, 설계 20번 §2). 지도 크기는 지역마다 가질 수 있어
    /// 인자로 받는다(설계 69번 §3.3). 저장·임포트 양쪽이 쓰는 순수 계산이라 #if UNITY_EDITOR로 감싸지 않는다.
    /// </summary>
    public static class TripCityMapCoordinateConverter
    {
        public static Vector2 ToNormalized(Vector2 contentPosition, Vector2 mapSize) => contentPosition + mapSize * 0.5f;

        public static Vector2 ToContentSpace(Vector2 normalizedPosition, Vector2 mapSize) => normalizedPosition - mapSize * 0.5f;
    }
}
