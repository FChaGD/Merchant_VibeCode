using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// 아이템 워크북 전체(공용 Item.xlsx + 카테고리 워크북 5개)를 한 번에 임포트한다(Docs/설계/50번 §4).
    /// 엑셀은 공용/카테고리 × 데이터/세부 데이터/스트링으로 나뉘어 있지만(기획 49번 §3.1), 런타임 자산은 카테고리별
    /// 형태(ItemDefinitionTableAsset/ItemStringTableAsset)를 유지한다 - 조인을 임포트 시점에 끝내 저장소들의 배선·조회
    /// 코드를 바꾸지 않기 위함이다. 카테고리 워크북은 공용 워크북과 Id로 조인해야 하므로 카테고리별 독립 임포트가
    /// 성립하지 않는다.
    ///
    /// 검사(기획 49번 §6)를 전부 통과한 뒤에만 자산을 기록한다 - 일부 카테고리만 갱신된 어긋난 상태를 남기지 않는다.
    /// 재실행해도 안전하다 - 대상 자산이 없으면 새로 만들고, 있으면 워크북 최신 내용으로 완전히 덮어쓴다.
    /// </summary>
    public static class ItemTableImporter
    {
        private const string CommonWorkbook = "Item.xlsx";
        private const string DataSheet = "ItemData";
        private const string StringSheet = "ItemStrings";
        private const string CategoryDetailSheet = "ItemDetail";
        private const string TownPriceSheet = "ItemTownPrice";
        private const int FixedFootprint = 1; // 장비/소모품/개인물품 고정 크기(사용자 확정, 2026-09-23).

        private sealed class CategorySource
        {
            public readonly ItemCategory Category;
            public readonly string Workbook;
            public readonly string DataAssetPath;
            public readonly string StringAssetPath;
            public readonly bool HasVariableFootprint;

            public CategorySource(ItemCategory category, string workbook, string dataAssetPath, string stringAssetPath, bool hasVariableFootprint)
            {
                Category = category;
                Workbook = workbook;
                DataAssetPath = dataAssetPath;
                StringAssetPath = stringAssetPath;
                HasVariableFootprint = hasVariableFootprint;
            }
        }

        // 크기가 아이템마다 다른 카테고리는 교역품 그리드에 놓이는 교역품·기타뿐이다(33번 §3.3 정정, 50번 §3.1).
        private static readonly CategorySource[] Categories =
        {
            new(ItemCategory.TradeGoods, "TradeGoods.xlsx", TableAssetPaths.TradeGoodsItemTable, TableAssetPaths.TradeGoodsItemStrings, hasVariableFootprint: true),
            new(ItemCategory.Equipment, "Equipment.xlsx", TableAssetPaths.EquipmentItemTable, TableAssetPaths.EquipmentItemStrings, hasVariableFootprint: false),
            new(ItemCategory.Consumable, "Consumable.xlsx", TableAssetPaths.ConsumableItemTable, TableAssetPaths.ConsumableItemStrings, hasVariableFootprint: false),
            new(ItemCategory.PersonalItem, "PersonalItem.xlsx", TableAssetPaths.PersonalItemItemTable, TableAssetPaths.PersonalItemItemStrings, hasVariableFootprint: false),
            new(ItemCategory.Misc, "Misc.xlsx", TableAssetPaths.MiscItemTable, TableAssetPaths.MiscItemStrings, hasVariableFootprint: true),
        };

        private sealed class CommonRow
        {
            public string Id;
            public ItemCategory Category;
            public string IconPath;
            public int Price;
            public string Ko;
            public string DescKo;
        }

        [MenuItem("Tools/Game/Table/Import Items")]
        public static void Import()
        {
            try
            {
                ImportOrThrow();
            }
            catch (Exception exception)
            {
                // 다른 도메인 임포트(TableImportAll/TableAutoImportOnPlay의 뒤 순서)까지 막지 않도록 여기서 끊는다.
                Debug.LogError($"{nameof(ItemTableImporter)}: 임포트 실패 - 자산을 갱신하지 않았다. {exception.Message}");
            }
        }

        private static void ImportOrThrow()
        {
            var folder = Path.Combine(Application.dataPath, "Table", "Item");
            var commonRows = ReadCommon(Path.Combine(folder, CommonWorkbook));

            var entriesByCategory = new Dictionary<ItemCategory, List<ItemDefinitionEntry>>();
            var tradeGoodsKinds = new List<TradeGoodsKindEntry>();
            var joinedIds = new HashSet<string>();
            foreach (var source in Categories)
            {
                entriesByCategory[source.Category] = ReadCategory(Path.Combine(folder, source.Workbook), source, commonRows, joinedIds, tradeGoodsKinds);
            }

            // 공용 Id마다 카테고리 행이 있어야 한다(기획 49번 §6).
            foreach (var row in commonRows.Values)
            {
                if (!joinedIds.Contains(row.Id))
                {
                    throw new FormatException($"공용 아이템 '{row.Id}'의 카테고리({row.Category}) 워크북 행이 없다.");
                }
            }

            foreach (var source in Categories)
            {
                WriteDefinitions(source.DataAssetPath, entriesByCategory[source.Category]);
                WriteStrings(source.StringAssetPath, entriesByCategory[source.Category], commonRows);
            }
            WriteTradeGoodsKinds(tradeGoodsKinds);

            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(ItemTableImporter)}: 아이템 {commonRows.Count}개 임포트 완료.");
        }

        private static Dictionary<string, CommonRow> ReadCommon(string workbookPath)
        {
            EnsureWorkbookExists(workbookPath);

            // 공용 데이터 시트 하나에 전체 아이템이 모이므로 ParseSlug의 시트 내 중복 검사가 곧 전체 Id 중복 검사다.
            var seenIds = new HashSet<string>();
            var rows = new Dictionary<string, CommonRow>();
            foreach (var row in EditorTableReader.ReadSheet(workbookPath, DataSheet))
            {
                var id = EditorTableReader.ParseSlug(row, "Id", seenIds);
                rows[id] = new CommonRow
                {
                    Id = id,
                    Category = EditorTableReader.ParseEnum<ItemCategory>(row, "Category"),
                    IconPath = row["IconPath"], // 아이콘 에셋이 들어오기 전까지 빈칸 허용(기획 49번 §3.1 예외)
                    Price = EditorTableReader.ParseInt(row, "Price"),
                };
            }

            var seenStringIds = new HashSet<string>();
            foreach (var row in EditorTableReader.ReadSheet(workbookPath, StringSheet))
            {
                var id = EditorTableReader.ParseSlug(row, "Id", seenStringIds);
                if (!rows.TryGetValue(id, out var common))
                {
                    throw new FormatException($"공용 스트링의 '{id}'가 공용 데이터에 없다.");
                }
                common.Ko = EditorTableReader.ParseRequiredString(row, "Ko");
                common.DescKo = EditorTableReader.ParseRequiredString(row, "DescKo");
            }

            foreach (var common in rows.Values)
            {
                if (!seenStringIds.Contains(common.Id))
                {
                    throw new FormatException($"공용 데이터의 '{common.Id}'에 공용 스트링 행이 없다.");
                }
            }

            WarnIfDetailRowsExist(workbookPath, TownPriceSheet);
            return rows;
        }

        private static List<ItemDefinitionEntry> ReadCategory(string workbookPath, CategorySource source, IReadOnlyDictionary<string, CommonRow> commonRows, HashSet<string> joinedIds, List<TradeGoodsKindEntry> tradeGoodsKinds)
        {
            EnsureWorkbookExists(workbookPath);

            var seenIds = new HashSet<string>();
            var entries = new List<ItemDefinitionEntry>();
            foreach (var row in EditorTableReader.ReadSheet(workbookPath, DataSheet))
            {
                var id = EditorTableReader.ParseSlug(row, "Id", seenIds);
                if (!commonRows.TryGetValue(id, out var common))
                {
                    throw new FormatException($"'{source.Workbook}'의 '{id}'가 공용 데이터에 없다.");
                }
                if (common.Category != source.Category)
                {
                    throw new FormatException($"'{source.Workbook}'의 '{id}'는 공용 데이터에서 {common.Category} 카테고리다.");
                }

                joinedIds.Add(id);
                entries.Add(new ItemDefinitionEntry
                {
                    Id = id,
                    FootprintWidth = source.HasVariableFootprint ? EditorTableReader.ParseInt(row, "FootprintWidth") : FixedFootprint,
                    FootprintHeight = source.HasVariableFootprint ? EditorTableReader.ParseInt(row, "FootprintHeight") : FixedFootprint,
                    Icon = ResolveIcon(common.IconPath, id),
                    Price = common.Price,
                });

                if (source.Category == ItemCategory.TradeGoods)
                {
                    tradeGoodsKinds.Add(new TradeGoodsKindEntry { Id = id, Kind = EditorTableReader.ParseEnum<TradeGoodsKind>(row, "TradeKind") });
                }
            }

            // 카테고리 세부 데이터·스트링은 아직 열이 없어 머리글만 있다(기획 49번 §3.1) - 시트 존재만 확인한다.
            WarnIfDetailRowsExist(workbookPath, CategoryDetailSheet);
            WarnIfDetailRowsExist(workbookPath, StringSheet);
            return entries;
        }

        // 세부 데이터는 아직 로직에서 쓰지 않는다(기획 49번 §3.2). 시트가 없으면 ReadSheet가 예외로 드러낸다.
        private static void WarnIfDetailRowsExist(string workbookPath, string sheetName)
        {
            var rows = EditorTableReader.ReadSheet(workbookPath, sheetName);
            if (rows.Count > 0)
            {
                Debug.LogWarning($"{nameof(ItemTableImporter)}: '{Path.GetFileName(workbookPath)}'의 '{sheetName}' 시트에 행이 있지만 아직 로직에서 쓰지 않는다.");
            }
        }

        private static void EnsureWorkbookExists(string workbookPath)
        {
            if (!File.Exists(workbookPath))
            {
                throw new FileNotFoundException($"워크북을 찾을 수 없다 - '{workbookPath}'.");
            }
        }

        private static void WriteDefinitions(string assetPath, List<ItemDefinitionEntry> entries)
        {
            var asset = EditorTableReader.GetOrCreateAsset<ItemDefinitionTableAsset>(assetPath);
            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                var entry = entries[i];
                element.FindPropertyRelative("Id").stringValue = entry.Id;
                element.FindPropertyRelative("FootprintWidth").intValue = entry.FootprintWidth;
                element.FindPropertyRelative("FootprintHeight").intValue = entry.FootprintHeight;
                element.FindPropertyRelative("Icon").objectReferenceValue = entry.Icon;
                element.FindPropertyRelative("Price").intValue = entry.Price;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }

        private static void WriteStrings(string assetPath, List<ItemDefinitionEntry> entries, IReadOnlyDictionary<string, CommonRow> commonRows)
        {
            var asset = EditorTableReader.GetOrCreateAsset<ItemStringTableAsset>(assetPath);
            var so = new SerializedObject(asset);
            var stringsProp = so.FindProperty("strings");
            stringsProp.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var common = commonRows[entries[i].Id];
                var element = stringsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").stringValue = common.Id;
                element.FindPropertyRelative("Ko").stringValue = common.Ko;
                element.FindPropertyRelative("DescKo").stringValue = common.DescKo;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }

        private static void WriteTradeGoodsKinds(List<TradeGoodsKindEntry> kinds)
        {
            var asset = EditorTableReader.GetOrCreateAsset<TradeGoodsKindTableAsset>(TableAssetPaths.TradeGoodsKindTable);
            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = kinds.Count;
            for (var i = 0; i < kinds.Count; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").stringValue = kinds[i].Id;
                EditorTableReader.SetEnumValue(element.FindPropertyRelative("Kind"), kinds[i].Kind);
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }

        // IconPath가 비어있거나 에셋을 못 찾으면 null로 두고 경고만 남긴다 - 임포트 자체를 막지 않는다
        // (아직 아트가 준비되지 않은 초기 콘텐츠 단계를 전제, Docs/설계/35번 §4 폴백 방침).
        private static Sprite ResolveIcon(string iconPath, string itemId)
        {
            if (string.IsNullOrWhiteSpace(iconPath)) return null;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (sprite == null)
            {
                Debug.LogWarning($"{nameof(ItemTableImporter)}: 아이템 '{itemId}'의 IconPath '{iconPath}'에서 Sprite를 찾을 수 없다.");
            }
            return sprite;
        }
    }
}
