using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// LocalPositioning.Stationary(원거리딜러) - 사거리 안이면 제자리에서 사격, 사거리 밖(공격 불가)이면
    /// 배치 위치(HomePosition)로 복귀한다(Docs/기획/19번). 자기보호(RetreatOnHit 등)가 위치를 강제로
    /// 옮겨도, 발동이 끝나 이 전략으로 제어가 돌아오면 원인과 무관하게 이 판정 하나로 복귀가 자연히
    /// 성립한다 - "자기보호 종료 시점"을 별도로 감지할 필요가 없다(기획 19번 §3).
    /// </summary>
    public class StationaryPositioningStrategy : IPositioningStrategy
    {
        public Vector2 ComputeMoveTarget(IBattleCombatant self, Vector2 selfPosition, IDamageable target, float range, Vector2 homePosition, IReadOnlyList<IBattleCombatant> sameSideUnits)
        {
            var distance = (target.Position - selfPosition).magnitude;
            return distance > range ? homePosition : selfPosition;
        }
    }
}
