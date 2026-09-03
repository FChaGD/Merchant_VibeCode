using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 역할군 축 3종(TargetPriority/LocalPositioning/SelfPreservation) Id→한국어 라벨(Docs/설계/18번
    /// §5.2) - RoleGroupTacticsCatalogAsset에서 분리된 문자열 전용 테이블. RoleGroup 그룹핑과 무관하게
    /// enum 값 자체에 대한 전역 라벨이라(축 값 하나는 여러 역할군이 공유할 수 있음) RoleGroup별로
    /// 나누지 않는다. RoleGroupTacticsTableImporter가 채운다. TacticsPanel이 소비.
    /// </summary>
    [CreateAssetMenu(fileName = "RoleGroupTacticsStringsTable", menuName = "Game/Tactics/Role Group Tactics Strings Table")]
    public class RoleGroupTacticsStringsTableAsset : ScriptableObject
    {
        [SerializeField] private List<LocalizedStringEntry> targetPriorityStrings = new();
        [SerializeField] private List<LocalizedStringEntry> positioningStrings = new();
        [SerializeField] private List<LocalizedStringEntry> selfPreservationStrings = new();

        public bool TryGetTargetPriorityLabel(TargetPriority value, out string ko) => TryGetLabel(targetPriorityStrings, (int)value, out ko);
        public bool TryGetPositioningLabel(LocalPositioning value, out string ko) => TryGetLabel(positioningStrings, (int)value, out ko);
        public bool TryGetSelfPreservationLabel(SelfPreservation value, out string ko) => TryGetLabel(selfPreservationStrings, (int)value, out ko);

        private static bool TryGetLabel(List<LocalizedStringEntry> strings, int id, out string ko)
        {
            foreach (var entry in strings)
            {
                if (entry.Id == id)
                {
                    ko = entry.Ko;
                    return true;
                }
            }

            ko = null;
            return false;
        }
    }
}
