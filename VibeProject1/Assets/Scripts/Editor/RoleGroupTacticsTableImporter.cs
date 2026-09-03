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
        private const string RoleGroupMapAssetPath = "Assets/Prefabs/ScriptableObejct/MercenaryRoleGroupMap.asset";
        private const string TacticsCatalogAssetPath = "Assets/Prefabs/ScriptableObejct/RoleGroupTacticsCatalog.asset";
        private const string StringsTableAssetPath = "Assets/Prefabs/ScriptableObejct/RoleGroupTacticsStringsTable.asset";

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
            var asset = AssetDatabase.LoadAssetAtPath<MercenaryRoleGroupMapAsset>(RoleGroupMapAssetPath);
            if (asset == null)
            {
                Debug.LogError($"{nameof(RoleGroupTacticsTableImporter)}: '{RoleGroupMapAssetPath}' 에셋을 찾을 수 없다.");
                return false;
            }

            var rows = EditorTableReader.ReadSheet(workbookPath, "MercenaryRoleGroupMap");
            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = rows.Count;
            for (var i = 0; i < rows.Count; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                EditorTableReader.SetEnumValue(element.FindPropertyRelative("MercenaryClass"), EditorTableReader.ParseEnum<MercenaryClass>(rows[i], "CharacterId"));
                EditorTableReader.SetEnumValue(element.FindPropertyRelative("RoleGroup"), EditorTableReader.ParseEnum<RoleGroup>(rows[i], "RoleGroupId"));
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            return true;
        }

        private static bool ImportTacticsCatalog(string workbookPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<RoleGroupTacticsCatalogAsset>(TacticsCatalogAssetPath);
            if (asset == null)
            {
                Debug.LogError($"{nameof(RoleGroupTacticsTableImporter)}: '{TacticsCatalogAssetPath}' 에셋을 찾을 수 없다.");
                return false;
            }

            var targetPriorityByRole = GroupByRoleGroup<TargetPriority>(EditorTableReader.ReadSheet(workbookPath, "RoleGroupTargetPriorityOptions"));
            var positioningByRole = GroupByRoleGroup<LocalPositioning>(EditorTableReader.ReadSheet(workbookPath, "RoleGroupPositioningOptions"));
            // 시트명 "RoleGroupSelfPreservationOptions"(기획 14번 §6.3 표기)는 32자라 Excel의 시트명
            // 31자 제한을 넘는다 - 실제 워크북 생성 중 발견(v1의 코드페이지 이슈와 같은 성격의
            // 구현 단계 보정). "RoleGroupSelfPreserveOptions"로 축약.
            var selfPreservationByRole = GroupByRoleGroup<SelfPreservation>(EditorTableReader.ReadSheet(workbookPath, "RoleGroupSelfPreserveOptions"));

            var roleGroups = new HashSet<RoleGroup>();
            roleGroups.UnionWith(targetPriorityByRole.Keys);
            roleGroups.UnionWith(positioningByRole.Keys);
            roleGroups.UnionWith(selfPreservationByRole.Keys);
            var orderedRoleGroups = roleGroups.OrderBy(rg => (int)rg).ToList();

            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = orderedRoleGroups.Count;
            for (var i = 0; i < orderedRoleGroups.Count; i++)
            {
                var roleGroup = orderedRoleGroups[i];
                var element = entriesProp.GetArrayElementAtIndex(i);
                EditorTableReader.SetEnumValue(element.FindPropertyRelative("RoleGroup"), roleGroup);
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
        private static Dictionary<RoleGroup, List<TEnum>> GroupByRoleGroup<TEnum>(
            IReadOnlyList<IReadOnlyDictionary<string, string>> rows) where TEnum : struct, System.Enum
        {
            var grouped = new Dictionary<RoleGroup, List<(int SortOrder, TEnum Value)>>();
            foreach (var row in rows)
            {
                var roleGroup = EditorTableReader.ParseEnum<RoleGroup>(row, "RoleGroupId");
                var value = EditorTableReader.ParseEnum<TEnum>(row, "OptionId");
                var sortOrder = EditorTableReader.ParseInt(row, "SortOrder");

                if (!grouped.TryGetValue(roleGroup, out var list))
                {
                    list = new List<(int, TEnum)>();
                    grouped[roleGroup] = list;
                }
                list.Add((sortOrder, value));
            }

            var result = new Dictionary<RoleGroup, List<TEnum>>();
            foreach (var pair in grouped)
            {
                pair.Value.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
                result[pair.Key] = pair.Value.ConvertAll(x => x.Value);
            }
            return result;
        }

        private static List<TEnum> GetOrEmpty<TEnum>(Dictionary<RoleGroup, List<TEnum>> byRole, RoleGroup roleGroup) where TEnum : struct, System.Enum
        {
            return byRole.TryGetValue(roleGroup, out var list) ? list : new List<TEnum>();
        }

        private static void WriteOptionList<TEnum>(SerializedProperty listProp, List<TEnum> options) where TEnum : struct, System.Enum
        {
            listProp.arraySize = options.Count;
            for (var i = 0; i < options.Count; i++)
            {
                EditorTableReader.SetEnumValue(listProp.GetArrayElementAtIndex(i).FindPropertyRelative("Value"), options[i]);
            }
        }

        private static void ImportStrings(string workbookPath)
        {
            var asset = EditorTableReader.GetOrCreateAsset<RoleGroupTacticsStringsTableAsset>(StringsTableAssetPath);
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
            for (var i = 0; i < rows.Count; i++)
            {
                var element = listProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").intValue = EditorTableReader.ParseInt(rows[i], "Id");
                element.FindPropertyRelative("Ko").stringValue = rows[i]["Ko"];
            }
        }
    }
}
