namespace Game.Core
{
    /// <summary>
    /// EnemyRecognitionType.FiveSecondOrProximity - 스폰 후 5초가 지나거나 그 전에 활동 반경
    /// 근처로 접근하면(둘 중 먼저) 인식.
    /// </summary>
    public class FiveSecondOrProximityRecognitionTracker : EnemyRecognitionTrackerBase
    {
        protected override bool ShouldRecognize(IDamageable enemy, float elapsedSeconds, IActivityRadiusZone radiusZone)
        {
            return elapsedSeconds >= 5f || radiusZone.Contains(enemy.Position);
        }
    }
}
