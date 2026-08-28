using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// LocalPositioning.Disperse(원거리딜러) - 사거리를 확보하되(Charge와 동일한 접근/정지 판단),
    /// 동료 원거리딜러가 가까이 있으면 그만큼 밀려난 지점을 목적지로 삼는다(Docs/설계/12번 §13.2).
    /// 새 반발 공식을 만들지 않고 BattleCharacterUnit.ApplySeparation이 이미 쓰는
    /// IUnitSpatialQuery.ComputeSeparationPush를 그대로 재사용한다 - 검증된 로직 재사용 우선
    /// (§10-1 점검 이력과 같은 판단).
    /// </summary>
    public class DispersePositioningStrategy : IPositioningStrategy
    {
        private readonly IUnitSpatialQuery spatialQuery;
        // sameSideUnits 중 원거리딜러만 골라내는 재사용 버퍼 - 매 틱 새로 할당하지 않는다(§12.8과 같은 이유).
        private readonly List<IBattleCombatant> rangedAllyBuffer = new();

        public DispersePositioningStrategy(IUnitSpatialQuery spatialQuery)
        {
            this.spatialQuery = spatialQuery;
        }

        public Vector2 ComputeMoveTarget(
            IBattleCombatant self, Vector2 selfPosition, IDamageable target, float range, Vector2 homePosition,
            IReadOnlyList<IBattleCombatant> sameSideUnits)
        {
            var distance = (target.Position - selfPosition).magnitude;
            var approachTarget = distance > range ? target.Position : selfPosition;

            rangedAllyBuffer.Clear();
            foreach (var unit in sameSideUnits)
            {
                if (unit.RoleGroup == RoleGroup.RangedDealer) rangedAllyBuffer.Add(unit);
            }

            var pushOut = spatialQuery.ComputeSeparationPush(self, selfPosition, TacticsTuning.DisperseRadiusMeters, rangedAllyBuffer);
            return approachTarget + pushOut;
        }
    }
}
