namespace Game.Core
{
    /// <summary>
    /// UnitTacticsProfile + 전장 기하 정보를 6개 전략 인스턴스로 조립한다 - LiveBattleSimulationRule이
    /// 6개 팩토리를 직접 호출하지 않고 이 한 곳에만 의존하게 해, 조립부(BuildAllies)가 계속 커지는
    /// 걸 막는다(Docs/설계/12번 §7 점검 이력 - SRP).
    /// </summary>
    public static class UnitTacticsBehaviorsFactory
    {
        public static UnitTacticsBehaviors Build(
            UnitTacticsProfile profile, float standardActivityRadius, float fieldRadius, IUnitSpatialQuery spatialQuery,
            FrontlineFormationCoordinator frontlineCoordinator, RangedSurroundCoordinator rangedSurroundCoordinator)
        {
            var radiusZone = ActivityRadiusZoneFactory.Create(profile.RadiusPreset, profile.HomePosition, standardActivityRadius);
            var recognitionTracker = EnemyRecognitionTrackerFactory.Create(profile.RecognitionType);
            var targetSelector = TargetSelectorFactory.Create(profile.TargetPriority, spatialQuery);
            var positioningStrategy = PositioningStrategyFactory.Create(profile.Positioning, frontlineCoordinator, rangedSurroundCoordinator, spatialQuery);
            var selfPreservationModifier = SelfPreservationModifierFactory.Create(profile.SelfPreservation);
            var pursuitPolicy = PursuitPolicyFactory.Create(profile.Pursuit);
            // 전장 경계 - 추적 프리셋과 무관하게 항상 적용되는 이동 하드 캡(Docs/기획/12번 §2.2-1,
            // Docs/설계/12번 §11). StandardActivityRadiusZone이 이미 "원점 기준 반경 clamp"라는
            // 동일한 수학을 갖고 있어 새 클래스 없이 그대로 재사용한다 - 대형 중심이 원점이라는
            // 가정은 활동 반경 TripWide 프리셋과 같다(BattleFieldLayout이 원점 기준으로 스폰/전장
            // 반경을 계산하므로 전장 경계도 같은 원점 기준 원이다).
            var fieldBoundaryZone = new StandardActivityRadiusZone(fieldRadius);

            return new UnitTacticsBehaviors(
                radiusZone, recognitionTracker, targetSelector, positioningStrategy, selfPreservationModifier, pursuitPolicy,
                profile.HomePosition, fieldBoundaryZone, profile.RoleGroup, profile.Positioning);
        }
    }
}
