using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Assets/Table/Tactics/RoleGroupTactics.xlsx(시트 7개: MercenaryRoleGroupMap/
    /// RoleGroupTargetPriorityOptions/RoleGroupPositioningOptions/RoleGroupSelfPreserveOptions(Excel
    /// 시트명 31자 제한으로 축약, §ImportTacticsCatalog 참고)/TargetPriorityStrings/PositioningStrings/
    /// SelfPreservationStrings)를 읽어
    /// MercenaryRoleGroupMapAsset/RoleGroupTacticsCatalogAsset/RoleGroupTacticsStringsTableAsset을
    /// 덮어쓴다(Docs/설계/18번 §7). v1의 TacticsTableImporter에서 역할군 축 관련 부분만 남기고
    /// 파티 3축(PartyPolicyTableImporter)을 분리해 개명한 것 - 워크북이 도메인별로 나뉘면서
    /// (기획 14번 §6.3) 자연히 갈라졌다. 앞의 두 에셋은 이미 존재하므로(수기 편집으로 만들어짐)
    /// 새로 만들지 않고 로드만 한다 - 없으면 경고 후 중단. StringsTable은 이번에 처음 생기는
    /// 에셋이라 get-or-create.
    /// </summary>
    public static class RoleGroupTacticsTableImporter
    {
        private const string WorkbookRelativePath = "Tactics/RoleGroupTactics.xlsx";

        [MenuItem("Tools/Game/Table/Import Role Group Tactics")]
        public static void Import()
        {
            var workbookPath = Path.Combine(Application.dataPath, "Table", WorkbookRelativePath);
            if (!File.Exists(workbookPath))
            {
                Debug.LogError($"{nameof(RoleGroupTacticsTableImporter)}: 워크북을 찾을 수 없다 - '{workbookPath}'.");
                return;
            }

            if (!ImportRoleGroupMap(workbookPath) || !ImportTacticsCatalog(workbookPath))
            {
                return;
            }

            ImportStrings(workbookPath);

            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(RoleGroupTacticsTableImporter)}: 임포트 완료.");
        }

        private static bool ImportRoleGroupMap(string workbookPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<MercenaryRoleGroupMapAsset>(TableAssetPaths.MercenaryRoleGroupMap);
            if (asset == null)
            {
                Debug.LogError($"{nameof(RoleGroupTacticsTableImporter)}: '{TableAssetPaths.MercenaryRoleGroupMap}' 에셋을 찾을 수 없다.");
                return false;
            }

            var rows = EditorTableReader.ReadSheet(workbookPath, "MercenaryRoleGroupMap");
            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = rows.Count;
            // 캐릭터 하나는 정확히 한 역할군에만 매핑돼야 하므로 CharacterId는 이 시트 안에서
            // 유일해야 한다(seenCharacterIds로 검증). RoleGroupId는 여러 캐릭터가 같은 역할군을
            // 공유하는 게 정상이라 중복 검증 대상이 아니다(빈 값 검증만 필요해 매 행 새 HashSet으로
            // ParseSlug의 중복 체크를 우회).
            var seenCharacterIds = new HashSet<string>();
            for (var i = 0; i < rows.Count; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("MercenaryClass").stringValue = EditorTableReader.ParseSlug(rows[i], "CharacterId", seenCharacterIds);
                element.FindPropertyRelative("RoleGroup").stringValue = EditorTableReader.ParseSlug(rows[i], "RoleGroupId", new HashSet<string>());
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            return true;
        }

        private static bool ImportTacticsCatalog(string workbookPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<RoleGroupTacticsCatalogAsset>(TableAssetPaths.RoleGroupTacticsCatalog);
            if (asset == null)
            {
                Debug.LogError($"{nameof(RoleGroupTacticsTableImporter)}: '{TableAssetPaths.RoleGroupTacticsCatalog}' 에셋을 찾을 수 없다.");
                return false;
            }

            var targetPriorityByRole = GroupByRoleGroup(EditorTableReader.ReadSheet(workbookPath, "RoleGroupTargetPriorityOptions"));
            var positioningByRole = GroupByRoleGroup(EditorTableReader.ReadSheet(workbookPath, "RoleGroupPositioningOptions"));
            // 시트명 "RoleGroupSelfPreservationOptions"(기획 14번 §6.3 표기)는 32자라 Excel의 시트명
            // 31자 제한을 넘는다 - 실제 워크북 생성 중 발견(v1의 코드페이지 이슈와 같은 성격의
            // 구현 단계 보정). "RoleGroupSelfPreserveOptions"로 축약.
            var selfPreservationByRole = GroupByRoleGroup(EditorTableReader.ReadSheet(workbookPath, "RoleGroupSelfPreserveOptions"));

            var roleGroups = new HashSet<string>();
            roleGroups.UnionWith(targetPriorityByRole.Keys);
            roleGroups.UnionWith(positioningByRole.Keys);
            roleGroups.UnionWith(selfPreservationByRole.Keys);
            // 정수 Id가 사라져 (int)rg 정렬을 못 쓴다 - 문자열 순서 정렬로 대체(표시 순서 자체는
            // 이미 SortOrder로 축마다 확정되므로, 역할군 나열 순서는 임의로 안정적이기만 하면 된다).
            var orderedRoleGroups = roleGroups.OrderBy(rg => rg, System.StringComparer.Ordinal).ToList();

            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = orderedRoleGroups.Count;
            for (var i = 0; i < orderedRoleGroups.Count; i++)
            {
                var roleGroup = orderedRoleGroups[i];
                var element = entriesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("RoleGroup").stringValue = roleGroup;
                WriteOptionList(element.FindPropertyRelative("TargetPriorityOptions"), GetOrEmpty(targetPriorityByRole, roleGroup));
                WriteOptionList(element.FindPropertyRelative("PositioningOptions"), GetOrEmpty(positioningByRole, roleGroup));
                WriteOptionList(element.FindPropertyRelative("SelfPreservationOptions"), GetOrEmpty(selfPreservationByRole, roleGroup));
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            return true;
        }

        // RoleGroup별로 묶고 SortOrder로 정렬한다 - 목록 순서 = 드롭다운 표시 순서 = override 초기값
        // (기획 12번 §2.1, 기획 14번 §3.5). 시트에 나열된 SortOrder를 그대로 신뢰한다(물리적 행 순서
        // 의존 금지). v2부터 DisplayLabel 컬럼이 없다 - 값(Id)만 읽는다.
        private static Dictionary<string, List<string>> GroupByRoleGroup(
            IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
        {
            var grouped = new Dictionary<string, List<(int SortOrder, string Value)>>();
            // RoleGroupId는 역할군마다 여러 행이 반복되는 그룹 키라 중복 검증 대상이 아니고,
            // OptionId도 같은 역할군 안에서만 유일하면 되는데(TargetPriority/Positioning/
            // SelfPreservation 각 축의 옵션 문자열) 이 시트 전체를 대상으로 한 ParseSlug 유일성
            // 검증은 과하다 - 둘 다 매 행 새 HashSet으로 ParseSlug의 중복 체크를 우회하고 빈 값
            // 검증만 받는다.
            foreach (var row in rows)
            {
                var roleGroup = EditorTableReader.ParseSlug(row, "RoleGroupId", new HashSet<string>());
                var value = EditorTableReader.ParseSlug(row, "OptionId", new HashSet<string>());
                var sortOrder = EditorTableReader.ParseInt(row, "SortOrder");

                if (!grouped.TryGetValue(roleGroup, out var list))
                {
                    list = new List<(int, string)>();
                    grouped[roleGroup] = list;
                }
                list.Add((sortOrder, value));
            }

            var result = new Dictionary<string, List<string>>();
            foreach (var pair in grouped)
            {
                pair.Value.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
                result[pair.Key] = pair.Value.ConvertAll(x => x.Value);
            }
            return result;
        }

        private static List<string> GetOrEmpty(Dictionary<string, List<string>> byRole, string roleGroup)
        {
            return byRole.TryGetValue(roleGroup, out var list) ? list : new List<string>();
        }

        private static void WriteOptionList(SerializedProperty listProp, List<string> options)
        {
            listProp.arraySize = options.Count;
            for (var i = 0; i < options.Count; i++)
            {
                listProp.GetArrayElementAtIndex(i).FindPropertyRelative("Value").stringValue = options[i];
            }
        }

        private static void ImportStrings(string workbookPath)
        {
            var asset = EditorTableReader.GetOrCreateAsset<RoleGroupTacticsStringsTableAsset>(TableAssetPaths.RoleGroupTacticsStringsTable);
            var so = new SerializedObject(asset);
            WriteStringList(so.FindProperty("targetPriorityStrings"), EditorTableReader.ReadSheet(workbookPath, "TargetPriorityStrings"));
            WriteStringList(so.FindProperty("positioningStrings"), EditorTableReader.ReadSheet(workbookPath, "PositioningStrings"));
            WriteStringList(so.FindProperty("selfPreservationStrings"), EditorTableReader.ReadSheet(workbookPath, "SelfPreservationStrings"));
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }

        private static void WriteStringList(SerializedProperty listProp, IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
        {
            listProp.arraySize = rows.Count;
            var seenIds = new HashSet<string>();
            for (var i = 0; i < rows.Count; i++)
            {
                var element = listProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").stringValue = EditorTableReader.ParseSlug(rows[i], "Id", seenIds);
                element.FindPropertyRelative("Ko").stringValue = rows[i]["Ko"];
            }
        }
    }
}
