using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    /// <summary>퀘스트 상태 전이(Docs/설계/107번 §6.3·§6.4).</summary>
    public class QuestManagerTests
    {
        private static readonly GoldTestItem Box = new("box", 1, 1);
        private static readonly GoldTestItem Wheat = new("wheat", 1, 1);
        private static readonly GoldTestItem Herb = new("herb", 1, 1);

        private GameObject go;
        private GoldTestInventory inventory;
        private QuestTestDate date;
        private QuestTestArrivals arrivals;
        private QuestTestBattles battles;
        private QuestTestEvents events;
        private QuestTestReputation reputation;
        private QuestTestIncome income;
        private GoldTestWallet wallet;
        private readonly List<string> warnings = new();

        [SetUp]
        public void SetUp()
        {
            go = new GameObject(nameof(QuestManager));
            inventory = new GoldTestInventory(GoldTestInventory.Section("A", 3, 1));
            inventory.AddDefinition(Box);
            inventory.AddDefinition(Wheat);
            inventory.AddDefinition(Herb);
            date = new QuestTestDate();
            arrivals = new QuestTestArrivals();
            battles = new QuestTestBattles();
            events = new QuestTestEvents();
            reputation = new QuestTestReputation();
            income = new QuestTestIncome();
            wallet = new GoldTestWallet(100);
            warnings.Clear();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        private QuestManager Create(params QuestDefinition[] defs)
        {
            var manager = go.AddComponent<QuestManager>();
            manager.Initialize(defs, new QuestManagerSources
            {
                Date = date,
                Reputation = reputation,
                Inventory = inventory,
                Settlement = new CargoTestSettlement(inventory),
                Gold = income,
                Wallet = wallet,
                Battles = battles,
                Arrivals = arrivals,
                Events = events,
                Location = new QuestTestLocation(),
                Warn = warnings.Add,
            });
            return manager;
        }

        private static QuestObjectiveDefinition Obj(string id, QuestObjectiveType type, QuestObjectiveRole role = QuestObjectiveRole.Core, int city = 0, string item = null, int count = 0)
            => new() { Id = id, Role = role, Type = type, CityId = city, ItemId = item, Count = count };

        private static QuestRewardDefinition Gold(int amount, string bonus = null) => new() { Type = QuestRewardType.Gold, Amount = amount, ObjectiveId = bonus };

        // 수락 시 상자 1개 지급, 도시 2로 납품(즉시 완료), 성공 골드 100.
        private static QuestDefinition Delivery(string id = "D")
        {
            return new QuestDefinition
            {
                Id = id,
                Title = id + "이름",
                Kind = QuestKind.General,
                Acceptance = QuestAcceptance.Request,
                Completion = QuestCompletion.Instant,
                BoardCityIds = new List<int> { 1 },
                Objectives = new List<QuestObjectiveDefinition> { Obj("d", QuestObjectiveType.Deliver, city: 2, item: "box", count: 1) },
                Cargo = new List<QuestCargoDefinition> { new() { ItemId = "box", Count = 1 } },
                Rewards = new List<QuestRewardDefinition> { Gold(100) },
            };
        }

        // 보고형 조달 납품 - 밀 2개를 도시 1에서 보고.
        private static QuestDefinition Procure()
        {
            return new QuestDefinition
            {
                Id = "R",
                Kind = QuestKind.General,
                Acceptance = QuestAcceptance.Request,
                Completion = QuestCompletion.Report,
                ReportCityId = 1,
                BoardCityIds = new List<int> { 1 },
                Objectives = new List<QuestObjectiveDefinition> { Obj("d", QuestObjectiveType.Deliver, city: 1, item: "wheat", count: 2) },
                Rewards = new List<QuestRewardDefinition> { Gold(50) },
            };
        }

        private static QuestObjectiveDefinition Deadline(int days, QuestObjectiveRole role = QuestObjectiveRole.Core)
        {
            var o = Obj("t", QuestObjectiveType.Deadline, role);
            o.HasDeadlineDays = true;
            o.DeadlineDays = days;
            return o;
        }

        private static bool Accept(QuestManager manager, string id, int city = 1) => manager.TryStart(id, QuestStartSource.Request, city, out _);

        [Test]
        public void Accept_GivesCargoAndCreatesInstance()
        {
            var manager = Create(Delivery());
            Assert.AreEqual(1, manager.PostedAt(1).Count);
            Assert.IsTrue(Accept(manager, "D"));
            Assert.AreEqual(1, inventory.Items.Count);
            Assert.IsTrue(manager.TryGetInstance("D", out var instance));
            Assert.AreEqual(QuestState.Active, instance.State);
            Assert.AreEqual(1, instance.GivenCargoCount);
            Assert.AreEqual(0, manager.PostedAt(1).Count, "진행 중이면 게시되지 않는다");
        }

        [Test]
        public void Accept_NoSpace_Refused()
        {
            inventory.Place(Wheat, 0, 0, "A");
            inventory.Place(Wheat, 1, 0, "A");
            inventory.Place(Wheat, 2, 0, "A");
            var manager = Create(Delivery());
            Assert.IsFalse(manager.CanAccept("D", 1, out var reason));
            Assert.AreEqual("적재 공간이 부족합니다", reason);
            Assert.IsFalse(Accept(manager, "D"));
            Assert.IsFalse(manager.TryGetInstance("D", out _));
        }

        [Test]
        public void InstantArrival_CompletesAndRewards()
        {
            var manager = Create(Delivery());
            Accept(manager, "D");
            arrivals.Arrive(3);
            Assert.IsTrue(manager.TryGetInstance("D", out _), "다른 도시 도착은 무관");
            arrivals.Arrive(2);
            Assert.IsFalse(manager.TryGetInstance("D", out _));
            Assert.AreEqual(100, income.Total);
            Assert.AreEqual(0, inventory.Items.Count, "납품 물품 차감");
            CollectionAssert.Contains(manager.Posting.CompletedIds, "D");
        }

        [Test]
        public void InstantArrival_OverflowStagesAndRaisesRewardStaged()
        {
            var def = Delivery();
            def.Rewards.Add(new QuestRewardDefinition { Type = QuestRewardType.Item, ItemId = "herb", Amount = 4 });
            var manager = Create(def);
            var raised = 0;
            manager.RewardStaged += () => raised++;
            Accept(manager, "D");
            arrivals.Arrive(2);
            Assert.AreEqual(3, inventory.Items.Count);
            Assert.AreEqual(1, inventory.StagedItems.Count);
            Assert.AreEqual(1, raised);
            Assert.IsTrue(manager.HasPendingCleanup);
        }

        [Test]
        public void Report_DeductsAndRewards()
        {
            var manager = Create(Procure());
            Accept(manager, "R");
            Assert.IsFalse(manager.CanReport("R", 1, out _));
            inventory.Place(Wheat, 0, 0, "A");
            inventory.Place(Wheat, 1, 0, "A");
            manager.TryGetInstance("R", out var instance);
            Assert.AreEqual(QuestState.Reportable, instance.State);
            Assert.IsFalse(manager.CanReport("R", 2, out var wrongCity));
            Assert.AreEqual("이 상인조합은 보고처가 아닙니다", wrongCity);
            Assert.IsTrue(manager.TryReport("R", 1, out _));
            Assert.AreEqual(50, income.Total);
            Assert.AreEqual(0, inventory.Items.Count);
        }

        [Test]
        public void Report_NoRewardSpace_Blocked()
        {
            var def = Procure();
            def.Rewards.Add(new QuestRewardDefinition { Type = QuestRewardType.Item, ItemId = "herb", Amount = 3 });
            var manager = Create(def);
            Accept(manager, "R");
            inventory.Place(Wheat, 0, 0, "A");
            inventory.Place(Wheat, 1, 0, "A");
            inventory.Place(Herb, 2, 0, "A");
            // 밀 2개를 뺀 자리는 2칸 - 약초 3개는 못 들어간다.
            Assert.IsFalse(manager.CanReport("R", 1, out var reason));
            Assert.AreEqual("보상을 받을 적재 공간이 부족합니다", reason);
            Assert.IsFalse(manager.TryReport("R", 1, out _));
            Assert.IsTrue(manager.TryGetInstance("R", out _));
        }

        [Test]
        public void Report_RewardFitsIntoDeliveredSpace()
        {
            var def = Procure();
            def.Rewards.Add(new QuestRewardDefinition { Type = QuestRewardType.Item, ItemId = "herb", Amount = 2 });
            var manager = Create(def);
            Accept(manager, "R");
            inventory.Place(Wheat, 0, 0, "A");
            inventory.Place(Wheat, 1, 0, "A");
            inventory.Place(Herb, 2, 0, "A");
            Assert.IsTrue(manager.CanReport("R", 1, out _), "가득 찼어도 납품으로 빠질 밀 2칸에 약초 2개가 들어간다");
            Assert.IsTrue(manager.TryReport("R", 1, out _));
            Assert.AreEqual(3, inventory.Items.Count(i => i.Definition.Id == "herb"));
            Assert.AreEqual(0, inventory.StagedItems.Count);
        }

        [Test]
        public void Report_SharedItem_OtherQuestReturnsToActive()
        {
            var first = Procure();
            var second = Procure();
            second.Id = "R2";
            var manager = Create(first, second);
            Accept(manager, "R");
            Accept(manager, "R2");
            inventory.Place(Wheat, 0, 0, "A");
            inventory.Place(Wheat, 1, 0, "A");
            manager.TryGetInstance("R2", out var other);
            Assert.AreEqual(QuestState.Reportable, other.State);
            Assert.IsTrue(manager.TryReport("R", 1, out _));
            Assert.AreEqual(QuestState.Active, other.State, "같은 밀을 납품해 사라졌으니 다른 퀘스트는 진행 중으로 돌아간다");
        }

        [Test]
        public void Accept_ExpiredFixedDeadline_Refused()
        {
            var def = Procure();
            var deadline = Obj("t", QuestObjectiveType.Deadline);
            deadline.HasDeadlineDate = true;
            deadline.DeadlineTotalDays = 5;
            def.Objectives.Add(deadline);
            var manager = Create(def);
            date.Set(5);
            Assert.IsTrue(manager.CanAccept("R", 1, out _), "마감일 당일은 수락할 수 있다");
            date.Set(6);
            Assert.IsFalse(manager.CanAccept("R", 1, out var reason));
            Assert.AreEqual("기한이 지난 의뢰입니다", reason);
            Assert.IsFalse(Accept(manager, "R"));
        }

        [Test]
        public void TriggerStart_ExpiredFixedDeadline_FailsImmediately()
        {
            var def = Procure();
            var deadline = Obj("t", QuestObjectiveType.Deadline);
            deadline.HasDeadlineDate = true;
            deadline.DeadlineTotalDays = 5;
            def.Objectives.Add(deadline);
            def.FailReputation = -2;
            var manager = Create(def);
            date.Set(6);
            Assert.IsTrue(manager.TryStart("R", QuestStartSource.Trigger, 1, out _));
            Assert.IsFalse(manager.TryGetInstance("R", out _));
            Assert.AreEqual(-2, reputation.Value);
        }

        [Test]
        public void BonusChanged_OnlyWhenBonusQuestSetChanges()
        {
            var def = Delivery();
            def.Effects.Add(new QuestEffectDefinition { Scope = QuestEffectScope.AllTrips, DifficultyBonus = 5 });
            var manager = Create(def, Procure());
            var raised = 0;
            manager.BonusChanged += () => raised++;
            Accept(manager, "R");
            Assert.AreEqual(0, raised, "가산 없는 퀘스트 수락");
            Accept(manager, "D");
            Assert.AreEqual(1, raised);
            date.Set(1);
            date.Set(2);
            Assert.AreEqual(1, raised, "날짜만 바뀌면 알리지 않는다");
            arrivals.Arrive(2);
            Assert.AreEqual(2, raised, "가산 퀘스트 완료");
        }

        [Test]
        public void Report_LosingItems_ReturnsToActive()
        {
            var manager = Create(Procure());
            Accept(manager, "R");
            inventory.Place(Wheat, 0, 0, "A");
            var second = inventory.Place(Wheat, 1, 0, "A");
            inventory.RemoveItem(second.InstanceId);
            manager.TryGetInstance("R", out var instance);
            Assert.AreEqual(QuestState.Active, instance.State);
            Assert.IsFalse(manager.CanReport("R", 1, out _));
        }

        [Test]
        public void CoreDeadlineExpired_FailsWithPenaltyAndCargoReturn()
        {
            var def = Delivery();
            def.Objectives.Add(Deadline(3));
            def.FailReputation = -5;
            def.FailGold = 30;
            def.ReturnCargoOnFail = true;
            var manager = Create(def);
            Accept(manager, "D");
            date.Set(3);
            Assert.IsTrue(manager.TryGetInstance("D", out _));
            date.Set(4);
            Assert.IsFalse(manager.TryGetInstance("D", out _));
            Assert.AreEqual(-5, reputation.Value);
            Assert.AreEqual(70, wallet.CurrentAmount);
            Assert.AreEqual(0, inventory.Items.Count, "지급 화물 회수");
        }

        [Test]
        public void ReportableDeadlineExpired_FailsWithoutDeduction()
        {
            var def = Procure();
            def.Objectives.Add(Deadline(3));
            def.FailReputation = -5;
            var manager = Create(def);
            Accept(manager, "R");
            inventory.Place(Wheat, 0, 0, "A");
            inventory.Place(Wheat, 1, 0, "A");
            manager.TryGetInstance("R", out var instance);
            Assert.AreEqual(QuestState.Reportable, instance.State);
            date.Set(3);
            Assert.IsTrue(manager.TryGetInstance("R", out _), "마감일 당일은 아직 보고할 수 있다");
            date.Set(4);
            Assert.IsFalse(manager.TryGetInstance("R", out _), "기한 안에 보고하지 않으면 실패");
            Assert.AreEqual(QuestState.Failed, instance.State);
            Assert.AreEqual(-5, reputation.Value);
            Assert.AreEqual(2, inventory.Items.Count, "실패는 납품 물품을 차감하지 않는다");
        }

        [Test]
        public void ReturnCargoOnFalse_KeepsCargo()
        {
            var manager = Create(Delivery());
            Accept(manager, "D");
            Assert.IsTrue(manager.TryAbandon("D", out _));
            Assert.AreEqual(1, inventory.Items.Count);
        }

        [Test]
        public void Abandon_MainRefused()
        {
            var def = Delivery();
            def.Kind = QuestKind.Main;
            var manager = Create(def);
            Assert.IsFalse(Accept(manager, "D"), "메인은 게시판에 걸리지 않는다");
            Assert.IsTrue(manager.TryStart("D", QuestStartSource.Trigger, 1, out _));
            Assert.IsFalse(manager.TryAbandon("D", out var reason));
            Assert.AreEqual("메인 퀘스트는 포기할 수 없습니다", reason);
        }

        [Test]
        public void Abandon_General_PenaltyAndRepost()
        {
            var def = Delivery();
            def.FailReputation = -3;
            def.HasRepostDays = true;
            def.RepostDays = 2;
            var manager = Create(def);
            Accept(manager, "D");
            manager.TryAbandon("D", out _);
            Assert.AreEqual(-3, reputation.Value);
            Assert.AreEqual(0, manager.PostedAt(1).Count);
            date.Set(2);
            Assert.AreEqual(1, manager.PostedAt(1).Count);
        }

        [Test]
        public void RepeatableReposts_OnceFailNeverWhenNoRepostDays()
        {
            var repeat = Delivery("Rep");
            repeat.Repeat = QuestRepeat.Repeatable;
            repeat.HasRepostDays = true;
            repeat.RepostDays = 1;
            var once = Delivery("One");
            var manager = Create(repeat, once);

            Accept(manager, "Rep");
            arrivals.Arrive(2);
            Assert.IsFalse(manager.PostedAt(1).Any(d =>d.Id == "Rep"));
            date.Set(1);
            Assert.IsTrue(manager.PostedAt(1).Any(d =>d.Id == "Rep"), "반복성은 일수 뒤 재게시");

            Accept(manager, "One");
            manager.TryAbandon("One", out _);
            date.Set(50);
            Assert.IsFalse(manager.PostedAt(1).Any(d =>d.Id == "One"), "단발성 실패 + 재게시 일수 없음 = 영구 소멸");
        }

        [Test]
        public void FollowUpBoard_Unlocks()
        {
            var first = Delivery("A");
            first.FollowUps.Add(new QuestFollowUpDefinition { NextQuestId = "B", Mode = QuestFollowUpMode.Board });
            var second = Procure();
            second.Id = "B";
            second.RequiredQuestId = "Z"; // 선행 조건과 별개로 후속 해금이면 게시된다
            var manager = Create(first, second);
            Assert.IsFalse(manager.PostedAt(1).Any(d =>d.Id == "B"));
            Accept(manager, "A");
            arrivals.Arrive(2);
            Assert.IsTrue(manager.PostedAt(1).Any(d =>d.Id == "B"));
        }

        [Test]
        public void FollowUpAutoAccept_StartsOrFallsBackToBoard()
        {
            var first = Delivery("A");
            first.FollowUps.Add(new QuestFollowUpDefinition { NextQuestId = "B", Mode = QuestFollowUpMode.AutoAccept });
            var second = Delivery("B");
            var manager = Create(first, second);
            Accept(manager, "A");
            arrivals.Arrive(2);
            Assert.IsTrue(manager.TryGetInstance("B", out _), "공간이 있으면 자동 수락");

            var third = Delivery("C");
            third.FollowUps.Add(new QuestFollowUpDefinition { NextQuestId = "E", Mode = QuestFollowUpMode.AutoAccept });
            var fourth = Delivery("E");
            fourth.Cargo[0].Count = 5; // 3칸에 5개 - 지급 불가
            var other = go.AddComponent<QuestManager>();
            inventory.DiscardStaged();
            other.Initialize(new[] { third, fourth }, new QuestManagerSources { Date = date, Inventory = inventory, Settlement = new CargoTestSettlement(inventory), Arrivals = arrivals, Warn = warnings.Add });
            manager.TryAbandon("B", out _);
            other.TryStart("C", QuestStartSource.Request, 1, out _);
            arrivals.Arrive(2);
            Assert.IsFalse(other.TryGetInstance("E", out _));
            CollectionAssert.Contains(other.Posting.UnlockedIds, "E");
        }

        [Test]
        public void FollowUpRequiresBonus()
        {
            var first = Delivery("A");
            first.Objectives.Add(Obj("w", QuestObjectiveType.BattleWin, QuestObjectiveRole.Bonus, count: 1));
            first.FollowUps.Add(new QuestFollowUpDefinition { NextQuestId = "B", Mode = QuestFollowUpMode.Board, RequiredBonusId = "w" });
            var second = Procure();
            second.Id = "B";
            second.RequiredQuestId = "Z";
            var manager = Create(first, second);
            Accept(manager, "A");
            arrivals.Arrive(2);
            Assert.IsFalse(manager.PostedAt(1).Any(d =>d.Id == "B"), "보조 미달이면 후속 없음");
        }

        [Test]
        public void ImmediateBonusPaidOnce()
        {
            var def = Delivery();
            def.BonusTiming = QuestBonusTiming.Immediate;
            def.Objectives.Add(Obj("w", QuestObjectiveType.BattleWin, QuestObjectiveRole.Bonus, count: 1));
            def.Rewards.Add(Gold(7, "w"));
            var manager = Create(def);
            Accept(manager, "D");
            battles.Win("Marauder");
            Assert.AreEqual(7, income.Total);
            battles.Win("Marauder");
            arrivals.Arrive(2);
            Assert.AreEqual(107, income.Total, "보조 보상은 한 번만");
        }

        [Test]
        public void OwnDeduction_DoesNotFailPreserve()
        {
            var def = Delivery();
            def.Objectives.Add(Obj("p", QuestObjectiveType.Preserve, QuestObjectiveRole.Bonus));
            def.Rewards.Add(Gold(5, "p"));
            var manager = Create(def);
            Accept(manager, "D");
            arrivals.Arrive(2);
            Assert.AreEqual(105, income.Total, "납품 차감은 의뢰 화물 손실이 아니다");
        }

        [Test]
        public void TwoQuestsCompleteOnSameArrival()
        {
            var manager = Create(Delivery("A"), Delivery("B"));
            Accept(manager, "A");
            Accept(manager, "B");
            arrivals.Arrive(2);
            Assert.AreEqual(0, manager.Active.Count);
            Assert.AreEqual(200, income.Total);
        }

        [Test]
        public void DateJumpsPastDeadlineAndRepost()
        {
            var def = Delivery();
            def.Objectives.Add(Deadline(2));
            def.Repeat = QuestRepeat.Repeatable;
            def.HasRepostDays = true;
            def.RepostDays = 3;
            var manager = Create(def);
            Accept(manager, "D");
            date.Set(10); // 숙소 휴식처럼 한 번에 여러 날
            Assert.IsFalse(manager.TryGetInstance("D", out _));
            Assert.AreEqual(0, manager.PostedAt(1).Count);
            date.Set(20);
            Assert.AreEqual(1, manager.PostedAt(1).Count);
        }

        [Test]
        public void BattleWin_QuestEventOnly()
        {
            var def = Delivery();
            var win = Obj("w", QuestObjectiveType.BattleWin, count: 1);
            win.QuestBattleOnly = true;
            def.Objectives[0] = win;
            def.Cargo.Clear();
            def.Effects.Add(new QuestEffectDefinition { Scope = QuestEffectScope.AllTrips, EventId = "QE" });
            var manager = Create(def);
            Accept(manager, "D");
            battles.Win("Marauder");
            Assert.IsTrue(manager.TryGetInstance("D", out _), "일반 전투는 세지 않는다");
            events.Start("Other");
            battles.Win("Marauder");
            Assert.IsTrue(manager.TryGetInstance("D", out _), "다른 이벤트의 전투도 세지 않는다");
            events.Start("QE");
            battles.Win("Marauder");
            Assert.IsFalse(manager.TryGetInstance("D", out _));
        }

        [Test]
        public void MainFailure_RaisesEvent()
        {
            var def = Delivery();
            def.Kind = QuestKind.Main;
            def.Objectives.Add(Deadline(1));
            var manager = Create(def);
            string failed = null;
            manager.MainQuestFailed += id => failed = id;
            manager.TryStart("D", QuestStartSource.Trigger, 1, out _);
            date.Set(5);
            Assert.AreEqual("D", failed);
        }

        [Test]
        public void CargoWarnings_OnlyActiveGivenCargo()
        {
            var manager = Create(Delivery("A"), Procure());
            Accept(manager, "R");
            CollectionAssert.IsEmpty(manager.Describe(new[] { "box", "wheat" }), "조달 물품은 의뢰 화물이 아니다");
            Accept(manager, "A");
            CollectionAssert.AreEqual(new[] { "A이름" }, manager.Describe(new[] { "box", "box" }));
        }

        [Test]
        public void PendingClearedOnTripStart()
        {
            var def = Delivery();
            def.Rewards.Add(new QuestRewardDefinition { Type = QuestRewardType.Item, ItemId = "herb", Amount = 4 });
            var milestones = new QuestTestMilestones();
            var manager = go.AddComponent<QuestManager>();
            manager.Initialize(new[] { def }, new QuestManagerSources { Inventory = inventory, Settlement = new CargoTestSettlement(inventory), Arrivals = arrivals, Milestones = milestones, Warn = warnings.Add });
            manager.TryStart("D", QuestStartSource.Request, 1, out _);
            arrivals.Arrive(2);
            Assert.IsTrue(manager.HasPendingCleanup);
            milestones.Raise(TripMilestone.TripStarted);
            Assert.IsFalse(manager.HasPendingCleanup, "정리하지 못한 표시는 다음 상행에서 열리지 않는다");
        }

        [Test]
        public void GivenCargoLost_Fails()
        {
            var manager = Create(Delivery());
            Accept(manager, "D");
            inventory.TryDiscard(new[] { new List<InventoryItemInstance>(inventory.Items)[0].InstanceId });
            Assert.IsFalse(manager.TryGetInstance("D", out _));
        }
    }
}
