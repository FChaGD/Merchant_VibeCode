using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Core.Tests
{
    /// <summary>
    /// 상행 출발 절차(Docs/Refactor/2026-10-26_씬전용.md D-1, SS-3) - 경고는 순차 확인, 모두 확인해야 임시 보관·초과 골드를 버리고 출발한다.
    /// 어느 단계든 취소하면 아무것도 바꾸지 않는다(기획 82번 E4·E5, 설계 83번 §6.3). 확인 창이 없으면 경고 없이 출발한다.
    /// </summary>
    public class TripDepartureFlowTests
    {
        private sealed class Prompt : IConfirmPrompt
        {
            public readonly List<string> Messages = new();
            public Action Confirm;
            public Action Cancel;
            public void Show(string message, string confirmText, string cancelText, Action onConfirm, Action onCancel)
            {
                Messages.Add(message);
                Confirm = onConfirm;
                Cancel = onCancel;
            }
        }

        private sealed class Staging : IInventoryStagingDiscarder
        {
            public List<InventoryItemInstance> Items = new();
            public int Discards;
            public IReadOnlyList<InventoryItemInstance> StagedItems => Items;
#pragma warning disable CS0067
            public event Action OnChanged;
#pragma warning restore CS0067
            public void DiscardStaged() { Discards++; Items.Clear(); }
        }

        private sealed class Gold : IGoldHoldingsReader, IGoldDepartureSettlement
        {
            public int Excess;
            public int Discards;
            public int PersonalGold => 0;
            public int PersonalLimit => 100;
            public int LoadedGold => 0;
            public int FreeCells => 0;
            public int FreeSpaceGold => 0;
            public int OwnedGold => 0;
            public int CarryCapacity => 0;
            public int ExcessGold => Excess;
#pragma warning disable CS0067
            public event Action Changed;
#pragma warning restore CS0067
            public int DiscardExcess() { Discards++; var e = Excess; Excess = 0; return e; }
        }

        private sealed class Departure : ITripDeparture
        {
            public bool Result = true;
            public int Calls;
            public bool TryDepart(int fromCityId, int toCityId) { Calls++; return Result; }
        }

        private sealed class Location : ITripCurrentLocationReader
        {
            public int CurrentCityId => 1;
#pragma warning disable CS0067
            public event Action Changed;
#pragma warning restore CS0067
        }

        private sealed class Destination : ITripDestinationReader
        {
            public int? DestinationCityId => 2;
            public bool IsAssigned => true;
#pragma warning disable CS0067
            public event Action Changed;
#pragma warning restore CS0067
        }

        private Staging staging;
        private Gold gold;
        private Departure departure;
        private int transitions;

        [SetUp]
        public void SetUp()
        {
            staging = new Staging();
            gold = new Gold();
            departure = new Departure();
            transitions = 0;
        }

        private TripDepartureFlow Flow(IConfirmPrompt prompt) => new(prompt, new[] { staging }, gold, gold, departure, new Location(), new Destination(), () => transitions++);

        private static InventoryItemInstance Item() => new("i1", null, new GridPosition(0, 0), 0, false, "W");

        [Test]
        public void NoWarnings_DepartsImmediately()
        {
            var prompt = new Prompt();
            Flow(prompt).Start(hasPlacedUnit: true);
            Assert.AreEqual(0, prompt.Messages.Count);
            Assert.AreEqual(1, staging.Discards);
            Assert.AreEqual(1, gold.Discards);
            Assert.AreEqual(1, departure.Calls);
            Assert.AreEqual(1, transitions);
        }

        [Test]
        public void Warnings_ConfirmAll_Departs()
        {
            staging.Items.Add(Item());
            gold.Excess = 50;
            var prompt = new Prompt();
            Flow(prompt).Start(hasPlacedUnit: false);
            Assert.AreEqual(1, prompt.Messages.Count, "경고는 하나씩 보인다");
            prompt.Confirm();
            prompt.Confirm();
            prompt.Confirm();
            Assert.AreEqual(3, prompt.Messages.Count);
            Assert.AreEqual(1, staging.Discards);
            Assert.AreEqual(1, gold.Discards);
            Assert.AreEqual(1, transitions);
        }

        [Test]
        public void Cancel_AtSecondWarning_ChangesNothing()
        {
            staging.Items.Add(Item());
            var prompt = new Prompt();
            Flow(prompt).Start(hasPlacedUnit: false);
            prompt.Confirm();
            prompt.Cancel?.Invoke();
            Assert.AreEqual(0, staging.Discards);
            Assert.AreEqual(0, gold.Discards);
            Assert.AreEqual(0, departure.Calls);
            Assert.AreEqual(0, transitions);
        }

        [Test]
        public void NoPrompt_DepartsWithoutWarnings()
        {
            staging.Items.Add(Item());
            Flow(null).Start(hasPlacedUnit: false);
            Assert.AreEqual(1, staging.Discards);
            Assert.AreEqual(1, transitions);
        }

        [Test]
        public void DepartFails_StillTransitions()
        {
            departure.Result = false;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("경로를 찾지 못해"));
            Flow(new Prompt()).Start(hasPlacedUnit: true);
            Assert.AreEqual(1, transitions);
        }

        [Test]
        public void CountPlacedUnits_CountsOccupiedSlots()
        {
            var layout = new FormationLayout(3, 2);
            Assert.AreEqual(0, layout.CountPlacedUnits());
            layout.SetUnitId(0, "A");
            layout.SetUnitId(4, "B");
            Assert.AreEqual(2, layout.CountPlacedUnits());
        }
    }
}
