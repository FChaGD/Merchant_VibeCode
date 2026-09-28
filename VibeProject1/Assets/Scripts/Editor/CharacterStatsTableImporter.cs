using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Assets/Table/Character/CharacterStats.xlsx(시트 3개: CharacterStats/CharacterStrings/MercenaryClassStrings)를 읽어
    /// CharacterStatsTableAsset/CharacterStringsTableAsset/MercenaryClassStringsTableAsset을 덮어쓴다(Docs/설계/18번 §7,
    /// 54번 §2.3). 행 단위가 캐릭터라 캐릭터 → 이름, 캐릭터 → 직업 → 직업명 연결이 끊기면 고용 화면에서 조용히 빈 값이 된다 -
    /// 세 시트를 모두 읽고 교차 검증을 통과한 뒤에만 자산을 쓴다(일부만 갱신된 불일치 상태를 남기지 않음).
    /// 재실행해도 안전 - 대상 에셋이 없으면 새로 만들고, 있으면 항상 엑셀 최신 내용으로 완전히 덮어쓴다(엑셀이 단일 진실 소스).
    /// </summary>
    public static class CharacterStatsTableImporter
    {
        private const string WorkbookRelativePath = "Character/CharacterStats.xlsx";

        [MenuItem("Tools/Game/Table/Import Character Stats")]
        public static void Import()
        {
            var workbookPath = Path.Combine(Application.dataPath, "Table", WorkbookRelativePath);
            if (!File.Exists(workbookPath))
            {
                Debug.LogError($"{nameof(CharacterStatsTableImporter)}: 워크북을 찾을 수 없다 - '{workbookPath}'.");
                return;
            }

            var stats = ReadCharacterStats(workbookPath);
            var names = ReadStrings(workbookPath, "CharacterStrings");
            var classNames = ReadStrings(workbookPath, "MercenaryClassStrings");
            Validate(stats, names, classNames);

            WriteCharacterStats(stats);
            WriteStrings(EditorTableReader.GetOrCreateAsset<CharacterStringsTableAsset>(TableAssetPaths.CharacterStringsTable), names);
            WriteStrings(EditorTableReader.GetOrCreateAsset<MercenaryClassStringsTableAsset>(TableAssetPaths.MercenaryClassStringsTable), classNames);

            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(CharacterStatsTableImporter)}: 임포트 완료.");
        }

        private static List<CharacterStatsEntry> ReadCharacterStats(string workbookPath)
        {
            var rows = EditorTableReader.ReadSheet(workbookPath, "CharacterStats");
            var entries = new List<CharacterStatsEntry>(rows.Count);
            var seenIds = new HashSet<string>();
            foreach (var row in rows)
            {
                entries.Add(new CharacterStatsEntry
                {
                    Id = EditorTableReader.ParseSlug(row, "Id", seenIds),
                    MercenaryClass = EditorTableReader.ParseRequiredString(row, "Class"),
                    MaxHp = EditorTableReader.ParseFloat(row, "MaxHp"),
                    Attack = EditorTableReader.ParseFloat(row, "Attack"),
                    Defense = EditorTableReader.ParseFloat(row, "Defense"),
                    MoveSpeed = EditorTableReader.ParseFloat(row, "MoveSpeed"),
                    AttackInterval = EditorTableReader.ParseFloat(row, "AttackInterval"),
                    Range = EditorTableReader.ParseFloat(row, "Range"),
                    MoraleSyncRate = EditorTableReader.ParseFloat(row, "MoraleSyncRate"),
                    HireCost = EditorTableReader.ParseInt(row, "HireCost"),
                });
            }
            return entries;
        }

        private static List<SlugLocalizedStringEntry> ReadStrings(string workbookPath, string sheetName)
        {
            var rows = EditorTableReader.ReadSheet(workbookPath, sheetName);
            var entries = new List<SlugLocalizedStringEntry>(rows.Count);
            var seenIds = new HashSet<string>();
            foreach (var row in rows)
            {
                entries.Add(new SlugLocalizedStringEntry
                {
                    Id = EditorTableReader.ParseSlug(row, "Id", seenIds),
                    Ko = EditorTableReader.ParseRequiredString(row, "Ko"),
                });
            }
            return entries;
        }

        private static void Validate(List<CharacterStatsEntry> stats, List<SlugLocalizedStringEntry> names, List<SlugLocalizedStringEntry> classNames)
        {
            var nameIds = new HashSet<string>();
            foreach (var name in names) nameIds.Add(name.Id);
            var classIds = new HashSet<string>();
            foreach (var className in classNames) classIds.Add(className.Id);

            foreach (var entry in stats)
            {
                if (!nameIds.Contains(entry.Id))
                {
                    throw new FormatException($"CharacterStats의 '{entry.Id}'에 해당하는 이름이 CharacterStrings 시트에 없다.");
                }
                if (!classIds.Contains(entry.MercenaryClass))
                {
                    throw new FormatException($"CharacterStats '{entry.Id}'의 직업 '{entry.MercenaryClass}'이(가) MercenaryClassStrings 시트에 없다.");
                }
            }
        }

        private static void WriteCharacterStats(List<CharacterStatsEntry> entries)
        {
            var asset = EditorTableReader.GetOrCreateAsset<CharacterStatsTableAsset>(TableAssetPaths.CharacterStatsTable);
            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                var entry = entries[i];
                element.FindPropertyRelative("Id").stringValue = entry.Id;
                element.FindPropertyRelative("MercenaryClass").stringValue = entry.MercenaryClass;
                element.FindPropertyRelative("MaxHp").floatValue = entry.MaxHp;
                element.FindPropertyRelative("Attack").floatValue = entry.Attack;
                element.FindPropertyRelative("Defense").floatValue = entry.Defense;
                element.FindPropertyRelative("MoveSpeed").floatValue = entry.MoveSpeed;
                element.FindPropertyRelative("AttackInterval").floatValue = entry.AttackInterval;
                element.FindPropertyRelative("Range").floatValue = entry.Range;
                element.FindPropertyRelative("MoraleSyncRate").floatValue = entry.MoraleSyncRate;
                element.FindPropertyRelative("HireCost").intValue = entry.HireCost;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }

        private static void WriteStrings(ScriptableObject asset, List<SlugLocalizedStringEntry> entries)
        {
            var so = new SerializedObject(asset);
            var stringsProp = so.FindProperty("strings");
            stringsProp.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var element = stringsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").stringValue = entries[i].Id;
                element.FindPropertyRelative("Ko").stringValue = entries[i].Ko;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }
    }
}
