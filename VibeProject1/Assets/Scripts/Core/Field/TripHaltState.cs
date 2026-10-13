using System.Collections.Generic;

namespace Game.Core
{
    public enum TripHaltTransition { None, Started, StillHeld, Ended }

    /// <summary>정지 원인 집합(설계 87 §6.2). 전이만 계산하고 부수 효과는 TripHaltController가 적용한다.</summary>
    public sealed class TripHaltState
    {
        private readonly HashSet<TripHaltReason> reasons = new();

        public bool IsHalted => reasons.Count > 0;
        public bool IsHeld(TripHaltReason reason) => reasons.Contains(reason);

        public TripHaltTransition Acquire(TripHaltReason reason)
        {
            if (!reasons.Add(reason)) return TripHaltTransition.None;
            return reasons.Count == 1 ? TripHaltTransition.Started : TripHaltTransition.StillHeld;
        }

        public TripHaltTransition Release(TripHaltReason reason)
        {
            if (!reasons.Remove(reason)) return TripHaltTransition.None;
            return reasons.Count == 0 ? TripHaltTransition.Ended : TripHaltTransition.StillHeld;
        }

        public void Clear() => reasons.Clear();
    }
}
