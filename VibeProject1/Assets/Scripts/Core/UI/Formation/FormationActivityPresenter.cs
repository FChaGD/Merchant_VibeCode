using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 진행 중인 배치/이동 활동(설계 25번 §5)을 경로선 + 슬롯 오버레이 마크 + 이동 아이콘으로 그린다. 잔여 시간이 매 프레임 줄어들어
    /// Field 정비창이 열린 동안 매 프레임 불리는 경로라 편집기(FormationGridEditor)에서 떼어 냈다(Docs/Refactor/2026-10-26_공통.md C-3b).
    /// 활동이 계속 0건이면 그리지 않는다(ActivityRedrawGate). 버퍼는 매 프레임 새로 할당하지 않고 재사용한다.
    /// Hub는 활동이 항상 0건이라 사실상 아무것도 하지 않는다.
    /// </summary>
    internal sealed class FormationActivityPresenter
    {
        private readonly FormationGridView view;
        private readonly Func<string, IFormationUnit> lookupUnit;
        private readonly ActivityRedrawGate redrawGate = new();
        private readonly List<FormationActivityOverlayVisual> overlayBuffer = new();
        private readonly List<FormationMovePathVisual> movePathBuffer = new();

        public FormationActivityPresenter(FormationGridView view, Func<string, IFormationUnit> lookupUnit)
        {
            this.view = view;
            this.lookupUnit = lookupUnit;
        }

        public void Tick(IReadOnlyList<FormationActivity> activities, Action<string, PointerEventData> onGhostBeginDrag, Action<PointerEventData> onGhostDrag, Action<PointerEventData> onGhostEndDrag)
        {
            if (!redrawGate.ShouldRedraw(activities.Count)) return;

            overlayBuffer.Clear();
            movePathBuffer.Clear();
            foreach (var activity in activities)
            {
                var unit = lookupUnit(activity.UnitId);
                overlayBuffer.Add(new FormationActivityOverlayVisual(activity.TargetSlotIndex, activity, unit));

                if (activity.Kind == FormationActivityKind.Moving)
                {
                    overlayBuffer.Add(new FormationActivityOverlayVisual(activity.OriginSlotIndex, activity, unit));
                    // 이동 중인 유닛 아이콘이 경로 위를 실시간으로 지나가도록(기획 20번 §3.3) 진행률과
                    // 아이콘을 함께 넘긴다 - 정적인 출발/도착 오버레이(overlayBuffer)와는 별개다.
                    movePathBuffer.Add(new FormationMovePathVisual(activity.PathSlotIndices, activity.Progress01, unit?.Icon, activity.PartialSegmentIndex, activity.PartialSegmentWeight));
                }
            }

            view.SetMoveVisuals(movePathBuffer);
            view.SetActivityOverlays(overlayBuffer, onGhostBeginDrag, onGhostDrag, onGhostEndDrag);
        }
    }
}
