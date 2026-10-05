using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>파괴 마차 제거 후 대열 정리 결과(설계 79번 §5.3). 반영(배치 저장·활동 취소)은 호출자가 한다.</summary>
    public readonly struct WreckRelocationResult
    {
        public FormationLayout Layout { get; }
        /// <summary>새 칸으로 옮겨진 유닛과 그 칸. 배치 중(Adding)이던 유닛은 배치 완료 상태로 여기 들어간다.</summary>
        public IReadOnlyList<(string unitId, int slot)> Relocated { get; }
        /// <summary>놓을 칸이 없어 팔레트로 돌아간 유닛.</summary>
        public IReadOnlyList<string> Released { get; }
        /// <summary>취소해야 할 진행 중 활동의 유닛 Id.</summary>
        public IReadOnlyList<string> CancelledActivityUnitIds { get; }
        /// <summary>정리 후 마차 덩어리 수 - 1보다 크면 대열 정리 단계가 필요하다.</summary>
        public int ComponentCount { get; }

        public WreckRelocationResult(FormationLayout layout, IReadOnlyList<(string unitId, int slot)> relocated, IReadOnlyList<string> released,
            IReadOnlyList<string> cancelledActivityUnitIds, int componentCount)
        {
            Layout = layout;
            Relocated = relocated;
            Released = released;
            CancelledActivityUnitIds = cancelledActivityUnitIds;
            ComponentCount = componentCount;
        }
    }

    /// <summary>
    /// 전투로 파괴된 마차를 배치에서 빼고, 그 때문에 있을 수 없게 된 유닛을 가까운 칸으로 옮기거나 팔레트로 돌린다(설계 79번 §5.3, 기획 77·78번).
    /// FormationAreaRules.Remove를 쓰지 않는 이유: 그쪽은 연결이 끊기면 편집 자체를 거부하는데, 파괴는 이미 일어난 일이라 거부할 수 없다(강제
    /// 경로). 끊어짐은 덩어리 수로만 돌려주고 ③ 대열 정리 단계(또는 궤주 시 마을 출발 조건)가 처리한다.
    /// - 시설 먼저, 다음 캐릭터: 시설이 대열을 넓혀 캐릭터가 들어갈 칸이 생길 수 있다. 한 유닛을 옮길 때마다 영역을 다시 계산한다.
    /// - 칸 선택: 원래 칸에서 칸 중심 직선거리가 가장 가까운 칸, 동률이면 칸 번호가 작은 쪽(설계 79번 §15-4). 다른 활동의 목표 칸은 피한다 -
    ///   그 활동이 완료되면 겹친다.
    /// - 마차 배치(Adding) 후보는 영역을 넓힐 수 있어 시설보다도 먼저 처리한다(설계에 명시되지 않은 경우 - 상행 중 팔레트 마차 배치).
    /// </summary>
    public static class FormationWreckRelocator
    {
        private readonly struct Candidate
        {
            public readonly string UnitId;
            public readonly IFormationUnit Unit;
            public readonly int OriginSlotIndex;

            public Candidate(string unitId, IFormationUnit unit, int originSlotIndex)
            {
                UnitId = unitId;
                Unit = unit;
                OriginSlotIndex = originSlotIndex;
            }
        }

        /// <param name="lookup">파괴 마차 Id도 찾을 수 있어야 한다 - 보유 목록에서 마차를 빼기 전에 만든 조회 함수를 넘긴다.</param>
        public static WreckRelocationResult Relocate(FormationLayout layout, IReadOnlyCollection<string> removedWagonIds, Func<string, IFormationUnit> lookup,
            IReadOnlyList<FormationActivity> activities, IReadOnlyList<FormationAreaPin> pins = null)
        {
            var removed = removedWagonIds != null ? new HashSet<string>(removedWagonIds) : new HashSet<string>();
            activities ??= Array.Empty<FormationActivity>();

            // 1. 파괴 마차 칸을 비운다.
            var result = layout.Clone();
            for (var slot = 0; slot < result.SlotCount; slot++)
            {
                var id = result.GetUnitId(slot);
                if (!string.IsNullOrEmpty(id) && removed.Contains(id)) result.Clear(slot);
            }

            var area = FormationArea.Compute(result, lookup, pins);
            var cancelled = new HashSet<string>();
            var candidates = new List<Candidate>();

            // 2. 진행 중 활동: 목표 칸이 새 영역에서 불가능하면 취소. 배치 중(Adding)이면 목표 칸을 원래 칸 삼아 재배치 후보로, 이동 중(Moving)이면
            //    출발 칸(배치상 위치)에 남는다 - 출발 칸도 밖이면 아래 3에서 배치된 유닛으로서 재배치된다(설계 79번 §15-5·§15-6).
            foreach (var activity in activities)
            {
                if (removed.Contains(activity.UnitId))
                {
                    cancelled.Add(activity.UnitId);
                    continue;
                }

                var unit = lookup?.Invoke(activity.UnitId);
                if (unit == null || FormationAreaRules.CanPlaceAt(area, unit, activity.TargetSlotIndex) == FormationEditRejection.None) continue;

                cancelled.Add(activity.UnitId);
                if (activity.Kind == FormationActivityKind.Adding) candidates.Add(new Candidate(activity.UnitId, unit, activity.TargetSlotIndex));
            }

            // 3. 있을 수 없는 칸의 배치 유닛(시설은 자리 칸 밖, 캐릭터는 대열 밖, 마차 0대면 기준 칸 규칙)을 모은다. 이런 유닛은 영역에 기여하지
            //    않으므로(FormationArea 요약 참고) 먼저 모두 빼 두어도 영역은 그대로다.
            for (var slot = 0; slot < result.SlotCount; slot++)
            {
                var id = result.GetUnitId(slot);
                if (string.IsNullOrEmpty(id)) continue;

                var unit = lookup?.Invoke(id);
                if (unit == null || unit.Kind == FormationUnitKind.Wagon) continue;
                if (FormationAreaRules.CanPlaceAt(area, unit, slot) == FormationEditRejection.None) continue;

                candidates.Add(new Candidate(id, unit, slot));
                result.Clear(slot);
            }

            // 옮겨지든 팔레트로 가든 그 유닛의 활동은 취소한다 - 활동이 가리키던 칸·경로가 더는 의미 없다.
            foreach (var candidate in candidates) cancelled.Add(candidate.UnitId);

            candidates.Sort((a, b) =>
            {
                var byKind = KindOrder(a.Unit.Kind).CompareTo(KindOrder(b.Unit.Kind));
                return byKind != 0 ? byKind : a.OriginSlotIndex.CompareTo(b.OriginSlotIndex);
            });

            // 남은(취소되지 않은) 활동의 목표 칸 - 완료되면 그 칸을 차지한다.
            var reservedSlots = new HashSet<int>();
            foreach (var activity in activities)
            {
                if (!cancelled.Contains(activity.UnitId)) reservedSlots.Add(activity.TargetSlotIndex);
            }

            // 4. 후보마다 영역을 다시 계산해 가장 가까운 칸으로 옮긴다. 없으면 팔레트.
            var relocated = new List<(string unitId, int slot)>();
            var released = new List<string>();
            foreach (var candidate in candidates)
            {
                area = FormationArea.Compute(result, lookup, pins);
                var target = FindNearestFreeSlot(area, result, candidate, reservedSlots);
                if (target < 0)
                {
                    released.Add(candidate.UnitId);
                    continue;
                }

                result.SetUnitId(target, candidate.UnitId);
                relocated.Add((candidate.UnitId, target));
            }

            // 5. 연결은 검사하지 않는다 - 덩어리 수만 돌려준다.
            var finalArea = FormationArea.Compute(result, lookup, pins);

            var cancelledInOrder = new List<string>();
            foreach (var activity in activities)
            {
                if (cancelled.Contains(activity.UnitId) && !cancelledInOrder.Contains(activity.UnitId)) cancelledInOrder.Add(activity.UnitId);
            }

            return new WreckRelocationResult(result, relocated, released, cancelledInOrder, finalArea.ComponentCount);
        }

        private static int KindOrder(FormationUnitKind kind) => kind switch
        {
            FormationUnitKind.Wagon => 0,
            FormationUnitKind.Facility => 1,
            _ => 2,
        };

        private static int FindNearestFreeSlot(FormationArea area, FormationLayout layout, Candidate candidate, HashSet<int> reservedSlots)
        {
            var columnCount = layout.ColumnCount;
            var originColumn = candidate.OriginSlotIndex % columnCount;
            var originRow = candidate.OriginSlotIndex / columnCount;

            var best = -1;
            var bestDistance = int.MaxValue;
            // 대열 칸만 훑는다 - 판 전체(2,500칸)보다 작고, 대열 밖 칸은 어떤 유닛도 놓을 수 없다(마차 0대 기준 칸·핀 포함).
            foreach (var slot in area.Cells)
            {
                if (!string.IsNullOrEmpty(layout.GetUnitId(slot)) || reservedSlots.Contains(slot)) continue;
                if (FormationAreaRules.CanPlaceAt(area, candidate.Unit, slot) != FormationEditRejection.None) continue;

                var dx = slot % columnCount - originColumn;
                var dy = slot / columnCount - originRow;
                var distance = dx * dx + dy * dy;
                if (distance < bestDistance || (distance == bestDistance && slot < best))
                {
                    best = slot;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
