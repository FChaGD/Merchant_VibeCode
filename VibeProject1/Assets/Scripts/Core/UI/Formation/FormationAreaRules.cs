using System;
using System.Collections.Generic;

namespace Game.Core
{
    public enum FormationEditRejection
    {
        None,
        OutsideArea,       // 목표 칸이 현재 대열 밖
        WagonOnly,         // 마차가 없어 기준 칸에는 마차만 놓을 수 있음
        Occupied,          // (상행 중) 목표 칸이 비어 있지 않음
        Disconnected,      // 편집 후 마차 연결이 끊김
        MinimumWagon,      // (상행 중) 마지막 마차 제거
    }

    /// <summary>
    /// 마차를 어디에 놓을 수 있는지(설계 60번 §15.2). 마을 정비창은 판 어디든(기획 59번 §3.4 - 다른 마차가 있으면 최종 상태에서 연결만 되면 허용),
    /// 상행 중 정비창은 현재 대열 안에만. 규칙은 FormationAreaRules 한곳에 두고 편집 정책이 이 값을 골라 넘긴다.
    /// </summary>
    public enum WagonPlacement
    {
        WithinArea,
        Anywhere,
    }

    /// <summary>편집 판정 결과. 허용되면 편집 후 배치와 자동 해제된 유닛 목록을 함께 준다.</summary>
    public readonly struct FormationEditResult
    {
        public FormationEditRejection Rejection { get; }
        public FormationLayout Layout { get; }
        public IReadOnlyList<string> ReleasedUnitIds { get; }
        public bool Accepted => Rejection == FormationEditRejection.None;

        private FormationEditResult(FormationEditRejection rejection, FormationLayout layout, IReadOnlyList<string> released)
        {
            Rejection = rejection;
            Layout = layout;
            ReleasedUnitIds = released;
        }

        public static FormationEditResult Reject(FormationEditRejection rejection) => new(rejection, null, Array.Empty<string>());
        public static FormationEditResult Accept(FormationLayout layout, IReadOnlyList<string> released) => new(FormationEditRejection.None, layout, released);
    }

    /// <summary>
    /// 마차 중심 대열의 편집 규칙(Docs/기획/59번 §3.2, 설계 60번 §4). 마을·상행 편집 정책이 같은 판정을 쓰도록 순수 함수로 둔다.
    /// 판정 방식: 편집을 배치 사본에 먼저 적용해 보고(후보 배치) 규칙을 검사한다 - 이동·교환을 "제거 + 배치"로 한 번에 다룰 수 있고,
    /// 동작 종류마다 규칙을 따로 구현하지 않아도 된다.
    /// </summary>
    public static class FormationAreaRules
    {
        /// <summary>배치에 적힌 유닛 Id를 로스터 유닛으로 찾는 조회 함수(판정 한 번 동안 쓸 사전을 만든다).</summary>
        public static Func<string, IFormationUnit> LookupFrom(ICaravanRosterProvider roster)
        {
            var byId = new Dictionary<string, IFormationUnit>();
            if (roster != null)
            {
                foreach (var unit in roster.GetRoster()) byId[unit.Id] = unit;
            }
            return id => !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out var unit) ? unit : null;
        }

        /// <summary>
        /// 팔레트 유닛을 목표 칸에 놓을 수 있는지(편집 전 영역 기준). 마차가 없으면 기준 칸에는 마차만, 핀 영역에는 누구나(설계 60번 §11-2).
        /// 마차 자유 배치(Anywhere)면 마차는 목표 칸 검사를 건너뛴다 - 연결 여부는 Validate가 최종 상태에서 판정한다.
        /// </summary>
        public static FormationEditRejection CanPlaceAt(FormationArea before, IFormationUnit unit, int targetSlotIndex, WagonPlacement wagonPlacement = WagonPlacement.WithinArea)
        {
            if (wagonPlacement == WagonPlacement.Anywhere && unit.Kind == FormationUnitKind.Wagon) return FormationEditRejection.None;
            if (!before.CanHost(unit, targetSlotIndex)) return FormationEditRejection.OutsideArea;
            if (before.WagonCount == 0 && unit.Kind != FormationUnitKind.Wagon && !before.IsPinCell(targetSlotIndex)) return FormationEditRejection.WagonOnly;
            return FormationEditRejection.None;
        }

        /// <summary>팔레트 배치(점유 칸이면 기존 유닛은 배치에서 빠짐).</summary>
        public static FormationEditResult Place(FormationLayout layout, IFormationUnit unit, int targetSlotIndex, Func<string, IFormationUnit> lookup, IReadOnlyList<FormationAreaPin> pins,
            WagonPlacement wagonPlacement = WagonPlacement.WithinArea, ConnectivityRule connectivity = ConnectivityRule.RequireConnected)
        {
            var before = FormationArea.Compute(layout, lookup, pins);
            var placeRejection = CanPlaceAt(before, unit, targetSlotIndex, wagonPlacement);
            if (placeRejection != FormationEditRejection.None) return FormationEditResult.Reject(placeRejection);

            var candidate = layout.Clone();
            candidate.SetUnitId(targetSlotIndex, unit.Id);
            return Validate(candidate, lookup, pins, connectivity, before.ComponentCount);
        }

