using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Assets/Table/Character/CharacterStats.xlsx(시트 2개: CharacterStats/CharacterStrings)를 읽어
    /// CharacterStatsTableAsset/CharacterStringsTableAsset을 덮어쓴다(Docs/설계/18번 §7). v1에서는
    /// 이 임포터가 Enemy 시트까지 함께 읽었으나, 워크북이 도메인별로 분리되면서(기획 14번 §6.3)
    /// Enemy 쪽은 EnemyStatsTableImporter로 옮겨갔다. 재실행해도 안전 - 대상 에셋이 없으면 새로
    /// 만들고, 있으면 항상 엑셀 최신 내용으로 완전히 덮어쓴다(엑셀이 단일 진실 소스).
    /// </summary>
    public static class CharacterStatsTableImporter
    {
        private const string WorkbookRelativePath = "Character/CharacterStats.xlsx";
        private const string AssetFolder = "Assets/Prefabs/ScriptableObejct";

        [MenuItem("Tools/Game/Table/Import Character Stats")]
        public static void Import()
        {
            var workbookPath = Path.Combine(Application.dataPath, "Table", WorkbookRelativePath);
            if (!File.Exists(workbookPath))
            {
                Debug.LogError($"{nameof(CharacterStatsTableImporter)}: 워크북을 찾을 수 없다 - '{workbookPath}'.");
                return;
            }

            ImportCharacterStats(workbookPath);
            ImportCharacterStrings(workbookPath);

            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(CharacterStatsTableImporter)}: 임포트 완료.");
        }

        private static void ImportCharacterStats(string workbookPath)
        {
            var rows = EditorTableReader.ReadSheet(workbookPath, "CharacterStats");
            var entries = new List<CharacterStatsEntry>(rows.Count);
            foreach (var row in rows)
            {
                entries.Add(new CharacterStatsEntry
                {
                    MercenaryClass = EditorTableReader.ParseEnum<MercenaryClass>(row, "Id"),
                    MaxHp = EditorTableReader.ParseFloat(row, "MaxHp"),
                    Attack = EditorTableReader.ParseFloat(row, "Attack"),
                    Defense = EditorTableReader.ParseFloat(row, "Defense"),
                    MoveSpeed = EditorTableReader.ParseFloat(row, "MoveSpeed"),
                    AttackInterval = EditorTableReader.ParseFloat(row, "AttackInterval"),
                    Range = EditorTableReader.ParseFloat(row, "Range"),
                    MoraleSyncRate = EditorTableReader.ParseFloat(row, "MoraleSyncRate"),
                });
            }

            var asset = EditorTableReader.GetOrCreateAsset<CharacterStatsTableAsset>($"{AssetFolder}/CharacterStatsTable.asset");
            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                var entry = entries[i];
                EditorTableReader.SetEnumValue(element.FindPropertyRelative("MercenaryClass"), entry.MercenaryClass);
                element.FindPropertyRelative("MaxHp").floatValue = entry.MaxHp;
                element.FindPropertyRelative("Attack").floatValue = entry.Attack;
                element.FindPropertyRelative("Defense").floatValue = entry.Defense;
                element.FindPropertyRelative("MoveSpeed").floatValue = entry.MoveSpeed;
                element.FindPropertyRelative("AttackInterval").floatValue = entry.AttackInterval;
                element.FindPropertyRelative("Range").floatValue = entry.Range;
                element.FindPropertyRelative("MoraleSyncRate").floatValue = entry.MoraleSyncRate;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }

        private static void ImportCharacterStrings(string workbookPath)
        {
            var rows = EditorTableReader.ReadSheet(workbookPath, "CharacterStrings");
            var asset = EditorTableReader.GetOrCreateAsset<CharacterStringsTableAsset>($"{AssetFolder}/CharacterStringsTable.asset");
            var so = new SerializedObject(asset);
            var stringsProp = so.FindProperty("strings");
            stringsProp.arraySize = rows.Count;
            for (var i = 0; i < rows.Count; i++)
            {
                var element = stringsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").intValue = EditorTableReader.ParseInt(rows[i], "Id");
                element.FindPropertyRelative("Ko").stringValue = rows[i]["Ko"];
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }
    }
}
