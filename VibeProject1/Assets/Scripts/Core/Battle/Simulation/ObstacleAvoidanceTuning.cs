using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마차·시설 장애물 회피 수치(Docs/기획/71번 §4.1, 설계 72번 §3.3) - 전부 테스트값.
    /// 기준 크기 이하는 고정값, 초과하면 크기 비례(사용자 결정 2026-10-01). 고정값을 "기준 크기에서의 비례값"으로
    /// 계산해 두었으므로 max(고정값, 비례값) 한 식이 곧 이 규칙이고, 기준 크기 경계에서 값이 끊기지 않는다.
    /// 기준 크기를 바꾸면 고정값도 따라간다.
    /// </summary>
    public static class ObstacleAvoidanceTuning
    {
        public const float ReferenceSize = ProtectedUnitTuning.BodySize;
        public const float ObstacleRadiusRatio = 1.43f;
        public const float ClearanceRatio = 0.71f;
        public const float MinObstacleRadius = ReferenceSize * 0.5f * ObstacleRadiusRatio;
        public const float MinClearance = ReferenceSize * 0.5f * ClearanceRatio;

        // 장애물 가장자리(회피 반경)부터 잰다 - 크기와 무관하게 "닿기 이만큼 전부터 틀기 시작"이 같다.
        public const float LookAheadDistance = 1.5f;
        // 도주는 목적지 없이 방향만 있어, 이 거리 앞의 가상 목적지로 조향을 계산한다.
        public const float FleeProbeDistance = 10f;
        // 좌우 두 접선이 목적지 방향과 이만큼도 차이 나지 않으면 정면으로 막힌 것으로 보고 오른쪽을 고른다.
        public const float SideTieThreshold = 0.02f;
        // 붙은 두 마차·시설 사이(통로)를 지나는 동안의 이동속도 배율(사용자 결정 2026-10-01, 설계 72번 §12).
        public const float PassageSpeedMultiplier = 0.5f;

        public static float ComputeObstacleRadius(float halfSize) => Mathf.Max(MinObstacleRadius, halfSize * ObstacleRadiusRatio);
        public static float ComputeClearance(float halfSize) => Mathf.Max(MinClearance, halfSize * ClearanceRatio);
    }
}
