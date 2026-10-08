using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Assets/Table/Town/TownStock.xlsx를 읽어 TownStockTableAsset을 덮어쓴다(Docs/설계/81번 §3.2). 참조 무결성은 이미 임포트된 자산이
    /// 아니라 원본 엑셀(City·Wagon·Facility·TradeGoods)을 직접 읽어 검사한다 - 임포트 순서와 무관하게 같은 결과를 내기 위함이다.
    /// 마을 규모상 판매 시설이 없는 행은 여기서 막지 않는다 - 규모별 시설 배정이 Bootstrap 인스펙터에 있어 실행 시점에 경고한다(§3.4).
    /// Category 열은 정수 Id가 아니라 이름 문자열(Wagon/Facility/TradeGoods)이다 - 기획 80번 §3-8이 이름으로 정했고, 행을 읽는 사람이
    /// 바로 알아볼 수 있어야 하기 때문이다(EditorTableReader.ParseEnum은 정수 Id용이라 쓰지 않는다).
    /// </summary>
    public static class TownStockTableImporter
    {
        [MenuItem("Tools/Game/Table/Import Town Stock")]
        public static void Import()
        {
            var workbookPath = TablePath("Town/TownStock.xlsx");
            if (!File.Exists(workbookPath))
            {
                Debug.LogError($"{nameof(TownStockTableImporter)}: 워크북을 찾을 수 없다 - '{workbookPath}'.");
                return;
            }

            var entries = ReadEntries(workbookPath);
            Validate(entries);
            Write(EditorTableReader.GetOrCreateAsset<TownStockTableAsset>(TableAssetPaths.TownStockTable), entries);

            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(TownStockTableImporter)}: 임포트 완료.");
        }

        private static string TablePath(string relative) => Path.Combine(Application.dataPath, "Table", relative);

        private static List<TownStockEntry> ReadEntries(string workbookPath)
        {
            var rows = EditorTableReader.ReadSheet(workbookPath, "TownStock");
            var entries = new List<TownStockEntry>(rows.Count);
            foreach (var row in rows)
            {
                entries.Add(new TownStockEntry
                {
                    CityId = EditorTableReader.ParseInt(row, "CityId"),
                    Category = ParseCategory(row),
                    ItemId = EditorTableReader.ParseRequiredString(row, "ItemId"),
                    Quantity = EditorTableReader.ParseInt(row, "Quantity"),
                });
            }
            return entries;
        }

        private static TownStockCategory ParseCategory(IReadOnlyDictionary<string, string> row)
        {
            var text = EditorTableReader.ParseRequiredString(row, "Category").Trim();
            // 숫자 문자열("0")도 Enum.TryParse를 통과하므로 이름으로 정의된 값만 받는다.
            if (Enum.TryParse(text, ignoreCase: false, out TownStockCategory category) && Enum.IsDefined(typeof(TownStockCategory), category) && !char.IsDigit(text[0]))
            {
                return category;
            }
            throw new FormatException($"컬럼 'Category' 값 '{text}'은(는) {string.Join("/", Enum.GetNames(typeof(TownStockCategory)))} 중 하나여야 한다.");
        }

        private static void Validate(List<TownStockEntry> entries)
        {
            var cityIds = ReadIds(TablePath("City/City.xlsx"), "CityData");
            var idsByCategory = new Dictionary<TownStockCategory, HashSet<string>>
            {
                [TownStockCategory.Wagon] = ReadIds(TablePath("Caravan/Wagon.xlsx"), "Wagon"),
                [TownStockCategory.Facility] = ReadIds(TablePath("Caravan/Facility.xlsx"), "Facility"),
                [TownStockCategory.TradeGoods] = ReadIds(TablePath("Item/TradeGoods.xlsx"), "ItemData"),
            };
            var seen = new HashSet<(int, TownStockCategory, string)>();

            foreach (var entry in entries)
            {
                var label = $"TownStock 행(CityId {entry.CityId}, {entry.Category}, '{entry.ItemId}')";
                if (!cityIds.Contains(entry.CityId.ToString())) throw new FormatException($"{label}: City.xlsx CityData에 없는 CityId.");
                if (!idsByCategory[entry.Category].Contains(entry.ItemId)) throw new FormatException($"{label}: {entry.Category} 테이블에 없는 ItemId.");
                if (entry.Quantity <= 0) throw new FormatException($"{label}: Quantity는 1 이상이어야 한다.");
                if (!seen.Add((entry.CityId, entry.Category, entry.ItemId))) throw new FormatException($"{label}: 같은 (CityId, Category, ItemId) 행이 중복됐다.");
            }
        }

        // Id 열 값을 문자열로 모은다 - 정수 Id(CityData)도 같은 방식으로 비교한다(숫자 셀은 "4"처럼 읽힌다).
        private static HashSet<string> ReadIds(string workbookPath, string sheetName)
        {
            var ids = new HashSet<string>();
            foreach (var row in EditorTableReader.ReadSheet(workbookPath, sheetName))
            {
                if (row.TryGetValue("Id", out var id) && !string.IsNullOrWhiteSpace(id)) ids.Add(id.Trim());
            }
            return ids;
        }

        private static void Write(TownStockTableAsset asset, List<TownStockEntry> entries)
        {
            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("CityId").intValue = entries[i].CityId;
                EditorTableReader.SetEnumValue(element.FindPropertyRelative("Category"), entries[i].Category);
                element.FindPropertyRelative("ItemId").stringValue = entries[i].ItemId;
                element.FindPropertyRelative("Quantity").intValue = entries[i].Quantity;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }
    }
}
