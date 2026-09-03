using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 파티 3축(EnemyRecognitionType/ActivityRadiusPreset/PursuitPreset) Id→한국어 라벨(Docs/설계/18번
    /// §5.2) - PartyTacticsPolicyCatalogAsset에서 분리된 문자열 전용 테이블. 역할군 그룹핑이 없는
    /// 3축을 리스트 3개로 한 에셋에 묶는 방식은 PartyTacticsPolicyCatalogAsset과 동일 전례.
    /// PartyPolicyTableImporter가 채운다. TacticsPanel이 소비.
    /// </summary>
    [CreateAssetMenu(fileName = "PartyTacticsPolicyStringsTable", menuName = "Game/Tactics/Party Tactics Policy Strings Table")]
    public class PartyTacticsPolicyStringsTableAsset : ScriptableObject
    {
        [SerializeField] private List<LocalizedStringEntry> recognitionStrings = new();
        [SerializeField] private List<LocalizedStringEntry> radiusStrings = new();
        [SerializeField] private List<LocalizedStringEntry> pursuitStrings = new();

        public bool TryGetRecognitionLabel(EnemyRecognitionType value, out string ko) => TryGetLabel(recognitionStrings, (int)value, out ko);
        public bool TryGetRadiusLabel(ActivityRadiusPreset value, out string ko) => TryGetLabel(radiusStrings, (int)value, out ko);
        public bool TryGetPursuitLabel(PursuitPreset value, out string ko) => TryGetLabel(pursuitStrings, (int)value, out ko);

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
