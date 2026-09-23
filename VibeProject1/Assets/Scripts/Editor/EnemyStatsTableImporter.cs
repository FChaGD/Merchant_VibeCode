using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Assets/Table/Enemy/EnemyStats.xlsx(시트 3개: EnemyStats/EnemyEncounterComposition/EnemyStrings)를
    /// 읽어 EnemyStatsTableAsset/EnemyEncounterCompositionTableAsset/EnemyStringsTableAsset을
    /// 덮어쓴다(Docs/설계/18번 §7). v1의 CharacterStatsTableImporter에서 Enemy 관련 시트만 분리해
    /// 나온 임포터 - 워크북이 도메인별로 나뉘면서(기획 14번 §6.3) 자연히 갈라졌다. 재실행해도 안전.
    /// </summary>
    public static class EnemyStatsTableImporter
    {
        private const string WorkbookRelativePath = "Enemy/EnemyStats.xlsx";

        [MenuItem("Tools/Game/Table/Import Enemy Stats")]
        public static void Import()
        {
            var workbookPath = Path.Combine(Application.dataPath, "Table", WorkbookRelativePath);
            if (!File.Exists(workbookPath))
            {
                Debug.LogError($"{nameof(EnemyStatsTableImporter)}: 워크북을 찾을 수 없다 - '{workbookPath}'.");
                return;
            }

            ImportEnemyStats(workbookPath);
            ImportEnemyEncounterComposition(workbookPath);
            ImportEnemyStrings(workbookPath);

            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(EnemyStatsTableImporter)}: 임포트 완료.");
        }

        private static void ImportEnemyStats(string workbookPath)
        {
            var rows = EditorTableReader.ReadSheet(workbookPath, "EnemyStats");
            var entries = new List<EnemyStatsEntry>(rows.Count);
            var seenIds = new HashSet<string>();
            foreach (var row in rows)
            {
                entries.Add(new EnemyStatsEntry
                {
                    EnemyType = EditorTableReader.ParseSlug(row, "Id", seenIds),
                    MaxHp = EditorTableReader.ParseFloat(row, "MaxHp"),
                    Attack = EditorTableReader.ParseFloat(row, "Attack"),
                    Defense = EditorTableReader.ParseFloat(row, "Defense"),
                    MoveSpeed = EditorTableReader.ParseFloat(row, "MoveSpeed"),
                    AttackInterval = EditorTableReader.ParseFloat(row, "AttackInterval"),
                    Range = EditorTableReader.ParseFloat(row, "Range"),
                    MoraleSyncRate = EditorTableReader.ParseFloat(row, "MoraleSyncRate"),
                    HpRegenPerSecond = EditorTableReader.ParseFloat(row, "HpRegenPerSecond"),
                });
            }

            var asset = EditorTableReader.GetOrCreateAsset<EnemyStatsTableAsset>(TableAssetPaths.EnemyStatsTable);
            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                var entry = entries[i];
                element.FindPropertyRelative("EnemyType").stringValue = entry.EnemyType;
                element.FindPropertyRelative("MaxHp").floatValue = entry.MaxHp;
                element.FindPropertyRelative("Attack").floatValue = entry.Attack;
                element.FindPropertyRelative("Defense").floatValue = entry.Defense;
                element.FindPropertyRelative("MoveSpeed").floatValue = entry.MoveSpeed;
                element.FindPropertyRelative("AttackInterval").floatValue = entry.AttackInterval;
                element.FindPropertyRelative("Range").floatValue = entry.Range;
                element.FindPropertyRelative("MoraleSyncRate").floatValue = entry.MoraleSyncRate;
                element.FindPropertyRelative("HpRegenPerSecond").floatValue = entry.HpRegenPerSecond;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }

        private static void ImportEnemyEncounterComposition(string workbookPath)
        {
            var rows = EditorTableReader.ReadSheet(workbookPath, "EnemyEncounterComposition");
            var entries = new List<EnemyEncounterCompositionEntry>(rows.Count);
            var seenIds = new HashSet<string>();
            foreach (var row in rows)
            {
                entries.Add(new EnemyEncounterCompositionEntry
                {
                    EnemyType = EditorTableReader.ParseSlug(row, "Id", seenIds),
                    CountMin = EditorTableReader.ParseInt(row, "CountMin"),
                    CountMax = EditorTableReader.ParseInt(row, "CountMax"),
                });
            }

            var asset = EditorTableReader.GetOrCreateAsset<EnemyEncounterCompositionTableAsset>(TableAssetPaths.EnemyEncounterCompositionTable);
            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                var entry = entries[i];
                element.FindPropertyRelative("EnemyType").stringValue = entry.EnemyType;
                element.FindPropertyRelative("CountMin").intValue = entry.CountMin;
                element.FindPropertyRelative("CountMax").intValue = entry.CountMax;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }

        private static void ImportEnemyStrings(string workbookPath)
        {
            var rows = EditorTableReader.ReadSheet(workbookPath, "EnemyStrings");
            var asset = EditorTableReader.GetOrCreateAsset<EnemyStringsTableAsset>(TableAssetPaths.EnemyStringsTable);
            var so = new SerializedObject(asset);
            var stringsProp = so.FindProperty("strings");
            stringsProp.arraySize = rows.Count;
            var seenIds = new HashSet<string>();
            for (var i = 0; i < rows.Count; i++)
            {
                var element = stringsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").stringValue = EditorTableReader.ParseSlug(rows[i], "Id", seenIds);
                element.FindPropertyRelative("Ko").stringValue = rows[i]["Ko"];
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }
    }
}
