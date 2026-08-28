using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// LocalPositioning.Blocking(전열) - 유닛 개별 계산을 폐기하고, FrontlineFormationCoordinator가
    /// 전열 전체를 그룹으로 묶어 매 틱 재편성한 슬롯을 그대로 반환한다(Docs/설계/12번 §12, §12.12
    /// 7단계). 코디네이터가 알아서 이동/정지/당겨오기까지 판단해 슬롯 좌표를 확정해두므로, 이 전략은
    /// "내 슬롯이 뭔지 물어보고 그대로 반환"하는 것 말고 할 일이 없다.
    /// </summary>
    public class BlockingPositioningStrategy : IPositioningStrategy
    {
        private readonly FrontlineFormationCoordinator coordinator;

        public BlockingPositioningStrategy(FrontlineFormationCoordinator coordinator)
        {
            this.coordinator = coordinator;
        }

        public Vector2 ComputeMoveTarget(IBattleCombatant self, Vector2 selfPosition, IDamageable target, float range, Vector2 homePosition, IReadOnlyList<IBattleCombatant> sameSideUnits)
        {
            foreach (var line in coordinator.ActiveLines)
            {
                if (line.TryGetSlotPosition(self, out var slotPosition)) return slotPosition;
            }

            // 대기(Docs/설계/12번 §12.11) - 아직 어느 라인에도 합류하지 못한 상태. 이번 틱엔 이동
            // 없음(selfPosition 그대로) - 다음 틱 미배정 풀 처리가 합류 가능해지는 순간 자동 편입된다.
            return selfPosition;
        }
    }
}
