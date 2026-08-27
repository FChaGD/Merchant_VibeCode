using System;

namespace Game.Core
{
    /// <summary>EnemyRecognitionType→구현체 매핑(OCP, Docs/설계/12번 §7).</summary>
    public static class EnemyRecognitionTrackerFactory
    {
        public static IEnemyRecognitionTracker Create(EnemyRecognitionType type)
        {
            return type switch
            {
                EnemyRecognitionType.OneSecondDelay => new OneSecondDelayRecognitionTracker(),
                EnemyRecognitionType.FiveSecondOrProximity => new FiveSecondOrProximityRecognitionTracker(),
                EnemyRecognitionType.ProximityOrHit => new ProximityOrHitRecognitionTracker(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            };
        }
    }
}
