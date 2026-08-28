namespace Game.Core
{
    /// <summary>
    /// 방향성 지시 축의 테스트 수치를 한 곳에 모은다(MoraleTuning/ProtectedUnitTuning과 같은 자리) -
    /// 각 전략 구현체는 이 상수만 참조하고 매직 넘버를 직접 갖지 않는다. 전부 플레이테스트 후
    /// 조정 대상(Docs/설계/12번 §5.6).
    /// </summary>
    public static class TacticsTuning
    {
        // ActivityRadiusPreset.FormationHold(대열 유지) - 배치 슬롯 위치 기준 반경.
        public const float FixedRadiusMeters = 4f;
        // ActivityRadiusPreset.TripWide(상행 전체) - 대형 반지름(FormationExtentRadius)에 더하는 마진.
        public const float StandardRadiusMarginMeters = 5f;
        // PursuitPreset.OffensiveJudgment - 반경 밖에서 미명중 누적 시 복귀 트리거 시간.
        public const float OffensiveJudgmentTimeoutSeconds = 5f;
        // PursuitPreset.NoPursuit - 반경 밖 체류 누적 시 복귀 트리거 시간.
        public const float NoPursuitTimeoutSeconds = 3f;
        // SelfPreservation.FallBackOnHeavyDamage - 일시 후퇴를 발동시키는 HP 비율 임계치.
        public const float FallBackOnHeavyDamageHpRatioThreshold = 0.5f;
        // SelfPreservation.FallBackOnHeavyDamage - "일시" 후퇴가 지속되는 시간(제작 단계에서 확정 -
        // 시간 기반 지속을 표현하려면 필요했다).
        public const float FallBackOnHeavyDamageRetreatSeconds = 2f;
        // SelfPreservation.Kiting - 사거리 대비 이 비율 이내로 적이 접근하면 후퇴 시작.
        public const float KitingProximityRangeRatio = 0.4f;
        // SelfPreservation.RetreatOnHit - 피격 시 후퇴하는 거리.
        public const float RetreatOnHitDistanceMeters = 2f;
    }
}
