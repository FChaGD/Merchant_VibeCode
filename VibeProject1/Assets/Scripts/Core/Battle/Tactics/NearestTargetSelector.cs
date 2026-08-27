using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>TargetPriority.Nearest(기본값) - 기존 IUnitSpatialQuery.FindNearest에 위임.</summary>
    public class NearestTargetSelector : ITargetSelector
    {
        private readonly IUnitSpatialQuery spatialQuery;

        public NearestTargetSelector(IUnitSpatialQuery spatialQuery)
        {
            this.spatialQuery = spatialQuery;
        }

        public IDamageable Select(Vector2 selfPosition, IReadOnlyList<IDamageable> recognizedCandidates)
        {
            return spatialQuery.FindNearest(selfPosition, recognizedCandidates);
        }
    }
}
