using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 전투 후 정비창 정리 모드의 편집 정책(설계 79번 §8, 기획 78번 §4.5). 시간이 멈춘 단계라 소요시간을 기다릴 수 없어 활동을 등록하지 않고
    /// IFormationRepository.Apply로 즉시 반영한다. 마차는 이동만(배치·제거 불가), 판 어디든(WagonPlacement.Anywhere) - 대열 안 이동만 허용하면
    /// 놓을 빈 칸이 없어 영영 못 잇는 경우가 생긴다. 연결 기준은 NoNewSplit - 덩어리가 셋 이상이면 한 번의 이동으로 다 이을 수 없어서다.
    /// 정지된 활동이 있는 유닛을 건드리면 그 활동을 취소하고 즉시 반영한다(기획 78번 §4-24·25). 취소는 반드시 Apply **전에** 한다 - 취소 이벤트가
    /// 패널을 다시 그리게 하는데, 드롭 처리 도중 이미 바뀐 배치로 다시 그리면 끌던 아이콘이 꺼져 뒤이은 OnEndDrag가 씹힌다(FormationGridEditor 주석).
    /// 그래서 해제·영역 밖이 될 활동도 편집 후 배치로 미리 골라 함께 취소한다.
    /// </summary>
    internal sealed class FieldRepairEditPolicy : IFieldFormationEditPolicy
    {
        private readonly ICaravanRosterProvider rosterProvider;
        private readonly IFormationRepository formationRepository;
        private readonly IFieldFormationActivityRepository activityRepository;
        private readonly Func<IReadOnlyList<FormationAreaPin>> getAreaPins;

        public FieldRepairEditPolicy(ICaravanRosterProvider rosterProvider, IFormationRepository formationRepository, IFieldFormationActivityRepository activityRepository,
            Func<IReadOnlyList<FormationAreaPin>> getAreaPins)
        {
            this.rosterProvider = rosterProvider;
            this.formationRepository = formationRepository;
            this.activityRepository = activityRepository;
            this.getAreaPins = getAreaPins;
        }

        // 마을과 같은 계산(가장 큰 마차 영역 + 1) - 마차를 판 어디든 옮길 수 있어 연결 가능한 칸까지 보여야 한다.
        public int GetVisibleMarginCells() => FormationVisibleMargin.ForAnywhereWagons(rosterProvider);

        // 마차는 새로 놓을 수 없다(기획 78번 §4-21).
        public bool ShowsInPalette(IFormationUnit unit) => unit.Kind != FormationUnitKind.Wagon;

        private IReadOnlyList<FormationAreaPin> GetAreaPins() => getAreaPins?.Invoke();

        // 상행 중 평소 규칙과 같이 빈 칸에만 놓는다 - 점유 칸을 덮어쓰면 덮인 유닛이 팔레트로 빠지는데, 그 유닛이 마차면 마차 제거가 된다.
        public bool HandlePaletteDrop(IFormationUnit unit, int targetSlotIndex)
        {
            if (unit == null || unit.Kind == FormationUnitKind.Wagon) return false;
            if (formationRepository == null || !formationRepository.TryLoadCurrent(out var layout)) return false;
            if (!string.IsNullOrEmpty(layout.GetUnitId(targetSlotIndex)) || IsOtherActivityTarget(targetSlotIndex, unit.Id)) return false;

            var lookup = FormationAreaRules.LookupFrom(rosterProvider);
            var result = FormationAreaRules.Place(layout, unit, targetSlotIndex, lookup, GetAreaPins(), WagonPlacement.Anywhere, ConnectivityRule.NoNewSplit);
            return Commit(result, lookup, unit.Id, null);
        }

        // 목표 칸이 점유돼 있으면 맞바꾼다(밀려난 유닛의 활동도 취소).
        public bool HandleGridMove(string unitId, int originSlotIndex, int targetSlotIndex)
        {
            if (originSlotIndex == targetSlotIndex) return false;
            if (formationRepository == null || !formationRepository.TryLoadCurrent(out var layout)) return false;
            if (IsOtherActivityTarget(targetSlotIndex, unitId)) return false;

            var lookup = FormationAreaRules.LookupFrom(rosterProvider);
            var result = FormationAreaRules.Move(layout, originSlotIndex, targetSlotIndex, lookup, GetAreaPins(), WagonPlacement.Anywhere, ConnectivityRule.NoNewSplit);
            return Commit(result, lookup, unitId, layout.GetUnitId(targetSlotIndex));
        }

        public bool HandleRemove(string unitId, int slotIndex)
        {
            if (formationRepository == null || !formationRepository.TryLoadCurrent(out var layout)) return false;

            var lookup = FormationAreaRules.LookupFrom(rosterProvider);
            // 마차 제거 불가(기획 78번 §4-21).
            if (lookup(layout.GetUnitId(slotIndex))?.Kind == FormationUnitKind.Wagon) return false;

            var result = FormationAreaRules.Remove(layout, slotIndex, lookup, GetAreaPins(), ConnectivityRule.NoNewSplit);
            return Commit(result, lookup, unitId, null);
        }

        // 이동 중(정지)인 유닛을 끌어 놓으면 재조정하지 않고 활동을 취소한 뒤 출발 칸(배치상 아직 그 유닛이 있는 칸)에서 목표 칸으로 즉시 옮긴다.
        // 목표가 출발 칸이면 활동만 취소한다(제자리 배치 완료).
        public bool HandleRedirectMove(string unitId, int newTargetSlotIndex)
        {
            if (activityRepository == null || !activityRepository.TryGetActivity(unitId, out var activity) || activity.Kind != FormationActivityKind.Moving) return false;

            if (newTargetSlotIndex == activity.OriginSlotIndex)
            {
                activityRepository.Cancel(unitId);
                return true;
            }

            return HandleGridMove(unitId, activity.OriginSlotIndex, newTargetSlotIndex);
        }

        // 편집 후 배치 기준으로 취소할 활동을 모두 고른 뒤 취소하고, 마지막에 반영한다(취소를 Apply 전에 두는 이유는 클래스 주석).
        private bool Commit(FormationEditResult result, Func<string, IFormationUnit> lookup, string movedUnitId, string displacedUnitId)
        {
            if (!result.Accepted) return false;

            if (activityRepository != null)
            {
                var toCancel = new List<string>();
                AddIfBusy(toCancel, movedUnitId);
                AddIfBusy(toCancel, displacedUnitId);
                foreach (var releasedId in result.ReleasedUnitIds) AddIfBusy(toCancel, releasedId);

                // 편집으로 대열이 줄어 목표 칸이 영역 밖이 된 활동(설계 60번 §5와 같은 정리).
                var area = FormationArea.Compute(result.Layout, lookup, GetAreaPins());
                foreach (var activity in activityRepository.ActiveActivities)
                {
                    if (!area.CanHost(lookup(activity.UnitId), activity.TargetSlotIndex) && !toCancel.Contains(activity.UnitId)) toCancel.Add(activity.UnitId);
                }

                foreach (var id in toCancel) activityRepository.Cancel(id);
            }

            formationRepository.Apply(result.Layout);
            return true;
        }

        private void AddIfBusy(List<string> into, string unitId)
        {
            if (!string.IsNullOrEmpty(unitId) && activityRepository.IsUnitBusy(unitId) && !into.Contains(unitId)) into.Add(unitId);
        }

        // 다른 유닛의 활동 목표 칸에는 놓을 수 없다(설계 79번 §8 - 평소와 같음). 옮기는 유닛 자신의 활동은 곧 취소되므로 제외한다.
        private bool IsOtherActivityTarget(int slotIndex, string unitId)
        {
            if (activityRepository == null) return false;
            foreach (var activity in activityRepository.ActiveActivities)
            {
                if (activity.UnitId != unitId && activity.TargetSlotIndex == slotIndex) return true;
            }
            return false;
        }
    }
}
