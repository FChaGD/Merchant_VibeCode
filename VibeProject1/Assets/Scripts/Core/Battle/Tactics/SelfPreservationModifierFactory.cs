using System;

namespace Game.Core
{
    /// <summary>SelfPreservation→구현체 매핑(OCP, Docs/설계/12번 §7).</summary>
    public static class SelfPreservationModifierFactory
    {
        public static ISelfPreservationModifier Create(SelfPreservation selfPreservation)
        {
            return selfPreservation switch
            {
                SelfPreservation.Resilient => new ResilientSelfPreservationModifier(),
                SelfPreservation.FallBackOnHeavyDamage => new FallBackOnHeavyDamageSelfPreservationModifier(),
                SelfPreservation.Kiting => new KitingSelfPreservationModifier(),
                SelfPreservation.RetreatOnHit => new RetreatOnHitSelfPreservationModifier(),
                _ => throw new ArgumentOutOfRangeException(nameof(selfPreservation), selfPreservation, null),
            };
        }
    }
}
