using System.IO;
using ClosedXML.Excel;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Quest.xlsx가 없을 때 머리글만 있는 워크북을 만든다(Docs/설계/107번 §4). 이미 있으면 건드리지 않는다 - 기획자가 채운 내용을 덮어쓰지 않기 위해서다
    /// (EncounterTableTemplateWriter와 같은 방식). 열 이름은 QuestTableImporter가 읽는 이름과 같다.
    /// </summary>
    public static class QuestTableTemplateWriter
    {
        [MenuItem("Tools/Game/Table/Create Quest Template")]
        public static void CreateIfMissing()
        {
            var path = Path.Combine(Application.dataPath, "Table", QuestTableImporter.RelativePath);
            if (File.Exists(path))
            {
                Debug.Log($"{nameof(QuestTableTemplateWriter)}: 이미 있어 만들지 않는다 - '{path}'.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var workbook = new XLWorkbook();
            AddSheet(workbook, "Quest", "Id", "Kind", "Repeat", "Acceptance", "Completion", "ReportCityId", "BoardCityIds", "RequiredQuestId", "MinReputation",
                "RepostDays", "FailReputation", "FailGold", "ReturnCargoOnFail", "BonusTiming");
            AddSheet(workbook, "Objective", "QuestId", "ObjectiveId", "Role", "Type", "CityId", "ItemId", "Count", "EnemyType", "QuestBattleOnly", "DeadlineDays", "DeadlineDate");
            AddSheet(workbook, "Reward", "QuestId", "ObjectiveId", "Type", "ItemId", "Amount");
            AddSheet(workbook, "Cargo", "QuestId", "ItemId", "Count");
            AddSheet(workbook, "Effect", "QuestId", "Scope", "DifficultyBonus", "EventId", "EventRepeat", "EventTiming", "ProgressMin", "ProgressMax");
            AddSheet(workbook, "FollowUp", "QuestId", "NextQuestId", "Mode", "RequiredBonusId");
            AddSheet(workbook, "QuestStrings", "Id", "Title", "Requester", "Description");
            workbook.SaveAs(path);
            AssetDatabase.Refresh();
            Debug.Log($"{nameof(QuestTableTemplateWriter)}: 템플릿 생성 - '{path}'.");
        }

        private static void AddSheet(XLWorkbook workbook, string name, params string[] headers)
        {
            var sheet = workbook.Worksheets.Add(name);
            for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        }
    }
}
