namespace Game.Core
{
    /// <summary>
    /// EnemyRecognitionType.ProximityOrHit - 시간 제한 없음. 활동 반경 근처로 접근하거나 아군을
    /// 공격하면(NotifyAttackedBy, 둘 중 먼저) 인식. 피격으로 인한 인식은 공격한 그 개체만 인식된다
    /// (NotifyAttackedBy가 그 공격자 하나만 recognized에 추가하는 EnemyRecognitionTrackerBase의
    /// 기본 동작을 그대로 씀).
    /// </summary>
    public class ProximityOrHitRecognitionTracker : EnemyRecognitionTrackerBase
    {
        protected override bool ShouldRecognize(IDamageable enemy, float elapsedSeconds, IActivityRadiusZone radiusZone)
        {
            return radiusZone.Contains(enemy.Position);
        }
    }
}
