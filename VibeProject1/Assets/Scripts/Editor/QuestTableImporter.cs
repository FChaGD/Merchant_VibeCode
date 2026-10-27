using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Assets/Table/Quest/Quest.xlsx → QuestTableAsset(Docs/설계/107번 §4). 7개 시트를 퀘스트 단위로 묶고, 참조 무결성은 이미 임포트된 자산이 아니라
    /// 원본 엑셀(City·TradeGoods·EnemyStats·Encounter)을 직접 읽어 검사한다 - 임포트 순서와 무관하게 같은 결과를 내기 위함이다(TownStock과 같은 방식).
    /// 열거형 열은 이름 문자열이다(행을 읽는 사람이 바로 알아보도록). 워크북이 없으면 경고만 남기고 중단한다 - 퀘스트가 없는 상태도 정상이다.
    /// </summary>
    public static class QuestTableImporter
    {
        public const string RelativePath = "Quest/Quest.xlsx";
        private const int QuestCargoKind = (int)TradeGoodsKind.QuestCargo;

        [MenuItem("Tools/Game/Table/Import Quest")]
        public static void Import()
        {
            var path = TablePath(RelativePath);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"{nameof(QuestTableImporter)}: 워크북이 없어 건너뛴다 - '{path}'(Tools > Game > Table > Create Quest Template).");
                return;
            }

            var quests = ReadQuests(path);
            QuestTableValidator.Validate(quests, ReadReferenceIds());

            var asset = EditorTableReader.GetOrCreateAsset<QuestTableAsset>(TableAssetPaths.QuestTable);
            asset.ReplaceAll(quests);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(QuestTableImporter)}: 임포트 완료({quests.Count}건).");
        }

        private static string TablePath(string relative) => Path.Combine(Application.dataPath, "Table", relative);

        private static List<QuestDefinition> ReadQuests(string path)
        {
            var quests = new List<QuestDefinition>();
            var byId = new Dictionary<string, QuestDefinition>();
            foreach (var row in EditorTableReader.ReadSheet(path, "Quest"))
            {
                var id = EditorTableReader.ParseRequiredString(row, "Id").Trim();
                var q = new QuestDefinition
                {
                    Id = id,
                    Kind = ParseName<QuestKind>(row, "Kind", id),
                    Repeat = ParseName<QuestRepeat>(row, "Repeat", id),
                    Acceptance = ParseName<QuestAcceptance>(row, "Acceptance", id),
                    Completion = ParseName<QuestCompletion>(row, "Completion", id),
                    ReportCityId = OptionalInt(row, "ReportCityId", id) ?? 0,
                    BoardCityIds = ParseIntList(row, "BoardCityIds", id),
                    RequiredQuestId = Optional(row, "RequiredQuestId"),
                    FailReputation = OptionalInt(row, "FailReputation", id) ?? 0,
                    FailGold = OptionalInt(row, "FailGold", id) ?? 0,
                    ReturnCargoOnFail = OptionalBool(row, "ReturnCargoOnFail", id) ?? false,
                    BonusTiming = Optional(row, "BonusTiming") != null ? ParseName<QuestBonusTiming>(row, "BonusTiming", id) : QuestBonusTiming.OnSuccess,
                };
                if (OptionalInt(row, "MinReputation", id) is { } minReputation)
                {
                    q.HasMinReputation = true;
                    q.MinReputation = minReputation;
                }
                if (OptionalInt(row, "RepostDays", id) is { } repostDays)
                {
                    q.HasRepostDays = true;
                    q.RepostDays = repostDays;
                }
                if (!byId.TryAdd(id, q)) throw new FormatException($"Quest '{id}': Id가 중복됐다.");
                quests.Add(q);
            }

            foreach (var row in EditorTableReader.ReadSheet(path, "Objective"))
            {
                var q = Owner(byId, row, "Objective");
                var o = new QuestObjectiveDefinition
                {
                    Id = EditorTableReader.ParseRequiredString(row, "ObjectiveId").Trim(),
                    Role = ParseName<QuestObjectiveRole>(row, "Role", q.Id),
                    Type = ParseName<QuestObjectiveType>(row, "Type", q.Id),
                    CityId = OptionalInt(row, "CityId", q.Id) ?? 0,
                    ItemId = Optional(row, "ItemId"),
                    Count = OptionalInt(row, "Count", q.Id) ?? 0,
                    EnemyType = Optional(row, "EnemyType"),
                    QuestBattleOnly = OptionalBool(row, "QuestBattleOnly", q.Id) ?? false,
                };
                if (OptionalInt(row, "DeadlineDays", q.Id) is { } days)
                {
                    o.HasDeadlineDays = true;
                    o.DeadlineDays = days;
                }
                if (Optional(row, "DeadlineDate") is { } dateText)
                {
                    if (!QuestDateFormat.TryParse(dateText, out var totalDays)) throw new FormatException($"Quest '{q.Id}': DeadlineDate '{dateText}'는 '경과연차-월-일' 형식이어야 한다.");
                    o.HasDeadlineDate = true;
                    o.DeadlineTotalDays = totalDays;
                }
                q.Objectives.Add(o);
            }

            foreach (var row in EditorTableReader.ReadSheet(path, "Reward"))
            {
                var q = Owner(byId, row, "Reward");
                q.Rewards.Add(new QuestRewardDefinition
                {
                    ObjectiveId = Optional(row, "ObjectiveId"),
                    Type = ParseName<QuestRewardType>(row, "Type", q.Id),
                    ItemId = Optional(row, "ItemId"),
                    Amount = OptionalInt(row, "Amount", q.Id) ?? 0,
                });
            }

            foreach (var row in EditorTableReader.ReadSheet(path, "Cargo"))
            {
                var q = Owner(byId, row, "Cargo");
                q.Cargo.Add(new QuestCargoDefinition { ItemId = EditorTableReader.ParseRequiredString(row, "ItemId").Trim(), Count = EditorTableReader.ParseInt(row, "Count") });
            }

            foreach (var row in EditorTableReader.ReadSheet(path, "Effect"))
            {
                var q = Owner(byId, row, "Effect");
                var min = OptionalFloat(row, "ProgressMin", q.Id) ?? 0f;
                q.Effects.Add(new QuestEffectDefinition
                {
                    Scope = ParseName<QuestEffectScope>(row, "Scope", q.Id),
                    DifficultyBonus = OptionalInt(row, "DifficultyBonus", q.Id) ?? 0,
                    EventId = Optional(row, "EventId"),
                    EventRepeat = Optional(row, "EventRepeat") != null ? ParseName<QuestEventRepeat>(row, "EventRepeat", q.Id) : QuestEventRepeat.PerQuest,
                    EventTiming = Optional(row, "EventTiming") != null ? ParseName<QuestEventTiming>(row, "EventTiming", q.Id) : QuestEventTiming.Progress,
                    ProgressMin = min,
                    ProgressMax = OptionalFloat(row, "ProgressMax", q.Id) ?? min,
                });
            }

            foreach (var row in EditorTableReader.ReadSheet(path, "FollowUp"))
            {
                var q = Owner(byId, row, "FollowUp");
                q.FollowUps.Add(new QuestFollowUpDefinition
                {
                    NextQuestId = EditorTableReader.ParseRequiredString(row, "NextQuestId").Trim(),
                    Mode = ParseName<QuestFollowUpMode>(row, "Mode", q.Id),
                    RequiredBonusId = Optional(row, "RequiredBonusId"),
                });
            }

            ApplyStrings(path, byId);
            return quests;
        }

        // 문자열 Id: 퀘스트 Id = 제목·의뢰자·설명, `퀘스트Id.목표Id` = 목표 표시문(Title 열). 없으면 "값 없음"(창작 금지 규칙).
        private static void ApplyStrings(string path, Dictionary<string, QuestDefinition> byId)
        {
            var strings = new Dictionary<string, IReadOnlyDictionary<string, string>>();
            foreach (var row in EditorTableReader.ReadSheet(path, "QuestStrings"))
            {
                var id = EditorTableReader.ParseRequiredString(row, "Id").Trim();
                strings[id] = row;
            }

            foreach (var q in byId.Values)
            {
                strings.TryGetValue(q.Id, out var row);
                q.Title = Text(row, "Title");
                q.Requester = Text(row, "Requester");
                q.Description = Text(row, "Description");
                foreach (var o in q.Objectives)
                {
                    strings.TryGetValue($"{q.Id}.{o.Id}", out var objectiveRow);
                    o.Label = Text(objectiveRow, "Title");
                }
            }
        }

        private static string Text(IReadOnlyDictionary<string, string> row, string column)
            => row != null && row.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : PlaceholderText.Missing;

        private static QuestDefinition Owner(Dictionary<string, QuestDefinition> byId, IReadOnlyDictionary<string, string> row, string sheet)
        {
            var questId = EditorTableReader.ParseRequiredString(row, "QuestId").Trim();
            if (!byId.TryGetValue(questId, out var q)) throw new FormatException($"{sheet}: QuestId '{questId}'가 Quest 시트에 없다.");
            return q;
        }

        private static QuestReferenceIds ReadReferenceIds()
        {
            var refs = new QuestReferenceIds();
            foreach (var row in EditorTableReader.ReadSheet(TablePath("City/City.xlsx"), "CityData"))
            {
                if (int.TryParse(Optional(row, "Id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cityId)) refs.CityIds.Add(cityId);
            }
            foreach (var row in EditorTableReader.ReadSheet(TablePath("Item/TradeGoods.xlsx"), "ItemData"))
            {
                var id = Optional(row, "Id");
                if (id == null) continue;
                refs.TradeGoodsIds.Add(id);
                if (int.TryParse(Optional(row, "TradeKind"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var kind) && kind == QuestCargoKind) refs.QuestCargoIds.Add(id);
            }
            foreach (var row in EditorTableReader.ReadSheet(TablePath("Enemy/EnemyStats.xlsx"), "EnemyStats"))
            {
                if (Optional(row, "Id") is { } id) refs.EnemyTypes.Add(id);
            }
            var encounterPath = TablePath("Encounter/Encounter.xlsx");
            if (File.Exists(encounterPath))
            {
                foreach (var row in EditorTableReader.ReadSheet(encounterPath, "Event"))
                {
                    if (Optional(row, "Id") is { } id) refs.EventIds.Add(id);
                }
            }
            return refs;
        }

        private static string Optional(IReadOnlyDictionary<string, string> row, string column)
            => row.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

        private static int? OptionalInt(IReadOnlyDictionary<string, string> row, string column, string questId)
        {
            var text = Optional(row, column);
            if (text == null) return null;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) throw new FormatException($"Quest '{questId}': {column} '{text}'는 정수여야 한다.");
            return value;
        }

        private static float? OptionalFloat(IReadOnlyDictionary<string, string> row, string column, string questId)
        {
            var text = Optional(row, column);
            if (text == null) return null;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) throw new FormatException($"Quest '{questId}': {column} '{text}'는 실수여야 한다.");
            return value;
        }

        private static bool? OptionalBool(IReadOnlyDictionary<string, string> row, string column, string questId)
        {
            var text = Optional(row, column);
            if (text == null) return null;
            if (!bool.TryParse(text, out var value)) throw new FormatException($"Quest '{questId}': {column} '{text}'는 TRUE/FALSE여야 한다.");
            return value;
        }

        private static List<int> ParseIntList(IReadOnlyDictionary<string, string> row, string column, string questId)
        {
            var list = new List<int>();
            var text = Optional(row, column);
            if (text == null) return list;
            foreach (var part in text.Split(','))
            {
                if (!int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) throw new FormatException($"Quest '{questId}': {column} '{text}'는 쉼표로 구분한 정수여야 한다.");
                list.Add(value);
            }
            return list;
        }

        // 숫자 문자열("0")도 Enum.TryParse를 통과하므로 이름으로 정의된 값만 받는다(TownStockTableImporter와 같은 규칙).
        private static T ParseName<T>(IReadOnlyDictionary<string, string> row, string column, string questId) where T : struct, Enum
        {
            var text = Optional(row, column) ?? throw new FormatException($"Quest '{questId}': {column}이 비어 있다.");
            if (!char.IsDigit(text[0]) && Enum.TryParse(text, ignoreCase: false, out T value) && Enum.IsDefined(typeof(T), value)) return value;
            throw new FormatException($"Quest '{questId}': {column} '{text}'는 {string.Join("/", Enum.GetNames(typeof(T)))} 중 하나여야 한다.");
        }
    }
}
