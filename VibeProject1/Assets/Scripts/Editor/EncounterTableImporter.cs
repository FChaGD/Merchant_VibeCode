using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Assets/Table/Encounter/Encounter.xlsx → EncounterTableAsset(Docs/설계/87번 §3.1). 조건은 임포트 때 한 번 해석해 자산에 넣는다 - 키·인자 오류를
    /// 실행 전에 막기 위해서다. 화자는 캐릭터 원본 엑셀(CharacterStats)과 Person 시트로 검사한다(임포트 순서와 무관하게 같은 결과). 열거형 열은 이름
    /// 문자열이다(행을 읽는 사람이 바로 알아보도록, TownStock과 같은 방식). YarnNode 존재는 Yarn 컴파일 결과가 필요해 콘텐츠 검증 테스트가 본다(§9).
    /// </summary>
    public static class EncounterTableImporter
    {
        private const int DefaultWeight = 100;
        public const string YarnProjectPath = "Assets/Content/Dialogue/Encounter.yarnproject";

        [MenuItem("Tools/Game/Table/Import Encounter")]
        public static void Import()
        {
            // Yarn 프로젝트는 .yarn 파일을 새로 추가해도 스스로 다시 컴파일되지 않아 새 노드가 실행 중 "없음"이 된다(설계 88 제작 중 확인). 대본은 엑셀과 함께
            // 콘텐츠이므로 테이블 임포트(Play 시 자동 임포트 포함)마다 프로젝트를 다시 컴파일해 둘이 항상 같은 시점의 내용을 보게 한다.
            if (File.Exists(YarnProjectPath)) AssetDatabase.ImportAsset(YarnProjectPath, ImportAssetOptions.ForceUpdate);

            var workbookPath = TablePath(EncounterTableTemplateWriter.RelativePath);
            if (!File.Exists(workbookPath))
            {
                Debug.LogError($"{nameof(EncounterTableImporter)}: 워크북을 찾을 수 없다 - '{workbookPath}' (Tools > Game > Table > Create Encounter Template).");
                return;
            }

            try
            {
                var registry = EncounterConditionRegistry.CreateDefault();
                var people = ReadPeople(workbookPath);
                var events = ReadEvents(workbookPath, registry);
                var dialogues = ReadDialogues(workbookPath, registry);
                Validate(events, dialogues, people);

                var asset = EditorTableReader.GetOrCreateAsset<EncounterTableAsset>(TableAssetPaths.EncounterTable);
                Write(asset, events, dialogues, people);
                AssetDatabase.SaveAssets();
                Debug.Log($"{nameof(EncounterTableImporter)}: 임포트 완료 - 이벤트 {events.Count}, 대화 {dialogues.Count}, 외부 인물 {people.Count}.");
            }
            catch (FormatException e)
            {
                Debug.LogError($"{nameof(EncounterTableImporter)}: {e.Message}");
            }
        }

        private static string TablePath(string relative) => Path.Combine(Application.dataPath, "Table", relative);

        private static List<EventEntry> ReadEvents(string path, EncounterConditionRegistry registry)
        {
            var titles = ReadEventTitles(path);
            var list = new List<EventEntry>();
            foreach (var row in EditorTableReader.ReadSheet(path, "Event"))
            {
                var id = EditorTableReader.ParseRequiredString(row, "Id").Trim();
                var placeText = Optional(row, "Place");
                var place = placeText == null ? EventPlace.Trip : ParseNameText<EventPlace>(placeText, "Place", id);
                titles.TryGetValue(id, out var title);
                // 휴식 이벤트는 진행 화면 기록 목록에 제목을 보인다(설계 109 §3.1) - 상행 이벤트는 제목을 쓰는 곳이 없어 선택.
                if (place == EventPlace.Rest && string.IsNullOrWhiteSpace(title)) throw new FormatException($"Event '{id}': {EventPlaceNames.Of(place)} 이벤트는 EventStrings 시트에 제목이 있어야 한다.");
                var conditions = ParseConditions(row, id, registry);
                // 장소에 없는 값을 보는 조건은 항상 거짓이라 작성 실수다(설계 109 §3.4) - 조건을 읽은 행에서 바로 막는다.
                foreach (var c in conditions)
                {
                    if (!EncounterConditionKeys.IsAllowedAt(c.Key, place)) throw new FormatException($"Event '{id}': 조건 '{c.Key}'는 {EventPlaceNames.Of(place)} 이벤트에서 쓸 수 없다.");
                }
                list.Add(new EventEntry
                {
                    Id = id,
                    YarnNode = EditorTableReader.ParseRequiredString(row, "YarnNode").Trim(),
                    Occurrence = ParseName<EventOccurrence>(row, "Occurrence", id),
                    Place = place,
                    Title = title ?? string.Empty,
                    Importance = ParseName<EventImportance>(row, "Importance", id),
                    Repeat = ParseName<EventRepeat>(row, "Repeat", id),
                    Weight = ParseWeight(row, id),
                    Conditions = conditions,
                });
            }
            return list;
        }

        // EventStrings 시트는 휴식 이벤트(설계 109)와 함께 생겼다 - 시트가 없는 이전 엑셀도 상행 이벤트만이면 그대로 임포트되게 한다.
        private static Dictionary<string, string> ReadEventTitles(string path)
        {
            var titles = new Dictionary<string, string>();
            IReadOnlyList<IReadOnlyDictionary<string, string>> rows;
            try
            {
                rows = EditorTableReader.ReadSheet(path, "EventStrings");
            }
            catch (InvalidOperationException)
            {
                return titles;
            }
            foreach (var row in rows) titles[EditorTableReader.ParseRequiredString(row, "Id").Trim()] = Optional(row, "Title") ?? string.Empty;
            return titles;
        }

        private static List<DialogueEntry> ReadDialogues(string path, EncounterConditionRegistry registry)
        {
            var list = new List<DialogueEntry>();
            foreach (var row in EditorTableReader.ReadSheet(path, "Dialogue"))
            {
                var id = EditorTableReader.ParseRequiredString(row, "Id").Trim();
                var occurrence = ParseName<DialogueOccurrence>(row, "Occurrence", id);
                var trigger = Optional(row, "Trigger") is { } t ? ParseNameText<DialogueTrigger>(t, "Trigger", id) : DialogueTrigger.None;
                if (occurrence == DialogueOccurrence.Trigger && trigger == DialogueTrigger.None) throw new FormatException($"Dialogue '{id}': Occurrence가 Trigger면 Trigger 열이 필요하다.");
                if (occurrence != DialogueOccurrence.Trigger && trigger != DialogueTrigger.None) throw new FormatException($"Dialogue '{id}': Trigger 열은 Occurrence가 Trigger일 때만 쓴다.");

                list.Add(new DialogueEntry
                {
                    Id = id,
                    YarnNode = EditorTableReader.ParseRequiredString(row, "YarnNode").Trim(),
                    Occurrence = occurrence,
                    Trigger = trigger,
                    Speakers = ParseSpeakers(row),
                    Weight = ParseWeight(row, id),
                    Conditions = ParseConditions(row, id, registry),
                });
            }
            return list;
        }

        private static List<PersonEntry> ReadPeople(string path)
        {
            var names = new Dictionary<string, string>();
            foreach (var row in EditorTableReader.ReadSheet(path, "PersonStrings"))
            {
                names[EditorTableReader.ParseRequiredString(row, "Id").Trim()] = EditorTableReader.ParseRequiredString(row, "Name");
            }

            var list = new List<PersonEntry>();
            foreach (var row in EditorTableReader.ReadSheet(path, "Person"))
            {
                var id = EditorTableReader.ParseRequiredString(row, "Id").Trim();
                if (!names.TryGetValue(id, out var name)) throw new FormatException($"Person '{id}'의 이름이 PersonStrings 시트에 없다.");
                list.Add(new PersonEntry { Id = id, Name = name, IllustrationKey = Optional(row, "IllustrationKey") ?? string.Empty });
            }
            return list;
        }

        private static void Validate(List<EventEntry> events, List<DialogueEntry> dialogues, List<PersonEntry> people)
        {
            var ids = new HashSet<string>();
            foreach (var e in events) if (!ids.Add(e.Id)) throw new FormatException($"Id '{e.Id}' 중복(이벤트·대화 Id는 이력을 함께 쓰므로 서로도 겹치면 안 된다).");
            foreach (var d in dialogues) if (!ids.Add(d.Id)) throw new FormatException($"Id '{d.Id}' 중복(이벤트·대화 Id는 이력을 함께 쓰므로 서로도 겹치면 안 된다).");

            var personIds = new HashSet<string>();
            foreach (var p in people) if (!personIds.Add(p.Id)) throw new FormatException($"Person Id '{p.Id}' 중복.");

            var characterIds = ReadCharacterIds();
            foreach (var d in dialogues)
            {
                foreach (var speaker in d.Speakers)
                {
                    if (!characterIds.Contains(speaker) && !personIds.Contains(speaker))
                    {
                        throw new FormatException($"Dialogue '{d.Id}': 화자 '{speaker}'가 CharacterStats·Person 어디에도 없다.");
                    }
                }
            }
        }

        private static HashSet<string> ReadCharacterIds()
        {
            var ids = new HashSet<string>();
            foreach (var row in EditorTableReader.ReadSheet(TablePath("Character/CharacterStats.xlsx"), "CharacterStats"))
            {
                if (row.TryGetValue("Id", out var id) && !string.IsNullOrWhiteSpace(id)) ids.Add(id.Trim());
            }
            return ids;
        }

        private static List<EncounterCondition> ParseConditions(IReadOnlyDictionary<string, string> row, string id, EncounterConditionRegistry registry)
        {
            List<EncounterCondition> conditions;
            try { conditions = EncounterConditionParser.Parse(Optional(row, "Conditions")); }
            catch (FormatException e) { throw new FormatException($"'{id}' 조건: {e.Message}"); }

            foreach (var c in conditions)
            {
                if (!registry.IsValidArgument(c.Key, c.Argument)) throw new FormatException($"'{id}' 조건 '{c.Key}:{c.Argument}'의 값 형식이 틀렸다.");
            }
            return conditions;
        }

        private static List<string> ParseSpeakers(IReadOnlyDictionary<string, string> row)
        {
            var list = new List<string>();
            var text = Optional(row, "Speakers");
            if (text == null) return list;
            foreach (var part in text.Split(';'))
            {
                var s = part.Trim();
                if (s.Length > 0) list.Add(s);
            }
            return list;
        }

        private static int ParseWeight(IReadOnlyDictionary<string, string> row, string id)
        {
            var text = Optional(row, "Weight");
            if (text == null) return DefaultWeight;
            if (!int.TryParse(text, out var weight) || weight < 0) throw new FormatException($"'{id}' Weight '{text}'는 0 이상의 정수여야 한다.");
            return weight;
        }

        private static TEnum ParseName<TEnum>(IReadOnlyDictionary<string, string> row, string column, string id) where TEnum : struct, Enum
            => ParseNameText<TEnum>(EditorTableReader.ParseRequiredString(row, column), column, id);

        // 숫자 문자열("0")도 Enum.TryParse를 통과하므로 이름으로 정의된 값만 받는다(TownStockTableImporter와 같은 이유).
        private static TEnum ParseNameText<TEnum>(string raw, string column, string id) where TEnum : struct, Enum
        {
            var text = raw.Trim();
            if (text.Length > 0 && !char.IsDigit(text[0]) && Enum.TryParse(text, ignoreCase: false, out TEnum value) && Enum.IsDefined(typeof(TEnum), value)) return value;
            throw new FormatException($"'{id}' {column} '{text}'은(는) {string.Join("/", Enum.GetNames(typeof(TEnum)))} 중 하나여야 한다.");
        }

        private static string Optional(IReadOnlyDictionary<string, string> row, string column)
            => row.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

        // 자산 필드가 private이라 SerializedObject로 쓴다.
        private static void Write(EncounterTableAsset asset, List<EventEntry> events, List<DialogueEntry> dialogues, List<PersonEntry> people)
        {
            var so = new SerializedObject(asset);
            WriteEvents(so.FindProperty("events"), events);
            WriteDialogues(so.FindProperty("dialogues"), dialogues);
            WritePeople(so.FindProperty("people"), people);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }

        private static void WriteEvents(SerializedProperty list, List<EventEntry> events)
        {
            list.arraySize = events.Count;
            for (var i = 0; i < events.Count; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Id").stringValue = events[i].Id;
                e.FindPropertyRelative("YarnNode").stringValue = events[i].YarnNode;
                EditorTableReader.SetEnumValue(e.FindPropertyRelative("Occurrence"), events[i].Occurrence);
                EditorTableReader.SetEnumValue(e.FindPropertyRelative("Place"), events[i].Place);
                e.FindPropertyRelative("Title").stringValue = events[i].Title;
                EditorTableReader.SetEnumValue(e.FindPropertyRelative("Importance"), events[i].Importance);
                EditorTableReader.SetEnumValue(e.FindPropertyRelative("Repeat"), events[i].Repeat);
                e.FindPropertyRelative("Weight").intValue = events[i].Weight;
                WriteConditions(e.FindPropertyRelative("Conditions"), events[i].Conditions);
            }
        }

        private static void WriteDialogues(SerializedProperty list, List<DialogueEntry> dialogues)
        {
            list.arraySize = dialogues.Count;
            for (var i = 0; i < dialogues.Count; i++)
            {
                var d = list.GetArrayElementAtIndex(i);
                d.FindPropertyRelative("Id").stringValue = dialogues[i].Id;
                d.FindPropertyRelative("YarnNode").stringValue = dialogues[i].YarnNode;
                EditorTableReader.SetEnumValue(d.FindPropertyRelative("Occurrence"), dialogues[i].Occurrence);
                EditorTableReader.SetEnumValue(d.FindPropertyRelative("Trigger"), dialogues[i].Trigger);
                var speakers = d.FindPropertyRelative("Speakers");
                speakers.arraySize = dialogues[i].Speakers.Count;
                for (var s = 0; s < dialogues[i].Speakers.Count; s++) speakers.GetArrayElementAtIndex(s).stringValue = dialogues[i].Speakers[s];
                d.FindPropertyRelative("Weight").intValue = dialogues[i].Weight;
                WriteConditions(d.FindPropertyRelative("Conditions"), dialogues[i].Conditions);
            }
        }

        private static void WritePeople(SerializedProperty list, List<PersonEntry> people)
        {
            list.arraySize = people.Count;
            for (var i = 0; i < people.Count; i++)
            {
                var p = list.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("Id").stringValue = people[i].Id;
                p.FindPropertyRelative("Name").stringValue = people[i].Name;
                p.FindPropertyRelative("IllustrationKey").stringValue = people[i].IllustrationKey;
            }
        }

        private static void WriteConditions(SerializedProperty list, List<EncounterCondition> conditions)
        {
            list.arraySize = conditions.Count;
            for (var i = 0; i < conditions.Count; i++)
            {
                var c = list.GetArrayElementAtIndex(i);
                c.FindPropertyRelative("Key").stringValue = conditions[i].Key;
                c.FindPropertyRelative("Negated").boolValue = conditions[i].Negated;
                c.FindPropertyRelative("Argument").stringValue = conditions[i].Argument;
            }
        }
    }
}
