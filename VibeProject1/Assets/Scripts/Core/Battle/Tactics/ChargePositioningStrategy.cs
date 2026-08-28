using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// LocalPositioning.Charge(전열) - 적에게 직진 접근(기존 기본 동작 그대로). 사거리 안에 들어오면
    /// 더 다가가지 않는다 - selfPosition을 그대로 반환해 "이동 없음"을 뜻한다(기존 BattleCharacterUnit이
    /// distance > Range일 때만 움직이던 동작을 그대로 승계, 안 그러면 사거리 안에서도 타겟 좌표까지
    /// 계속 파고드는 회귀가 생긴다).
    /// </summary>
    public class ChargePositioningStrategy : IPositioningStrategy
    {
        public Vector2 ComputeMoveTarget(IBattleCombatant self, Vector2 selfPosition, IDamageable target, float range, Vector2 homePosition, IReadOnlyList<IBattleCombatant> sameSideUnits)
        {
            var distance = (target.Position - selfPosition).magnitude;
            return distance > range ? target.Position : selfPosition;
        }
    }
}