        /// <summary>칸 → 칸 이동(목표가 점유돼 있으면 교환). 목표 칸은 편집 전 영역 안이어야 한다(마차 자유 배치면 마차는 예외).</summary>
        public static FormationEditResult Move(FormationLayout layout, int originSlotIndex, int targetSlotIndex, Func<string, IFormationUnit> lookup, IReadOnlyList<FormationAreaPin> pins,
            WagonPlacement wagonPlacement = WagonPlacement.WithinArea, ConnectivityRule connectivity = ConnectivityRule.RequireConnected)
        {
            var unit = lookup?.Invoke(layout.GetUnitId(originSlotIndex));
            if (unit == null) return FormationEditResult.Reject(FormationEditRejection.OutsideArea);

            var before = FormationArea.Compute(layout, lookup, pins);
            var placeRejection = CanPlaceAt(before, unit, targetSlotIndex, wagonPlacement);
            if (placeRejection != FormationEditRejection.None) return FormationEditResult.Reject(placeRejection);

            // 교환이면 밀려나는 유닛도 원래 칸에 있을 수 있어야 한다 - 시설이 시설 영역 칸으로 밀려나 곧바로 해제되는 것을 막는다.
            var displaced = lookup?.Invoke(layout.GetUnitId(targetSlotIndex));
            if (displaced != null && !before.CanHost(displaced, originSlotIndex)) return FormationEditResult.Reject(FormationEditRejection.OutsideArea);

            var candidate = layout.Clone();
            candidate.Swap(originSlotIndex, targetSlotIndex);
            return Validate(candidate, lookup, pins, connectivity, before.ComponentCount);
        }

        public static FormationEditResult Remove(FormationLayout layout, int slotIndex, Func<string, IFormationUnit> lookup, IReadOnlyList<FormationAreaPin> pins,
            ConnectivityRule connectivity = ConnectivityRule.RequireConnected)
        {
            // 편집 전 덩어리 수는 NoNewSplit에서만 쓴다 - 기본 기준 호출에 영역 계산을 한 번 더 붙이지 않는다.
            var beforeComponentCount = connectivity == ConnectivityRule.NoNewSplit ? FormationArea.Compute(layout, lookup, pins).ComponentCount : 1;
            var candidate = layout.Clone();
            candidate.Clear(slotIndex);
            return Validate(candidate, lookup, pins, connectivity, beforeComponentCount);
        }

        /// <summary>
        /// 후보 배치 검사(설계 60번 §14.3): 있을 수 없는 칸의 유닛(시설은 마차·핀 영역 밖, 캐릭터는 대열 밖)을 해제한 **뒤** 최종 상태에서 마차 연결을
        /// 판정한다 - 시설도 마차를 잇기 때문에(기획 59번 §3.3) 시설 해제로 연결이 끊기는지는 해제 후에야 알 수 있다. 끊기면 거부하고, 후보는 사본이라
        /// 해제도 반영되지 않는다. 시설을 해제하면 대열이 줄어 캐릭터가 추가로 밖이 될 수 있어 해제할 유닛이 없을 때까지 반복한다(마차는 자기 칸이 늘
        /// 자기 영역 안이라 해제되지 않는다).
        /// 연결 기준이 NoNewSplit이면 "모두 연결" 대신 최종 덩어리 수가 beforeComponentCount(편집 전 배치의 덩어리 수) 이하인지 본다(설계 79번 §5.5).
        /// 편집 전 덩어리 수는 후보 배치로는 알 수 없어 호출자가 넘긴다 - Place/Move/Remove는 원본 배치로 계산해 넘긴다.
        /// </summary>
        public static FormationEditResult Validate(FormationLayout candidate, Func<string, IFormationUnit> lookup, IReadOnlyList<FormationAreaPin> pins,
            ConnectivityRule connectivity = ConnectivityRule.RequireConnected, int beforeComponentCount = 1)
        {
            var released = new List<string>();
            FormationArea area;
            while (true)
            {
                area = FormationArea.Compute(candidate, lookup, pins);
                var releasedThisPass = false;
                for (var slot = 0; slot < candidate.SlotCount; slot++)
                {
                    var id = candidate.GetUnitId(slot);
                    if (string.IsNullOrEmpty(id) || area.CanHost(lookup?.Invoke(id), slot)) continue;

                    candidate.Clear(slot);
                    released.Add(id);
                    releasedThisPass = true;
                }

                if (!releasedThisPass) break;
            }

            var connected = connectivity == ConnectivityRule.NoNewSplit
                ? area.ComponentCount <= Math.Max(1, beforeComponentCount)
                : area.WagonsConnected;
            if (!connected) return FormationEditResult.Reject(FormationEditRejection.Disconnected);

            return FormationEditResult.Accept(candidate, released);
        }
    }
}
