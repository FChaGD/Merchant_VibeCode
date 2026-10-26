using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 대열에서 유닛 칸만 비운다 - 상행 종료의 사망자 제거(설계 81 §6.4)와 이벤트 이탈(설계 89 §5.1)이 같은 조작을 쓴다. 캐릭터는 대열 연결(마차·시설 영역)과
    /// 무관하므로 영역 검사가 필요 없다. 바뀐 칸이 있을 때만 저장소에 반영한다.
    /// </summary>
    public static class FormationLayoutClearing
    {
        public static bool ClearUnits(IFormationRepository formation, ICollection<string> unitIds)
        {
            if (formation == null || unitIds == null || unitIds.Count == 0 || !formation.TryLoadCurrent(out var current) || current == null) return false;

            var layout = current.Clone();
            var changed = false;
            for (var i = 0; i < layout.SlotCount; i++)
            {
                if (layout.GetUnitId(i) is { } id && unitIds.Contains(id))
                {
                    layout.Clear(i);
                    changed = true;
                }
            }
            if (changed) formation.Apply(layout);
            return changed;
        }
    }
}
