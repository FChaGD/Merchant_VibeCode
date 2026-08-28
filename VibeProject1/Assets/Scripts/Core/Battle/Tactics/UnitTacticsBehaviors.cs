using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// UnitTacticsProfile을 실제로 실행할 수 있는 형태(6개 전략 인스턴스)로 만든 것 - 유닛 1기당
    /// 1세트, 전투 시작 시 한 번 조립돼 BattleCharacterUnit 생성자에 그대로 주입된다. 이 컨테이너가
    /// null이면(적 유닛) 방향성 지시가 적용되지 않는다 - 이번 설계는 아군에만 적용되기 때문
    /// (Docs/설계/12번 §0).
    /// </summary>
    public class UnitTacticsBehaviors
    {
        public IActivityRadiusZone RadiusZone { get; }
        public IEnemyRecognitionTracker RecognitionTracker { get; }
        public ITargetSelector TargetSelector { get; }
        public IPositioningStrategy PositioningStrategy { get; }
        public ISelfPreservationModifier SelfPreservationModifier { get; }
        public IPursuitPolicy PursuitPolicy { get; }
        public Vector2 HomePosition { get; }
        // 추적 프리셋(PursuitPolicy)과 무관하게 항상 적용되는 이동 하드 캡(Docs/기획/12번 §2.2-1) -
        // RadiusZone(활동 반경 프리셋별)과 별개로, 전장 경계 자체를 나타낸다.
        public IActivityRadiusZone FieldBoundaryZone { get; }
        // 방진 형성 로직(Docs/설계/12번 §12.3)이 보호대상 후보군(RangedDealer/Supporter)을 식별해야
        // 해서 노출 - IBattleCombatant.RoleGroup이 이 값을 그대로 전달한다.
        public RoleGroup RoleGroup { get; }
        // 방진 형성 로직(Docs/설계/12번 §12.4)이 "LocalPositioning.Blocking을 고른 전열 유닛"을
        // 식별해야 해서 노출 - PositioningStrategy는 이미 구체 전략 인스턴스로 조립돼 있어 원본
        // enum 값을 잃어버리므로 별도로 들고 있는다.
        public LocalPositioning Positioning { get; }

        public UnitTacticsBehaviors(
            IActivityRadiusZone radiusZone, IEnemyRecognitionTracker recognitionTracker, ITargetSelector targetSelector,
            IPositioningStrategy positioningStrategy, ISelfPreservationModifier selfPreservationModifier, IPursuitPolicy pursuitPolicy,
            Vector2 homePosition, IActivityRadiusZone fieldBoundaryZone, RoleGroup roleGroup, LocalPositioning positioning)
        {
            RadiusZone = radiusZone;
            RecognitionTracker = recognitionTracker;
            TargetSelector = targetSelector;
            PositioningStrategy = positioningStrategy;
            SelfPreservationModifier = selfPreservationModifier;
            PursuitPolicy = pursuitPolicy;
            HomePosition = homePosition;
            FieldBoundaryZone = fieldBoundaryZone;
            RoleGroup = roleGroup;
            Positioning = positioning;
        }
    }
}
