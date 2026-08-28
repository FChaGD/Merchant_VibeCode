using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 방진선(저지형 그룹) 하나의 상태 - 위치/방향, 합류 순서를 유지하는 소속 유닛 목록, 슬롯 배정을
    /// 틱 간 유지한다(Docs/설계/12번 §12.2, §12.6). FrontlineFormationCoordinator만 이 값을 갱신한다.
    ///
    /// 슬롯 소속은 "합류·이탈 이벤트가 있을 때만" 재계산한다 - 매 틱 "가장 가까운 슬롯"을 다시
    /// 따지면 유닛이 좌우로 진동하는 문제가 생긴다(§12.6). 슬롯 위치 자체는 linePoint를 중심으로
    /// lineDir 방향 1m 간격(정수 offset)으로 정의되는 무한한 좌표 집합이라, 실제로 필요한 만큼만
    /// 오프셋을 점유하는 딕셔너리로 표현한다(배열로 미리 크기를 잡을 이유가 없다).
    /// </summary>
    public class FrontlineFormationLine
    {
        public Vector2 LinePoint { get; set; }
        public Vector2 LineDir { get; set; }
        // 합류 순서를 그대로 보존한다(§12.2) - 슬롯 오프셋과는 별개로 "이 라인에 누가 있는지"를
        // 순서 있는 형태로 노출하는 용도(디버그/향후 UI 등).
        public List<IBattleCombatant> Members { get; } = new();
        // 교차 처리(§12.9)가 매 틱 다시 계산해 채우는 이동 가능 오프셋 범위 - null이면 그쪽으로
        // 무제한. 두 값 다 null이면 교차 없음(기존 동작 그대로).
        public int? MinAllowedOffset { get; set; }
        public int? MaxAllowedOffset { get; set; }
        // 이 라인이 상대하는 적 수(recognizedUnion 크기) - 교차 승자 판정 2순위 기준(§12.9 규칙2).
        // Update(§12.4)가 라인 기하를 갱신할 때 함께 채운다.
        public int EnemyCount { get; set; }
        // 라인 단위 이탈(당겨오기) 판정 전용 인스턴스(§3.2-1 "라인 단위로 이탈·복귀를 판단") - 파티의
        // Pursuit 프리셋을 그대로 쓰지만, 내부 누적 타이머는 유닛 개별 정책과 공유하지 않고 라인
        // 생성 시점에 별도로 만든다(Docs/설계/12번 §12.4).
        public IPursuitPolicy PursuitPolicy { get; set; }

        private readonly Dictionary<IBattleCombatant, int> slotOffsetByMember = new();
        private readonly Dictionary<int, IBattleCombatant> memberBySlotOffset = new();
        // FindNearestEmptyOffset 계산용 재사용 버퍼(§12.8과 같은 이유 - 이벤트성이라도 매번 새로
        // 할당할 이유는 없다).
        private readonly List<int> candidateOffsetBuffer = new();

        public Vector2 GetSlotPosition(int offset) => LinePoint + LineDir * (offset * TacticsTuning.LineSlotSpacingMeters);

        public bool TryGetSlotPosition(IBattleCombatant member, out Vector2 position)
        {
            if (slotOffsetByMember.TryGetValue(member, out var offset))
            {
                position = GetSlotPosition(offset);
                return true;
            }
            position = default;
            return false;
        }

        /// <summary>
        /// 합류 - 그 순간 가장 가까운 빈 슬롯에 배정하고 계속 유지한다(§12.6). 이미 소속돼 있으면
        /// 아무 것도 하지 않는다(중복 합류 방어). 교차 처리(§12.9)로 허용 범위가 너무 좁아져 빈
        /// 슬롯을 못 찾는 극단적 경우 false를 반환한다 - 호출자는 이번 틱엔 합류시키지 않고 넘어간다.
        /// </summary>
        public bool Join(IBattleCombatant member)
        {
            if (slotOffsetByMember.ContainsKey(member)) return true;

            var offset = FindNearestEmptyOffset(member.Position);
            if (!offset.HasValue) return false;

            AssignSlot(offset.Value, member);
            Members.Add(member);
            return true;
        }

        /// <summary>
        /// 이탈(사망/도주) - 그 슬롯만 비우고, 슬롯 오프셋상 가장 가까운 다른 소속 유닛을 그 자리로
        /// 당겨 채운다(1회성 이벤트, 연쇄 재배정 없음 - §12.6). 소속이 아니면 아무 것도 하지 않는다.
        /// </summary>
        public void Leave(IBattleCombatant member)
        {
            if (!slotOffsetByMember.TryGetValue(member, out var vacatedOffset)) return;

            RemoveSlot(member);
            Members.Remove(member);

            var neighborOffset = FindNearestOccupiedOffset(vacatedOffset);
            if (neighborOffset.HasValue)
            {
                var neighbor = memberBySlotOffset[neighborOffset.Value];
                RemoveSlot(neighbor);
                AssignSlot(vacatedOffset, neighbor);
            }
        }

        private void AssignSlot(int offset, IBattleCombatant member)
        {
            slotOffsetByMember[member] = offset;
            memberBySlotOffset[offset] = member;
        }

        private void RemoveSlot(IBattleCombatant member)
        {
            if (slotOffsetByMember.TryGetValue(member, out var offset))
            {
                slotOffsetByMember.Remove(member);
                memberBySlotOffset.Remove(offset);
            }
        }

        /// <summary>
        /// 교차 처리(§12.9)로 새로 좁혀진 허용 범위를 벗어난 소속 유닛을 제거해 evictedBuffer에
        /// 담아 돌려준다 - 죽음/도주(Leave)와 달리 인접 유닛을 당겨 채우지 않는다(빈 자리 자체가
        /// 더 이상 유효 영역이 아니므로 채울 이유가 없다). 호출자가 이 유닛들을 Join으로 다시
        /// 배치해야 한다 - 정상 배정 경로(FindNearestEmptyOffset)가 남은 빈 슬롯 중 가장 가까운
        /// 곳(대개 반대편)에 자동으로 배치해, 규칙3(손실분 보충)이 별도 로직 없이 성립한다.
        /// </summary>
        public void EvictOutOfBounds(List<IBattleCombatant> evictedBuffer)
        {
            evictedBuffer.Clear();
            foreach (var member in Members)
            {
                if (!IsOffsetAllowed(slotOffsetByMember[member])) evictedBuffer.Add(member);
            }
            foreach (var member in evictedBuffer)
            {
                RemoveSlot(member);
                Members.Remove(member);
            }
        }

        private bool IsOffsetAllowed(int offset) =>
            (!MinAllowedOffset.HasValue || offset >= MinAllowedOffset.Value) &&
            (!MaxAllowedOffset.HasValue || offset <= MaxAllowedOffset.Value);

        // 빈 슬롯 후보 = 이미 점유된 오프셋 구간([min,max]) 안의 빈 칸 전부 + 그 구간 양 끝을 한 칸씩
        // 확장한 지점(min-1, max+1). 그 너머 오프셋은 확장 지점보다 항상 더 멀어서(1m 간격 직선이라
        // 확장 지점을 지나면 거리가 단조 증가) 후보에 넣을 필요가 없다. 소속이 하나도 없으면 오프셋 0.
        // 교차 처리(§12.9)로 막힌 오프셋은 후보에서 제외한다 - 전부 막혀 있으면 null(합류 실패).
        private int? FindNearestEmptyOffset(Vector2 position)
        {
            candidateOffsetBuffer.Clear();

            if (memberBySlotOffset.Count == 0)
            {
                if (IsOffsetAllowed(0)) candidateOffsetBuffer.Add(0);
            }
            else
            {
                var min = int.MaxValue;
                var max = int.MinValue;
                foreach (var offset in memberBySlotOffset.Keys)
                {
                    if (offset < min) min = offset;
                    if (offset > max) max = offset;
                }
                for (var offset = min; offset <= max; offset++)
                {
                    if (!memberBySlotOffset.ContainsKey(offset) && IsOffsetAllowed(offset)) candidateOffsetBuffer.Add(offset);
                }
                if (IsOffsetAllowed(min - 1)) candidateOffsetBuffer.Add(min - 1);
                if (IsOffsetAllowed(max + 1)) candidateOffsetBuffer.Add(max + 1);
            }

            if (candidateOffsetBuffer.Count == 0) return null;

            var bestOffset = candidateOffsetBuffer[0];
            var bestSqrDistance = (GetSlotPosition(bestOffset) - position).sqrMagnitude;
            for (var i = 1; i < candidateOffsetBuffer.Count; i++)
            {
                var sqrDistance = (GetSlotPosition(candidateOffsetBuffer[i]) - position).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestOffset = candidateOffsetBuffer[i];
                }
            }
            return bestOffset;
        }

        // 오프셋 거리(|offset - targetOffset|) 기준 가장 가까운 점유 슬롯을 찾는다 - 반드시 인접
        // (±1)일 필요는 없다(이미 ±1이 비어있을 수 있음).
        private int? FindNearestOccupiedOffset(int targetOffset)
        {
            int? best = null;
            var bestDistance = int.MaxValue;
            foreach (var offset in memberBySlotOffset.Keys)
            {
                var distance = Mathf.Abs(offset - targetOffset);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = offset;
                }
            }
            return best;
        }
    }
}
