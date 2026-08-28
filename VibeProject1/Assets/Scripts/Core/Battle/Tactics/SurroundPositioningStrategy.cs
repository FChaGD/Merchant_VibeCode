using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// LocalPositioning.Surround(포위) - RangedSurroundCoordinator가 배정한 각도를 조회해 타겟
    /// 궤도 좌표로 반환한다(Docs/설계/12번 §13.3). BlockingPositioningStrategy와 같은 성격 - 코디네이터가
    /// 알아서 그룹/각도를 관리해두므로 이 전략은 "내 각도가 뭔지 물어보고 궤도 좌표로 환산"하는 것
    /// 말고 할 일이 없다.
    /// </summary>
    public class SurroundPositioningStrategy : IPositioningStrategy
    {
        private readonly RangedSurroundCoordinator coordinator;

        public SurroundPositioningStrategy(RangedSurroundCoordinator coordinator)
        {
            this.coordinator = coordinator;
        }

        public Vector2 ComputeMoveTarget(
            IBattleCombatant self, Vector2 selfPosition, IDamageable target, float range, Vector2 homePosition,
            IReadOnlyList<IBattleCombatant> sameSideUnits)
        {
            if (coordinator.TryGetAngle(target, self, out var angle))
            {
                return target.Position + range * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            // 대기(Docs/설계/12번 §12.11과 같은 처리) - 아직 그룹에 배정되지 않은 극단적 경우.
            return selfPosition;
        }
    }
}
