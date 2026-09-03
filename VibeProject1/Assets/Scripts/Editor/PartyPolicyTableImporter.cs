using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Assets/Table/Tactics/PartyPolicy.xlsx(시트 6개: PartyRecognitionOptions/PartyRadiusOptions/
    /// PartyPursuitOptions/PartyRecognitionStrings/PartyRadiusStrings/PartyPursuitStrings)를 읽어
    /// PartyTacticsPolicyCatalogAsset/PartyTacticsPolicyStringsTableAsset을 덮어쓴다(Docs/설계/18번
    /// §7). v1의 TacticsTableImporter에서 파티 3축 부분만 분리해 나온 임포터 - 워크북이 도메인별로
    /// 나뉘면서(기획 14번 §6.3) 자연히 갈라졌다. 두 에셋 모두 get-or-create(둘 다 인스펙터 수기
    /// 편집 이력이 없는 임포트 전용 에셋).
    /// </summary>
    public static class PartyPolicyTableImporter
    {
        private const string WorkbookRelativePath = "Tactics/PartyPolicy.xlsx";
        private const string CatalogAssetPath = "Assets/Prefabs/ScriptableObejct/PartyTacticsPolicyCatalog.asset";
        private const string StringsTableAssetPath = "Assets/Prefabs/ScriptableObejct/PartyTacticsPolicyStringsTable.asset";

        [MenuItem("Tools/Game/Table/Import Party Policy")]
        public static void Import()
        {
            var workbookPath = Path.Combine(Application.dataPath, "Table", WorkbookRelativePath);
            if (!File.Exists(workbookPath))
            {
                Debug.LogError($"{nameof(PartyPolicyTableImporter)}: 워크북을 찾을 수 없다 - '{workbookPath}'.");
                return;
            }

            if (!ImportCatalog(workbookPath))
            {
                return;
            }

            ImportStrings(workbookPath);

            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(PartyPolicyTableImporter)}: 임포트 완료.");
        }

        // 파티 3축(기획 14번 §3.6) - 역할군 그룹핑이 없어 SortOrder로 정렬만 하는 단순한 헬퍼를 쓴다.
        // 축마다 IsDefault가 정확히 1개여야 하며, 어긋나면(0개/2개 이상) 즉시 에러로 드러내고 임포트를
        // 중단한다(Docs/설계/17번 §10.4/§10.6 - v2에서도 동일 규칙 유지).
        private static bool ImportCatalog(string workbookPath)
        {
            var asset = EditorTableReader.GetOrCreateAsset<PartyTacticsPolicyCatalogAsset>(CatalogAssetPath);

            var recognitionOptions = ReadOptionsSortedBySortOrder<EnemyRecognitionType>(EditorTableReader.ReadSheet(workbookPath, "PartyRecognitionOptions"));
            var radiusOptions = ReadOptionsSortedBySortOrder<ActivityRadiusPreset>(EditorTableReader.ReadSheet(workbookPath, "PartyRadiusOptions"));
            var pursuitOptions = ReadOptionsSortedBySortOrder<PursuitPreset>(EditorTableReader.ReadSheet(workbookPath, "PartyPursuitOptions"));

            if (!HasExactlyOneDefault(recognitionOptions, "PartyRecognitionOptions")
                || !HasExactlyOneDefault(radiusOptions, "PartyRadiusOptions")
                || !HasExactlyOneDefault(pursuitOptions, "PartyPursuitOptions"))
            {
                return false;
            }

            var so = new SerializedObject(asset);
            WriteOptionList(so.FindProperty("recognitionOptions"), recognitionOptions);
            WriteOptionList(so.FindProperty("radiusOptions"), radiusOptions);
            WriteOptionList(so.FindProperty("pursuitOptions"), pursuitOptions);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            return true;
        }

        private static bool HasExactlyOneDefault<TEnum>(List<(TEnum Value, int SortOrder, bool IsDefault)> options, string sheetName) where TEnum : struct, System.Enum
        {
            var defaultCount = options.Count(o => o.IsDefault);
            if (defaultCount == 1) return true;

            Debug.LogError($"{nameof(PartyPolicyTableImporter)}: 시트 '{sheetName}'의 IsDefault가 {defaultCount}개다 - 정확히 1개여야 한다.");
            return false;
        }

        private static List<(TEnum Value, int SortOrder, bool IsDefault)> ReadOptionsSortedBySortOrder<TEnum>(
            IReadOnlyList<IReadOnlyDictionary<string, string>> rows) where TEnum : struct, System.Enum
        {
            var options = new List<(TEnum Value, int SortOrder, bool IsDefault)>(rows.Count);
            foreach (var row in rows)
            {
                options.Add((
                    EditorTableReader.ParseEnum<TEnum>(row, "Id"),
                    EditorTableReader.ParseInt(row, "SortOrder"),
                    EditorTableReader.ParseBool(row, "IsDefault")));
            }
            options.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            return options;
        }

        private static void WriteOptionList<TEnum>(SerializedProperty listProp, List<(TEnum Value, int SortOrder, bool IsDefault)> options) where TEnum : struct, System.Enum
        {
            listProp.arraySize = options.Count;
            for (var i = 0; i < options.Count; i++)
            {
                var element = listProp.GetArrayElementAtIndex(i);
                EditorTableReader.SetEnumValue(element.FindPropertyRelative("Value"), options[i].Value);
                element.FindPropertyRelative("SortOrder").intValue = options[i].SortOrder;
                element.FindPropertyRelative("IsDefault").boolValue = options[i].IsDefault;
            }
        }

        private static void ImportStrings(string workbookPath)
        {
            var asset = EditorTableReader.GetOrCreateAsset<PartyTacticsPolicyStringsTableAsset>(StringsTableAssetPath);
            var so = new SerializedObject(asset);
            WriteStringList(so.FindProperty("recognitionStrings"), EditorTableReader.ReadSheet(workbookPath, "PartyRecognitionStrings"));
            WriteStringList(so.FindProperty("radiusStrings"), EditorTableReader.ReadSheet(workbookPath, "PartyRadiusStrings"));
            WriteStringList(so.FindProperty("pursuitStrings"), EditorTableReader.ReadSheet(workbookPath, "PartyPursuitStrings"));
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
