using System.IO;
using ClosedXML.Excel;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Encounter.xlsx가 없을 때 머리글만 있는 워크북을 만든다(Docs/설계/87번 §3.1). 이미 있으면 건드리지 않는다 - 기획자가 채운 내용을 덮어쓰지 않기 위해서다.
    /// </summary>
    public static class EncounterTableTemplateWriter
    {
        public const string RelativePath = "Encounter/Encounter.xlsx";

        [MenuItem("Tools/Game/Table/Create Encounter Template")]
        public static void CreateIfMissing()
        {
            var path = Path.Combine(Application.dataPath, "Table", RelativePath);
            if (File.Exists(path))
            {
                Debug.Log($"{nameof(EncounterTableTemplateWriter)}: 이미 있어 만들지 않는다 - '{path}'.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var workbook = new XLWorkbook();
            AddSheet(workbook, "Event", "Id", "YarnNode", "Occurrence", "Importance", "Repeat", "Weight", "Conditions");
            AddSheet(workbook, "Dialogue", "Id", "YarnNode", "Occurrence", "Trigger", "Speakers", "Weight", "Conditions");
            AddSheet(workbook, "Person", "Id", "IllustrationKey");
            AddSheet(workbook, "PersonStrings", "Id", "Name");
            workbook.SaveAs(path);
            AssetDatabase.Refresh();
            Debug.Log($"{nameof(EncounterTableTemplateWriter)}: 템플릿 생성 - '{path}'.");
        }

        private static void AddSheet(XLWorkbook workbook, string name, params string[] headers)
        {
            var sheet = workbook.Worksheets.Add(name);
            for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        }
    }
}
