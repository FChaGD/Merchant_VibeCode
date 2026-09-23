using System;

namespace Game.Core
{
    /// <summary>EnemyRecognitionType→구현체 매핑(OCP, Docs/설계/12번 §7).</summary>
    public static class EnemyRecognitionTrackerFactory
    {
        public static IEnemyRecognitionTracker Create(string type)
        {
            return type switch
            {
                "OneSecondDelay" => new OneSecondDelayRecognitionTracker(),
                "FiveSecondOrProximity" => new FiveSecondOrProximityRecognitionTracker(),
                "ProximityOrHit" => new ProximityOrHitRecognitionTracker(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            };
        }
    }
}
