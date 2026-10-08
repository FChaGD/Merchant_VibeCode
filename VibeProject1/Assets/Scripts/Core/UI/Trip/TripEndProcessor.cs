using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 상행 종료(마을 도착 — 정상 도착·궤주 귀환) 처리(Docs/설계/81번 §6.4). 사망 캐릭터를 배치·상단에서 영구히 빼고 사망 기록에 남긴 뒤
    /// 전원 상태를 초기화한다. 초기화가 사망 플래그를 지우므로 제거를 먼저 한다. 의존성은 전부 선택적 - 없으면 그 단계만 건너뛴다.
    /// </summary>
    public sealed class TripEndProcessor
    {
        private readonly ICaravanRosterProvider roster;
        private readonly IUnitConditionRepository conditions;
        private readonly IFormationRepository formation;
        private readonly IHiredCharacterRemover remover;
        private readonly IDeceasedCharacterRecorder deceased;

        public TripEndProcessor(ICaravanRosterProvider roster, IUnitConditionRepository conditions, IFormationRepository formation, IHiredCharacterRemover remover, IDeceasedCharacterRecorder deceased)
        {
            this.roster = roster;
            this.conditions = conditions;
            this.formation = formation;
            this.remover = remover;
            this.deceased = deceased;
        }

        public void Finish()
        {
            var deadIds = CollectDead();
            if (deadIds.Count > 0)
            {
                ClearFromFormation(deadIds);
                foreach (var id in deadIds)
                {
                    if (remover != null && remover.TryRemoveHired(id)) deceased?.Record(id);
                }
            }
            conditions?.ResetAllToFull();
        }

        // 로스터를 순회하는 동안 제거하지 않도록 먼저 모은다.
        private List<string> CollectDead()
        {
            var ids = new List<string>();
            if (roster == null || conditions == null) return ids;
            foreach (var unit in roster.GetRoster())
            {
                if (unit is IMercenaryUnit && conditions.IsDead(unit.Id)) ids.Add(unit.Id);
            }
            return ids;
        }

        // 캐릭터는 대열 연결(마차·시설 영역)과 무관하므로 칸만 비운다.
        private void ClearFromFormation(List<string> deadIds)
        {
            if (formation == null || !formation.TryLoadCurrent(out var current) || current == null) return;

            var layout = current.Clone();
            var dead = new HashSet<string>(deadIds);
            var changed = false;
            for (var i = 0; i < layout.SlotCount; i++)
            {
                if (layout.GetUnitId(i) is { } id && dead.Contains(id))
                {
                    layout.Clear(i);
                    changed = true;
                }
            }
            if (changed) formation.Apply(layout);
        }
    }
}
