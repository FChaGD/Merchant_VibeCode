namespace Game.Core
{
    /// <summary>
    /// UnitTacticsProfile + 전장 기하 정보를 6개 전략 인스턴스로 조립한다 - LiveBattleSimulationRule이
    /// 6개 팩토리를 직접 호출하지 않고 이 한 곳에만 의존하게 해, 조립부(BuildAllies)가 계속 커지는
    /// 걸 막는다(Docs/설계/12번 §7 점검 이력 - SRP).
    /// </summary>
    public static class UnitTacticsBehaviorsFactory
    {
        public static UnitTacticsBehaviors Build(UnitTacticsProfile profile, float standardActivityRadius, IUnitSpatialQuery spatialQuery)
        {
            var radiusZone = ActivityRadiusZoneFactory.Create(profile.RadiusPreset, profile.HomePosition, standardActivityRadius);
            var recognitionTracker = EnemyRecognitionTrackerFactory.Create(profile.RecognitionType);
            var targetSelector = TargetSelectorFactory.Create(profile.TargetPriority, spatialQuery);
            var positioningStrategy = PositioningStrategyFactory.Create(profile.Positioning);
            var selfPreservationModifier = SelfPreservationModifierFactory.Create(profile.SelfPreservation);
            var pursuitPolicy = PursuitPolicyFactory.Create(profile.Pursuit);

            return new UnitTacticsBehaviors(
                radiusZone, recognitionTracker, targetSelector, positioningStrategy, selfPreservationModifier, pursuitPolicy,
                profile.HomePosition);
        }
    }
}
