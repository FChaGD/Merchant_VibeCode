using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>가림 원인 집합(설계 89 §13.2) - 정지 원인(TripHaltState)과 같은 규칙. 하나라도 있으면 가림. 각 조작은 가림 여부가 바뀌었는지 돌려준다.</summary>
    public sealed class FieldUIHideState
    {
        private readonly HashSet<FieldUIHideReason> reasons = new();

        public bool IsHidden => reasons.Count > 0;

        public bool Acquire(FieldUIHideReason reason) => reasons.Add(reason) && reasons.Count == 1;

        public bool Release(FieldUIHideReason reason) => reasons.Remove(reason) && reasons.Count == 0;

        public bool Clear()
        {
            var wasHidden = IsHidden;
            reasons.Clear();
            return wasHidden;
        }
    }
}
