using System;

namespace Game.Core
{
    /// <summary>SelfPreservation→구현체 매핑(OCP, Docs/설계/12번 §7).</summary>
    public static class SelfPreservationModifierFactory
    {
        public static ISelfPreservationModifier Create(string selfPreservation)
        {
            return selfPreservation switch
            {
                "Resilient" => new ResilientSelfPreservationModifier(),
                "FallBackOnHeavyDamage" => new FallBackOnHeavyDamageSelfPreservationModifier(),
                "Kiting" => new KitingSelfPreservationModifier(),
                "RetreatOnHit" => new RetreatOnHitSelfPreservationModifier(),
                _ => throw new ArgumentOutOfRangeException(nameof(selfPreservation), selfPreservation, null),
            };
        }
    }
}
