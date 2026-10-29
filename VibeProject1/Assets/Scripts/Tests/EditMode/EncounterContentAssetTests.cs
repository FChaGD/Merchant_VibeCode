using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Tests
{
    /// <summary>실제 콘텐츠(Assets/Content/Dialogue/*.yarn + Encounter 테이블 자산) 정합성 - 콘텐츠가 늘수록 의미가 커진다.</summary>
    public class EncounterContentAssetTests
    {
        private static List<YarnScriptNode> ScanAll()
        {
            var nodes = new List<YarnScriptNode>();
            var folder = Path.Combine(Application.dataPath, "Content/Dialogue");
            if (!Directory.Exists(folder)) return nodes;
            foreach (var file in Directory.GetFiles(folder, "*.yarn", SearchOption.AllDirectories))
            {
                nodes.AddRange(YarnSourceScanner.Scan(File.ReadAllText(file)));
            }
            return nodes;
        }

        [Test]
        public void TableNodesExist_AndScriptsAreValid()
        {
            var nodes = ScanAll();
            var titles = new HashSet<string>();
            foreach (var n in nodes) titles.Add(n.Title);

            var table = AssetDatabase.LoadAssetAtPath<EncounterTableAsset>(TableAssetPaths.EncounterTable);
            Assert.IsNotNull(table, "EncounterTable 자산이 없다 - Tools > Game > Table > Import Encounter.");

            var errors = EncounterContentValidator.ValidateTable(table.Events, table.Dialogues, titles.Contains);
            var personIds = new HashSet<string>();
            foreach (var p in table.People) personIds.Add(p.Id);
            var stats = AssetDatabase.LoadAssetAtPath<CharacterStatsTableAsset>(TableAssetPaths.CharacterStatsTable);
            errors.AddRange(EncounterContentValidator.ValidateScripts(nodes, id => personIds.Contains(id) || (stats != null && stats.TryGetEntry(id, out _))));

            // 이벤트 대본(설계 89 §5.4) - 명령·인자·Id·#choice.
            var eventNodes = new Dictionary<string, EventPlace>();
            foreach (var e in table.Events) eventNodes[e.YarnNode] = e.Place;
            var items = AssetDatabase.LoadAssetAtPath<ItemDefinitionTableAsset>(TableAssetPaths.TradeGoodsItemTable);
            errors.AddRange(EncounterContentValidator.ValidateEventScripts(nodes, eventNodes, new EventScriptRules
            {
                ItemExists = id => items != null && items.TryGetEntry(id, out _),
                CharacterExists = id => stats != null && stats.TryGetEntry(id, out _),
                SpeakerExists = id => personIds.Contains(id) || (stats != null && stats.TryGetEntry(id, out _)),
            }));
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
        }
    }
}
