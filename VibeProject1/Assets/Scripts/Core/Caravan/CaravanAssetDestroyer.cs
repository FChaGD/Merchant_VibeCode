using System;
using System.Collections.Generic;

namespace Game.Core
{
    public readonly struct CaravanAssetDestructionResult
    {
        public int Relocated { get; }
        public int Released { get; }
        /// <summary>정리 후 마차 덩어리가 2개 이상 - 대열 정리 단계 대상.</summary>
        public bool Disconnected { get; }

        public CaravanAssetDestructionResult(int relocated, int released, bool disconnected)
        {
            Relocated = relocated;
            Released = released;
            Disconnected = disconnected;
        }
    }

    /// <summary>
    /// 파괴된 마차·시설을 대열에서 빼고(재배치·활동 취소) 보유 목록에서 제거한다(Docs/설계/91번 §4.4). 전투 정산과 이벤트 효과(설계 91번 §6.2)가
    /// 같은 순서를 쓰도록 정산기에서 뽑았다. 순서 제약: 대열 조회는 보유 제거 **전에** 만든다 - 뺀 뒤엔 파괴 개체를 마차·시설로 인식하지 못해
    /// 그 칸의 영역을 계산하지 못한다. 의존성은 모두 선택적이다 - 없는 쪽 처리만 건너뛴다(배틀 테스트 씬 등).
    /// 내구도 기록은 보유 변경 알림으로 원장이 지우므로 여기서 다루지 않는다.
    /// </summary>
    public sealed class CaravanAssetDestroyer
    {
        private readonly IFormationRepository formation;
        private readonly ICaravanRosterProvider roster;
        private readonly IFieldFormationActivityRepository activities;
        private readonly IOwnedCaravanAssetRemover remover;

        public CaravanAssetDestroyer(IFormationRepository formation, ICaravanRosterProvider roster, IFieldFormationActivityRepository activities, IOwnedCaravanAssetRemover remover)
        {
            this.formation = formation;
            this.roster = roster;
            this.activities = activities;
            this.remover = remover;
        }

        public CaravanAssetDestructionResult Destroy(IReadOnlyCollection<string> ids)
        {
            ids ??= Array.Empty<string>();
            int relocated = 0, released = 0;
            var disconnected = false;

            if (formation != null && formation.TryLoadCurrent(out var layout) && layout != null)
            {
                var lookup = FormationAreaRules.LookupFrom(roster);
                if (ids.Count > 0)
                {
                    IReadOnlyList<FormationActivity> active = activities != null ? activities.ActiveActivities : Array.Empty<FormationActivity>();
                    var relocation = FormationWreckRelocator.Relocate(layout, ids, lookup, active);
                    formation.Apply(relocation.Layout);

                    if (activities != null)
                    {
                        foreach (var unitId in relocation.CancelledActivityUnitIds)
                        {
                            if (activities.IsUnitBusy(unitId)) activities.Cancel(unitId);
                        }
                    }

                    relocated = relocation.Relocated.Count;
                    released = relocation.Released.Count;
                    disconnected = relocation.ComponentCount > 1;
                }
                else
                {
                    // 파괴가 없어도 이미 끊어진 대열(이전 궤주 뒤 등)이면 대열 정리 단계 대상이다.
                    disconnected = !FormationArea.Compute(layout, lookup).WagonsConnected;
                }
            }

            // 보유 목록 제거 - 물류품 섹션·내구도 기록도 보유 변경 알림으로 함께 사라진다.
            if (remover != null)
            {
                foreach (var id in ids) remover.TryRemoveOwned(id);
            }

            return new CaravanAssetDestructionResult(relocated, released, disconnected);
        }
    }
}
