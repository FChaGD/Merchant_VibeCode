using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Assets/Table/Caravan/Wagon.xlsx·Facility.xlsx(각각 데이터 시트 + 스트링 시트)를 읽어 마차·시설 자산 4개를 덮어쓴다
    /// (Docs/설계/56번 §2.3). 데이터 행에 이름이 빠지면 마구간 화면에서 조용히 "값 없음"이 되므로, 두 시트를 모두 읽고 교차 검증을
    /// 통과한 뒤에만 자산을 쓴다(CharacterStatsTableImporter와 같은 방식). 재실행해도 안전 - 엑셀이 단일 진실 소스다.
    /// </summary>
    public static class CaravanAssetTableImporter
    {
        [MenuItem("Tools/Game/Table/Import Caravan Assets")]
        public static void Import()
        {
            ImportWorkbook("Caravan/Wagon.xlsx", "Wagon", TableAssetPaths.WagonTable, TableAssetPaths.WagonStrings);
            ImportWorkbook("Caravan/Facility.xlsx", "Facility", TableAssetPaths.FacilityTable, TableAssetPaths.FacilityStrings);

            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(CaravanAssetTableImporter)}: 임포트 완료.");
        }

        private static void ImportWorkbook(string relativePath, string sheetName, string tableAssetPath, string stringsAssetPath)
        {
            var workbookPath = Path.Combine(Application.dataPath, "Table", relativePath);
            if (!File.Exists(workbookPath))
            {
                Debug.LogError($"{nameof(CaravanAssetTableImporter)}: 워크북을 찾을 수 없다 - '{workbookPath}'.");
                return;
            }

            var entries = ReadEntries(workbookPath, sheetName);
            var names = ReadStrings(workbookPath, sheetName + "Strings");
            Validate(entries, names, sheetName);

            WriteEntries(EditorTableReader.GetOrCreateAsset<CaravanAssetTableAsset>(tableAssetPath), entries);
            WriteStrings(EditorTableReader.GetOrCreateAsset<CaravanAssetStringsTableAsset>(stringsAssetPath), names);
        }

        private static List<CaravanAssetEntry> ReadEntries(string workbookPath, string sheetName)
        {
            var rows = EditorTableReader.ReadSheet(workbookPath, sheetName);
            var entries = new List<CaravanAssetEntry>(rows.Count);
            var seenIds = new HashSet<string>();
            foreach (var row in rows)
            {
                entries.Add(new CaravanAssetEntry
                {
                    Id = EditorTableReader.ParseSlug(row, "Id", seenIds),
                    Price = EditorTableReader.ParseInt(row, "Price"),
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

        private static void Validate(List<CaravanAssetEntry> entries, List<SlugLocalizedStringEntry> names, string sheetName)
        {
            var nameIds = new HashSet<string>();
            foreach (var name in names) nameIds.Add(name.Id);

            foreach (var entry in entries)
            {
                if (!nameIds.Contains(entry.Id))
                {
                    throw new FormatException($"{sheetName}의 '{entry.Id}'에 해당하는 이름이 {sheetName}Strings 시트에 없다.");
                }
            }
        }

        private static void WriteEntries(CaravanAssetTableAsset asset, List<CaravanAssetEntry> entries)
        {
            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").stringValue = entries[i].Id;
                element.FindPropertyRelative("Price").intValue = entries[i].Price;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }

        private static void WriteStrings(CaravanAssetStringsTableAsset asset, List<SlugLocalizedStringEntry> entries)
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
