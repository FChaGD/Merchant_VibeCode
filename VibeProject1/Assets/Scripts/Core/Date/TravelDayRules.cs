using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 이동 초 → 게임 일수(Docs/기획/84번 §4.1, 설계 85번 §3.2). 정보 패널 예상과 실제 가산이 같은 이동 수치를 읽어야 일치하므로
    /// 환산 값은 날짜 매니저가 아니라 TripTravelSettings에 둔다. 설정값이 0 이하면(새 필드가 없는 옛 씬 저장값) 기본값을 쓴다.
    /// 구간 소요시간은 공식 계산값(float)이라 89.99999초처럼 경계 바로 아래로 나올 수 있어, 버리기 전에 아주 작은 보정을 더한다.
    /// </summary>
    public static class TravelDayRules
    {
        private const float FloorEpsilonDays = 1e-4f;

        public static int ToDays(TripTravelSettings settings, float seconds) => FloorDays(seconds / SecondsPerDay(settings));

        public static int ToRoutDays(TripTravelSettings settings, float seconds) => FloorDays(seconds * RoutMultiplier(settings) / SecondsPerDay(settings));

        private static float SecondsPerDay(TripTravelSettings settings)
            => settings != null && settings.SecondsPerDay > 0f ? settings.SecondsPerDay : TripTravelSettings.DefaultSecondsPerDay;

        private static float RoutMultiplier(TripTravelSettings settings)
            => settings != null && settings.RoutDayMultiplier > 0f ? settings.RoutDayMultiplier : TripTravelSettings.DefaultRoutDayMultiplier;

        private static int FloorDays(float days) => days <= 0f ? 0 : Mathf.FloorToInt(days + FloorEpsilonDays);
    }
}
