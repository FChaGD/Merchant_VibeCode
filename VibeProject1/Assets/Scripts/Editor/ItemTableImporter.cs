using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// 아이템 카테고리 4종(교역품/장비/소모품/개인물품) 워크북을 읽어 ItemDefinitionTableAsset/
    /// ItemStringTableAsset을 덮어쓴다(Docs/설계/35번 §5). CharacterStatsTableImporter 등과 달리
    /// 카테고리 4개가 스키마가 완전히 같아 임포트 로직 자체를 공유하고, 워크북/자산 경로만 카테고리별로
    /// 다르다(35번 §2 - 클래스 공유 결정과 같은 이유). 재실행해도 안전 - 대상 에셋이 없으면 새로 만들고,
    /// 있으면 항상 워크북 최신 내용으로 완전히 덮어쓴다.
    ///
    /// 사이즈(FootprintWidth/Height)는 교역품/전리품(골드 상자 포함)만 아이템마다 다르고, 장비/소모품/
    /// 개인물품은 전부 1×1 고정이다(사용자 확정, 2026-09-23) - 후자 3개 워크북은 크기 컬럼 자체가
    /// 없고, 임포터가 1×1을 코드로 못 박는다. hasVariableFootprint가 이 분기를 담당한다.
    /// </summary>
    public static class ItemTableImporter
    {
        [MenuItem("Tools/Game/Table/Import Trade Goods Items")]
        public static void ImportTradeGoods() => Import("TradeGoods.xlsx", TableAssetPaths.TradeGoodsItemTable, TableAssetPaths.TradeGoodsItemStrings, hasVariableFootprint: true);

        [MenuItem("Tools/Game/Table/Import Equipment Items")]
        public static void ImportEquipment() => Import("Equipment.xlsx", TableAssetPaths.EquipmentItemTable, TableAssetPaths.EquipmentItemStrings, hasVariableFootprint: false);

        [MenuItem("Tools/Game/Table/Import Consumable Items")]
        public static void ImportConsumable() => Import("Consumable.xlsx", TableAssetPaths.ConsumableItemTable, TableAssetPaths.ConsumableItemStrings, hasVariableFootprint: false);

        [MenuItem("Tools/Game/Table/Import Personal Items")]
        public static void ImportPersonalItem() => Import("PersonalItem.xlsx", TableAssetPaths.PersonalItemItemTable, TableAssetPaths.PersonalItemItemStrings, hasVariableFootprint: false);

        private static void Import(string workbookFileName, string dataAssetPath, string stringAssetPath, bool hasVariableFootprint)
        {
            var workbookPath = Path.Combine(Application.dataPath, "Table", "Item", workbookFileName);
            if (!File.Exists(workbookPath))
            {
                Debug.LogError($"{nameof(ItemTableImporter)}: 워크북을 찾을 수 없다 - '{workbookPath}'.");
                return;
            }

            ImportItemData(workbookPath, dataAssetPath, hasVariableFootprint);
            ImportItemStrings(workbookPath, stringAssetPath);

            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(ItemTableImporter)}: '{workbookFileName}' 임포트 완료.");
        }

        private const int FixedFootprint = 1; // 장비/소모품/개인물품 고정 크기(사용자 확정, 2026-09-23).

        private static void ImportItemData(string workbookPath, string dataAssetPath, bool hasVariableFootprint)
        {
            var rows = EditorTableReader.ReadSheet(workbookPath, "ItemData");
            var seenIds = new HashSet<string>();
            var entries = new List<ItemDefinitionEntry>(rows.Count);
            foreach (var row in rows)
            {
                var id = EditorTableReader.ParseSlug(row, "Id", seenIds);
                entries.Add(new ItemDefinitionEntry
                {
                    Id = id,
                    FootprintWidth = hasVariableFootprint ? EditorTableReader.ParseInt(row, "FootprintWidth") : FixedFootprint,
                    FootprintHeight = hasVariableFootprint ? EditorTableReader.ParseInt(row, "FootprintHeight") : FixedFootprint,
                    Icon = ResolveIcon(row["IconPath"], id),
                });
            }

            var asset = EditorTableReader.GetOrCreateAsset<ItemDefinitionTableAsset>(dataAssetPath);
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
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }

        private static void ImportItemStrings(string workbookPath, string stringAssetPath)
        {
            var rows = EditorTableReader.ReadSheet(workbookPath, "ItemStrings");
            var seenIds = new HashSet<string>();
            var asset = EditorTableReader.GetOrCreateAsset<ItemStringTableAsset>(stringAssetPath);
            var so = new SerializedObject(asset);
            var stringsProp = so.FindProperty("strings");
            stringsProp.arraySize = rows.Count;
            for (var i = 0; i < rows.Count; i++)
            {
                var element = stringsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").stringValue = EditorTableReader.ParseSlug(rows[i], "Id", seenIds);
                element.FindPropertyRelative("Ko").stringValue = rows[i]["Ko"];
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
