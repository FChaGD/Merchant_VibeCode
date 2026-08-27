namespace Game.Core
{
    /// <summary>EnemyRecognitionType.OneSecondDelay(기본값) - 스폰 후 1초 지연 인식.</summary>
    public class OneSecondDelayRecognitionTracker : EnemyRecognitionTrackerBase
    {
        protected override bool ShouldRecognize(IDamageable enemy, float elapsedSeconds, IActivityRadiusZone radiusZone)
        {
            return elapsedSeconds >= 1f;
        }
    }
}
