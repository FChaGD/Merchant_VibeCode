using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// LocalPositioning.Surround(포위) 조율자 - 전투 1회당 1개, BattleSimulationLoop이 필드로
    /// 소유한다(FrontlineFormationCoordinator와 같은 자리, Docs/설계/12번 §13.3). §12(방진 형성)와
    /// 같은 "코디네이터 + 얇은 전략" 패턴을 재사용하지만, 그룹화 기준이 훨씬 단순하다 - 거리 기반
    /// 군집화가 필요 없다. 스티키 타겟팅이 이미 "누가 같은 적을 노리는지"를 결정해주므로, 그룹화
    /// 키는 그냥 CurrentTarget 참조 그 자체다.
    /// </summary>
    public class RangedSurroundCoordinator
    {
        // 방진선과 달리 위치/방향 같은 별도 기하 상태가 없다 - 궤도 좌표는 매 틱 타겟 위치+자기
        // 사거리로 그때그때 계산되므로 캐시할 게 없다(각도만 이벤트성으로 유지).
        private class SurroundGroup
        {
            public readonly Dictionary<IBattleCombatant, float> AngleByMember = new();
        }

        private readonly Dictionary<IDamageable, SurroundGroup> groupsByTarget = new();

        // 재사용 버퍼(§12.8과 같은 이유) - 매 틱 새 List/Dictionary를 할당하지 않는다.
        private readonly List<IDamageable> targetsToRemoveBuffer = new();
        private readonly List<IBattleCombatant> membersToRemoveBuffer = new();
        private readonly List<float> angleSortBuffer = new();

        /// <summary>
        /// 각 유닛 Tick 이전에 실행된다(§12.2와 같은 타이밍 이유 - 이번 틱 SurroundPositioningStrategy가
        /// 참조할 각도를 유닛이 움직이기 전에 먼저 확정해둔다).
        /// </summary>
        public void Update(IReadOnlyList<IBattleCombatant> allies)
        {
            CleanUpGroups();
            AssignNewMembers(allies);
        }

        public bool TryGetAngle(IDamageable target, IBattleCombatant member, out float angle)
        {
            if (groupsByTarget.TryGetValue(target, out var group) && group.AngleByMember.TryGetValue(member, out angle))
            {
                return true;
            }
            angle = 0f;
            return false;
        }

        // 타겟 사망 시 그룹 통째로 제거. 소속 유닛 중 사망/도주했거나 더 이상 이 타겟을 CurrentTarget
        // 으로 갖지 않는 유닛은 그 유닛만 제거한다(각도 재분배 없음 - §12.6과 같은 이벤트성 철학,
        // 빈 각도는 그냥 비워둔다).
        private void CleanUpGroups()
        {
            targetsToRemoveBuffer.Clear();
            foreach (var pair in groupsByTarget)
            {
                var target = pair.Key;
                var group = pair.Value;

                if (!target.IsAlive)
                {
                    targetsToRemoveBuffer.Add(target);
                    continue;
                }

                membersToRemoveBuffer.Clear();
                foreach (var member in group.AngleByMember.Keys)
                {
                    if (!member.IsAlive || member.IsFleeing || !ReferenceEquals(member.CurrentTarget, target))
                    {
                        membersToRemoveBuffer.Add(member);
                    }
                }
                foreach (var member in membersToRemoveBuffer) group.AngleByMember.Remove(member);

                if (group.AngleByMember.Count == 0) targetsToRemoveBuffer.Add(target);
            }

            foreach (var target in targetsToRemoveBuffer) groupsByTarget.Remove(target);
        }

        // Positioning==Surround·생존·타겟 보유 유닛 중 자기 타겟의 그룹에 아직 없는 유닛만 새로 합류시킨다.
        private void AssignNewMembers(IReadOnlyList<IBattleCombatant> allies)
        {
            foreach (var ally in allies)
            {
                if (!ally.IsAlive || ally.IsFleeing) continue;
                if (ally.Positioning != LocalPositioning.Surround) continue;

                var target = ally.CurrentTarget;
                if (target == null) continue;

                if (!groupsByTarget.TryGetValue(target, out var group))
                {
                    group = new SurroundGroup();
                    groupsByTarget[target] = group;
                }

                if (group.AngleByMember.ContainsKey(ally)) continue;

                group.AngleByMember[ally] = ComputeAssignmentAngle(group, target, ally);
            }
        }

        // 그룹이 비어있으면 "타겟→자기 현재 위치" 방향의 각도 그대로 배정(불필요한 순간이동 방지,
        // Blocking의 "가장 가까운 빈 슬롯"과 같은 이유). 기존 멤버가 있으면 "기존 각도들 사이 가장
        // 넓은 간격의 중간각"을 배정해 원 위에서 균등하게 퍼지도록 한다(Docs/설계/12번 §13.3/§13.4).
        private float ComputeAssignmentAngle(SurroundGroup group, IDamageable target, IBattleCombatant joining)
        {
            if (group.AngleByMember.Count == 0)
            {
                var toSelf = joining.Position - target.Position;
                return toSelf.sqrMagnitude > 0.0001f ? Mathf.Atan2(toSelf.y, toSelf.x) : 0f;
            }

            angleSortBuffer.Clear();
            foreach (var angle in group.AngleByMember.Values) angleSortBuffer.Add(angle);
            angleSortBuffer.Sort();

            var bestGapStart = angleSortBuffer[0];
            var bestGapSize = 0f;
            for (var i = 0; i < angleSortBuffer.Count; i++)
            {
                var current = angleSortBuffer[i];
                // 마지막 각도→처음 각도 랩어라운드 간격(+2π)까지 포함해 원 전체를 훑는다.
                var next = i + 1 < angleSortBuffer.Count ? angleSortBuffer[i + 1] : angleSortBuffer[0] + Mathf.PI * 2f;
                var gap = next - current;
                if (gap > bestGapSize)
                {
                    bestGapSize = gap;
                    bestGapStart = current;
                }
            }

            return bestGapStart + bestGapSize * 0.5f;
        }
    }
}
