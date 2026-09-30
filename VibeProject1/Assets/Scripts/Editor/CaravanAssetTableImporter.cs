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
            ImportWorkbook("Caravan/Wagon.xlsx", "Wagon", TableAssetPaths.WagonTable, TableAssetPaths.WagonStrings, hasCargoShape: true);
            ImportWorkbook("Caravan/Facility.xlsx", "Facility", TableAssetPaths.FacilityTable, TableAssetPaths.FacilityStrings, hasCargoShape: false);

            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(CaravanAssetTableImporter)}: 임포트 완료.");
        }

        // hasCargoShape: 적재 모양 열(CargoShape)은 마차 시트에만 있다(Docs/기획/63번 §3.1).
        private static void ImportWorkbook(string relativePath, string sheetName, string tableAssetPath, string stringsAssetPath, bool hasCargoShape)
        {
            var workbookPath = Path.Combine(Application.dataPath, "Table", relativePath);
            if (!File.Exists(workbookPath))
            {
                Debug.LogError($"{nameof(CaravanAssetTableImporter)}: 워크북을 찾을 수 없다 - '{workbookPath}'.");
                return;
            }

            var entries = ReadEntries(workbookPath, sheetName, hasCargoShape);
            var names = ReadStrings(workbookPath, sheetName + "Strings");
            Validate(entries, names, sheetName);

            WriteEntries(EditorTableReader.GetOrCreateAsset<CaravanAssetTableAsset>(tableAssetPath), entries);
            WriteStrings(EditorTableReader.GetOrCreateAsset<CaravanAssetStringsTableAsset>(stringsAssetPath), names);
        }

        private static List<CaravanAssetEntry> ReadEntries(string workbookPath, string sheetName, bool hasCargoShape)
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
                    AreaShape = EditorTableReader.ParseRequiredString(row, "AreaShape"),
                    CargoShape = hasCargoShape ? EditorTableReader.ParseRequiredString(row, "CargoShape") : string.Empty,
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
                // 대열 영역 모양은 런타임 카탈로그와 같은 파서로 검증한다(설계 66번 §3) - 형식이 틀리면 카탈로그가 기준 칸 1칸으로 줄여 버린다.
                if (!FormationAreaShape.TryParse(entry.AreaShape, out _, out var areaError))
                {
                    throw new FormatException($"{sheetName}의 '{entry.Id}' 대열 영역 모양(AreaShape) 오류: {areaError}");
                }
                // 적재 모양은 런타임 카탈로그와 같은 파서로 검증한다(설계 64번 §2.2) - 형식이 틀린 마차는 조용히 적재 공간 없이 시작하게 된다.
                if (!string.IsNullOrEmpty(entry.CargoShape) && !InventoryShape.TryParse(entry.CargoShape, out _, out var shapeError))
                {
                    throw new FormatException($"{sheetName}의 '{entry.Id}' 적재 모양(CargoShape) 오류: {shapeError}");
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
                element.FindPropertyRelative("AreaShape").stringValue = entries[i].AreaShape;
                element.FindPropertyRelative("CargoShape").stringValue = entries[i].CargoShape;
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
