using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 지도 콘텐츠 좌표(TripCity.MapPosition이 쓰는 값 - pivot=(0.5,0.5)라 지도 중심 기준, 음수 포함)와
    /// 엑셀에서 사람이 보는 정규화 좌표(좌하단 (0,0) ~ 우상단 (TripMapView.ContentSize, 같은 값), 항상
    /// 양수)를 상호 변환한다(Docs/기획/15번 §7.2, 설계 20번 §2). 저장(콘텐츠→정규화)과 임포트
    /// (정규화→콘텐츠) 양쪽이 필요로 해 공용으로 뽑았다 - 에디터/디버그 전용이 아니라 순수 계산이라
    /// #if UNITY_EDITOR로 감싸지 않는다.
    /// </summary>
    public static class TripCityMapCoordinateConverter
    {
        public static Vector2 ToNormalized(Vector2 contentPosition)
        {
            var half = TripMapView.ContentSize / 2f;
            return contentPosition + new Vector2(half, half);
        }

        public static Vector2 ToContentSpace(Vector2 normalizedPosition)
        {
            var half = TripMapView.ContentSize / 2f;
            return normalizedPosition - new Vector2(half, half);
        }
    }
}
