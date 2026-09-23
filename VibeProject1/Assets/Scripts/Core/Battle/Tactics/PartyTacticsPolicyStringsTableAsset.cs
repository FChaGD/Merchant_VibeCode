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
        [SerializeField] private List<SlugLocalizedStringEntry> recognitionStrings = new();
        [SerializeField] private List<SlugLocalizedStringEntry> radiusStrings = new();
        [SerializeField] private List<SlugLocalizedStringEntry> pursuitStrings = new();

        public bool TryGetRecognitionLabel(string value, out string ko) => TryGetLabel(recognitionStrings, value, out ko);
        public bool TryGetRadiusLabel(string value, out string ko) => TryGetLabel(radiusStrings, value, out ko);
        public bool TryGetPursuitLabel(string value, out string ko) => TryGetLabel(pursuitStrings, value, out ko);

        private static bool TryGetLabel(List<SlugLocalizedStringEntry> strings, string id, out string ko)
        {
            if (TableEntryLookup.TryFind(strings, id, e => e.Id, out var entry))
            {
                ko = entry.Ko;
                return true;
            }

            ko = null;
            return false;
        }
    }
}
