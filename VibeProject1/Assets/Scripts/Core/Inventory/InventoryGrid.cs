using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 그리드 좌표/점유 칸 계산을 전담하는 자료구조(Docs/설계/32번 §2). FormationLayout(슬롯 1칸=유닛
    /// 1개, 1차원 인덱스 배열)과 달리 아이템이 여러 칸(WxH)을 동시에 차지할 수 있어 좌표 기반 점유
    /// 배열로 겹침/범위를 검사한다.
    ///
    /// 구역(섹션)을 여러 개 가질 수 있다(설계 64번 §3) - 상단 물류품은 마차 1대 = 섹션 1개, 나머지 인벤토리는 섹션 1개다.
    /// 마차별로 그리드를 따로 두지 않고 한 그리드 안의 섹션으로 둔 이유는, 마차 간 이동·교환도 TryApplyPlacements 한 번의
    /// 원자적 조작이 되게 하기 위해서다 - 제거 후 배치로 옮기면 골드 상자 지갑 연동(저장소의 TryPlace/Remove 경로)이 끼어든다.
    /// 섹션 모양의 막힌 칸은 배치 판정에서 범위 밖과 똑같이 취급한다.
    ///
    /// 임시 보관은 별도 컬렉션이 아니라 "같은 인벤토리 안에서 칸을 점유하지 않는 상태"로 두고 모든 섹션이 공유한다
    /// (설계 40번 §3.1, 기획 63번 §3.4) - 그리드↔임시 보관 이동이 TryApplyPlacements 한 번의 원자적 조작이 된다.
    /// 섹션 인자가 null이면 첫 섹션이다 - 섹션이 1개인 인벤토리의 호출부를 단순하게 두기 위함이다.
    /// </summary>
    public class InventoryGrid
    {
        public const string DefaultSectionId = "main";

        private sealed class SectionState
        {
            public readonly InventorySection Section;
            public readonly string[,] Occupancy;

            public SectionState(InventorySection section)
            {
                Section = section;
                Occupancy = new string[section.Shape.Width, section.Shape.Height];
            }
        }

        private readonly Dictionary<string, InventoryItemInstance> placedById = new();
        private readonly List<InventoryItemInstance> stagedItems = new();
        private readonly List<InventorySection> sections = new();
        private readonly Dictionary<string, SectionState> sectionsById = new();

        public IReadOnlyList<InventorySection> Sections => sections;
        public IReadOnlyCollection<InventoryItemInstance> Items => placedById.Values;
        public IReadOnlyList<InventoryItemInstance> StagedItems => stagedItems;

        /// <summary>섹션 없이 시작한다 - 섹션은 AddSection으로 늘린다(상단 물류품: 보유 마차 동기화).</summary>
        public InventoryGrid()
        {
        }

        /// <summary>직사각형 섹션 1개짜리 그리드(장비·소모품·개인 물품, 테스트).</summary>
        public InventoryGrid(int width, int height)
        {
            AddSection(new InventorySection(DefaultSectionId, string.Empty, InventoryShape.Rectangle(width, height)));
        }

        /// <summary>섹션을 끝에 추가한다. 같은 Id가 이미 있으면 false. 모양 변경은 지원하지 않는다 - 마차가 바뀌면 섹션을 제거하고 새로 추가한다.</summary>
        public bool AddSection(InventorySection section)
        {
            if (section == null || section.Shape == null || sectionsById.ContainsKey(section.Id)) return false;

            sections.Add(section);
            sectionsById[section.Id] = new SectionState(section);
            return true;
        }

        /// <summary>
        /// 섹션을 제거한다(파괴 마차 정리, 기획 77번 §3-8·설계 79번 §5.2). 배치된 아이템이 남아 있으면 false - 아이템을 어디로 보낼지
        /// (임시 보관·환급 여부)는 저장소의 정책이라, 그리드가 몰래 옮기거나 지우지 않고 호출자가 먼저 비우게 한다.
        /// </summary>
        public bool RemoveSection(string sectionId)
        {
            if (sectionId == null || !sectionsById.ContainsKey(sectionId)) return false;

            foreach (var item in placedById.Values)
            {
                if (item.SectionId == sectionId) return false;
            }

            sectionsById.Remove(sectionId);
            sections.RemoveAll(section => section.Id == sectionId);
            return true;
        }

        public bool HasSection(string sectionId) => sectionId != null && sectionsById.ContainsKey(sectionId);

        /// <summary>
        /// 새 인스턴스를 임시 보관 끝에 넣는다(전투 회수 물품, 설계 79번 §5.1). 칸을 점유하지 않으므로 실패하지 않는다 - 회수 물품이
        /// 들어갈 자리가 없어 사라지는 일이 없게, 배치는 플레이어가 적재 단계에서 직접 한다.
        /// </summary>
        public InventoryItemInstance StageNew(IInventoryItemDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            var staged = new InventoryItemInstance(Guid.NewGuid().ToString("N"), definition, default, 0, isStaged: true);
            stagedItems.Add(staged);
            return staged;
        }

        // quarterTurns: 구매 자동 배치가 회전한 자리를 쓸 수 있도록 받는다(Docs/설계/50번 §5.1).
        public bool TryPlace(IInventoryItemDefinition definition, GridPosition position, out InventoryItemInstance placed, int quarterTurns = 0, string sectionId = null)
        {
            placed = default;
            if (definition == null || !TryResolveSection(sectionId, out var state))
            {
                return false;
            }

            var turns = InventoryRotation.Normalize(quarterTurns);
            var width = turns % 2 == 1 ? definition.FootprintHeight : definition.FootprintWidth;
            var height = turns % 2 == 1 ? definition.FootprintWidth : definition.FootprintHeight;
            if (!FitsAndFree(state, position, width, height))
            {
                return false;
            }

            placed = new InventoryItemInstance(Guid.NewGuid().ToString("N"), definition, position, turns, isStaged: false, state.Section.Id);
            placedById[placed.InstanceId] = placed;
            Mark(state, placed, placed.InstanceId);
            return true;
        }

        public bool Remove(string instanceId)
        {
            if (placedById.Remove(instanceId, out var item))
            {
                Unmark(sectionsById[item.SectionId], item);
                return true;
            }

            return stagedItems.RemoveAll(staged => staged.InstanceId == instanceId) > 0;
        }

        public bool TryGetAt(string sectionId, GridPosition position, out InventoryItemInstance item)
        {
            if (!TryResolveSection(sectionId, out var state) || !state.Section.Shape.IsUsable(position.X, position.Y)
                || state.Occupancy[position.X, position.Y] is not { } instanceId)
            {
                item = default;
                return false;
            }

            return placedById.TryGetValue(instanceId, out item);
        }

        public bool TryFind(string instanceId, out InventoryItemInstance item)
        {
            if (placedById.TryGetValue(instanceId, out item)) return true;

            var index = IndexOfStaged(instanceId);
            item = index >= 0 ? stagedItems[index] : default;
            return index >= 0;
        }

        /// <summary>
        /// 목록의 아이템들을 한꺼번에 새 위치(또는 임시 보관)로 옮긴다. 섹션이 달라도 한 번에 처리한다. 하나라도 범위를
        /// 벗어나거나(막힌 칸 포함) 겹치면 아무것도 바꾸지 않고 false - 교환·자동 정렬·마차 간 이동이 중간 실패로 반쯤
        /// 적용되는 상태가 생기지 않는다.
        /// </summary>
        public bool TryApplyPlacements(IReadOnlyList<ItemPlacement> placements) => ApplyPlacements(placements, commit: true);

        /// <summary>TryApplyPlacements와 같은 검사만 하고 상태는 바꾸지 않는다(드래그 미리보기용).</summary>
        public bool CanApplyPlacements(IReadOnlyList<ItemPlacement> placements) => ApplyPlacements(placements, commit: false);

        private bool ApplyPlacements(IReadOnlyList<ItemPlacement> placements, bool commit)
        {
            if (placements == null || placements.Count == 0) return false;

            // 1) 대상 아이템을 전부 찾고 중복 지정·없는 섹션 지정을 거부한다.
            var originals = new List<InventoryItemInstance>(placements.Count);
            var targets = new List<SectionState>(placements.Count);
            var seen = new HashSet<string>();
            foreach (var placement in placements)
            {
                if (!seen.Add(placement.InstanceId) || !TryFind(placement.InstanceId, out var original)) return false;

                SectionState target = null;
                if (placement.Position != null && !TryResolveSection(placement.SectionId, out target)) return false;

                originals.Add(original);
                targets.Add(target);
            }

            // 2) 대상 아이템의 현재 점유를 해제한 상태에서 새 위치를 하나씩 검사·기록한다(서로 간 겹침도 검출).
            foreach (var original in originals)
            {
                if (!original.IsStaged) Unmark(sectionsById[original.SectionId], original);
            }

            var updated = new List<InventoryItemInstance>(placements.Count);
            var valid = true;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                var target = targets[i];
                var next = new InventoryItemInstance(
                    placement.InstanceId,
                    originals[i].Definition,
                    placement.Position ?? default,
                    placement.QuarterTurns,
                    isStaged: target == null,
                    target?.Section.Id);

                if (target != null)
                {
                    if (!FitsAndFree(target, next.Position, next.Width, next.Height))
                    {
                        valid = false;
                        break;
                    }
                    Mark(target, next, next.InstanceId);
                }
                updated.Add(next);
            }

            // 3) 실패했거나 검사만 하는 경우 기록한 새 점유를 지우고 원래 점유를 복구한다.
            if (!valid || !commit)
            {
                foreach (var next in updated)
                {
                    if (!next.IsStaged) Unmark(sectionsById[next.SectionId], next);
                }
                foreach (var original in originals)
                {
                    if (!original.IsStaged) Mark(sectionsById[original.SectionId], original, original.InstanceId);
                }
                return valid;
            }

            // 4) 확정: 임시 보관에 남는 아이템은 순서를 유지하고, 새로 들어오는 아이템은 끝에 붙인다.
            for (var i = 0; i < updated.Count; i++)
            {
                var original = originals[i];
                var next = updated[i];
                if (original.IsStaged && next.IsStaged)
                {
                    stagedItems[IndexOfStaged(next.InstanceId)] = next;
                    continue;
                }

                if (original.IsStaged) stagedItems.RemoveAt(IndexOfStaged(original.InstanceId));
                else placedById.Remove(original.InstanceId);

                if (next.IsStaged) stagedItems.Add(next);
                else placedById[next.InstanceId] = next;
            }

            return true;
        }

        private bool TryResolveSection(string sectionId, out SectionState state)
        {
            if (sectionId == null)
            {
                state = sections.Count > 0 ? sectionsById[sections[0].Id] : null;
                return state != null;
            }

            return sectionsById.TryGetValue(sectionId, out state);
        }

        private int IndexOfStaged(string instanceId) => stagedItems.FindIndex(staged => staged.InstanceId == instanceId);

        private static bool FitsAndFree(SectionState state, GridPosition position, int width, int height)
        {
            if (!state.Section.Shape.ContainsRect(position, width, height)) return false;

            for (var x = position.X; x < position.X + width; x++)
            {
                for (var y = position.Y; y < position.Y + height; y++)
                {
                    if (state.Occupancy[x, y] != null) return false;
                }
            }

            return true;
        }

        // 섹션 모양은 바뀌지 않고(제거는 빈 섹션만) 배치는 항상 FitsAndFree를 거치므로 점유 범위는 늘 섹션 안이다.
        private static void Mark(SectionState state, InventoryItemInstance item, string instanceId)
        {
            for (var x = item.Position.X; x < item.Position.X + item.Width; x++)
            {
                for (var y = item.Position.Y; y < item.Position.Y + item.Height; y++)
                {
                    state.Occupancy[x, y] = instanceId;
                }
            }
        }

        // 자기 Id가 기록된 칸만 지운다 - 검사 도중 되돌릴 때 그 칸을 차지한 다른 아이템의 점유까지 지우지 않기 위함이다.
        private static void Unmark(SectionState state, InventoryItemInstance item)
        {
            for (var x = item.Position.X; x < item.Position.X + item.Width; x++)
            {
                for (var y = item.Position.Y; y < item.Position.Y + item.Height; y++)
                {
                    if (state.Occupancy[x, y] == item.InstanceId) state.Occupancy[x, y] = null;
                }
            }
        }
    }
}
