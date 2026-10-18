using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 단위 날짜 누적(Docs/기획/92번 §3, 설계 93번 §3.1) - 순수. 구간 시계는 구간마다 경과를 0으로 되돌리므로, 중간 도시를 넘어 이어지는 30초 미만
    /// 나머지는 여기서 따로 들고 있다. 이동 수치는 호출마다 받는다 - Play 중 인스펙터 튜닝이 다음 호출부터 바로 반영되고, 설정 소스를 몰라도 된다.
    /// 일수는 TravelDayRules와 같은 환산(경계 보정 포함)으로 내고, 보정 때문에 나머지가 미세하게 음수가 되는 것은 0으로 막는다.
    /// 궤주는 구간 시계의 경과가 아니라 여기서 센 "직전 도시부터 실제 이동 초"를 쓴다 - 구간 경과는 난이도 재계산 때 진행 비율로 다시 맞춰져
    /// 실제 이동과 어긋난다(설계 93 §3.1 개정 2026-10-18).
    /// </summary>
    public sealed class TripDayCounter
    {
        public float RemainderSeconds { get; private set; }
        public float MovedSeconds { get; private set; }
        /// <summary>직전 도시(또는 출발)부터 실제 이동 초 - 중간 도시 도착에서 0으로 돌린다.</summary>
        public float LegMovedSeconds { get; private set; }

        /// <summary>이동 초를 더하고 이번에 넘긴 일수를 돌려준다.</summary>
        public int AddMoved(TripTravelSettings settings, float seconds)
        {
            if (seconds <= 0f) return 0;
            MovedSeconds += seconds;
            LegMovedSeconds += seconds;
            RemainderSeconds += seconds;
            return TakeDays(settings);
        }

        public float SecondsUntilNextDay(TripTravelSettings settings) => Mathf.Max(0f, TravelDayRules.SecondsPerDay(settings) - RemainderSeconds);

        /// <summary>궤주 정산 결과 미리보기(디버그) - 상태를 바꾸지 않는다.</summary>
        public int PreviewRout(TripTravelSettings settings)
            => TravelDayRules.ToDays(settings, RemainderSeconds + LegMovedSeconds * TravelDayRules.RoutExtraRatio(settings));

        /// <summary>궤주: 나머지 + 직전 도시부터 이동 초 × 추가 비율을 일수로 바꾸고 나머지는 버린다(기획 92 §3-3·3-4).</summary>
        public int SettleRout(TripTravelSettings settings)
        {
            var days = PreviewRout(settings);
            RemainderSeconds = 0f;
            LegMovedSeconds = 0f;
            return days;
        }

        /// <summary>중간 도시 도착 - 나머지는 이어지고 구간 이동 초만 0으로.</summary>
        public void BeginLeg() => LegMovedSeconds = 0f;

        public void Reset()
        {
            RemainderSeconds = 0f;
            MovedSeconds = 0f;
            LegMovedSeconds = 0f;
        }

        private int TakeDays(TripTravelSettings settings)
        {
            var days = TravelDayRules.ToDays(settings, RemainderSeconds);
            if (days > 0) RemainderSeconds = Mathf.Max(0f, RemainderSeconds - days * TravelDayRules.SecondsPerDay(settings));
            return days;
        }
    }
}
