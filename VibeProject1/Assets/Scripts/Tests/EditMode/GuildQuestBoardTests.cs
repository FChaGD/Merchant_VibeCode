using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    /// <summary>상인조합 게시판(Docs/설계/107번 §11.1) - 현재 마을 기준 목록·상세·사유. Placeholder 게시판 테스트 대체.</summary>
    public class GuildQuestBoardTests
    {
        private static readonly GoldTestItem Wheat = new("wheat", 1, 1);

        private GameObject go;
        private GoldTestInventory inventory;
        private QuestTestLocation location;
        private QuestManager manager;
        private GuildQuestBoard board;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Guild");
            inventory = new GoldTestInventory(GoldTestInventory.Section("A", 3, 1));
            inventory.AddDefinition(Wheat);
            location = new QuestTestLocation { CurrentCityId = 1 };
            manager = go.AddComponent<QuestManager>();
            manager.Initialize(new[] { Quest("Here", 1, 1), Quest("Report", 1, 2), Quest("Elsewhere", 3, 3) },
                new QuestManagerSources { Inventory = inventory, Settlement = new CargoTestSettlement(inventory), Location = location, Warn = _ => { } });
            board = go.AddComponent<GuildQuestBoard>();
            board.Initialize(manager, location, null, inventory, null);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        // 보고형 조달 - 밀 1개를 reportCity에서 보고.
        private static QuestDefinition Quest(string id, int boardCity, int reportCity) => new()
        {
            Id = id,
            Title = id,
            Requester = "의뢰자",
            Kind = QuestKind.General,
            Acceptance = QuestAcceptance.Request,
            Completion = QuestCompletion.Report,
            ReportCityId = reportCity,
            BoardCityIds = new List<int> { boardCity },
            Objectives = new List<QuestObjectiveDefinition> { new() { Id = "d", Role = QuestObjectiveRole.Core, Type = QuestObjectiveType.Deliver, CityId = reportCity, ItemId = "wheat", Count = 1 } },
            Rewards = new List<QuestRewardDefinition> { new() { Type = QuestRewardType.Gold, Amount = 10 } },
        };

        private GuildQuestStatus? StatusOf(string id) => board.Quests.FirstOrDefault(q => q.Id == id)?.Status;

        [Test]
        public void ListsAvailableActiveReportableForCity()
        {
            Assert.AreEqual(GuildQuestStatus.Available, StatusOf("Here"));
            Assert.IsNull(StatusOf("Elsewhere"));

            Assert.IsTrue(board.TryAccept("Here"));
            Assert.AreEqual(GuildQuestStatus.Active, StatusOf("Here"));
            inventory.Place(Wheat, 0, 0, "A");
            Assert.AreEqual(GuildQuestStatus.Reportable, StatusOf("Here"));
            Assert.AreEqual("Here", board.Quests[0].Id, "보고 가능이 맨 위");
        }

        [Test]
        public void ReportableShownAtReportCityRegardlessOfSource()
        {
            board.TryAccept("Report");
            inventory.Place(Wheat, 0, 0, "A");
            Assert.AreEqual(GuildQuestStatus.Active, StatusOf("Report"), "보고처가 아닌 수락 마을에서는 진행 중");
            location.CurrentCityId = 2;
            Assert.AreEqual(GuildQuestStatus.Reportable, StatusOf("Report"));
            Assert.IsTrue(board.CanReport("Report", out _));
            Assert.IsTrue(board.TryReport("Report"));
            Assert.IsNull(StatusOf("Report"));
        }

        [Test]
        public void Detail_ObjectiveLinesAndStatus()
        {
            board.TryAccept("Here");
            var detail = board.GetDetail("Here");
            Assert.AreEqual(GuildQuestStatus.Active, detail.Status);
            Assert.IsTrue(detail.IsReportType);
            StringAssert.Contains("[핵심]", detail.Objectives);
            StringAssert.Contains("(0/1)", detail.Objectives);
            Assert.AreEqual("기한 없음", detail.Deadline);
            Assert.AreEqual("골드 10", detail.Reward);
        }

        [Test]
        public void Reasons_PassThrough()
        {
            board.TryAccept("Here");
            Assert.IsFalse(board.CanReport("Here", out var reason));
            Assert.AreEqual("아직 목표를 모두 달성하지 않았습니다", reason);
            Assert.IsFalse(board.CanAccept("Here", out _), "진행 중이면 수락 불가");
        }
    }
}
