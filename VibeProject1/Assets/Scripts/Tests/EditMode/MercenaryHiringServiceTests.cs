using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Core;
using Object = UnityEngine.Object;

namespace Game.Core.Tests
{
    public class MercenaryHiringServiceTests
    {
        // 고용 서비스는 IHiredCharacterRoster 계약만 쓴다 - 직업 정보는 후보 프로필에서 받으므로 Id → 직업만 기억한다.
        private class FakeRoster : IHiredCharacterRoster
        {
            private readonly Dictionary<string, string> classById = new();
            private readonly Dictionary<string, string> knownClasses = new();
            public bool FailNextAdd { get; set; }
            public event Action OnHiredChanged;

            public void Know(CharacterProfile profile) => knownClasses[profile.CharacterId] = profile.MercenaryClass;

            public bool IsHired(string characterId) => classById.ContainsKey(characterId);

            public int CountHiredOfClass(string mercenaryClass)
            {
                var count = 0;
                foreach (var value in classById.Values)
                {
                    if (value == mercenaryClass) count++;
                }
                return count;
            }

            public bool TryAddHired(string characterId)
            {
                if (FailNextAdd || IsHired(characterId) || !knownClasses.TryGetValue(characterId, out var mercenaryClass)) return false;
                classById[characterId] = mercenaryClass;
                OnHiredChanged?.Invoke();
                return true;
            }
        }

        private GameObject gameObject;
        private InMemoryPlayerCurrencyWallet wallet;
        private FakeRoster roster;
        private MercenaryHiringService service;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject(nameof(MercenaryHiringServiceTests));
            wallet = gameObject.AddComponent<InMemoryPlayerCurrencyWallet>();
            wallet.ResolveDependencies(null); // 기본 소지 재화 상한만큼 가득 찬 상태로 시작
            roster = new FakeRoster();
            service = new MercenaryHiringService(wallet, roster);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        private CharacterProfile Profile(string id, string mercenaryClass, int cost)
        {
            var profile = new CharacterProfile(id, id, mercenaryClass, mercenaryClass, new BattleUnitStats(100, 10, 5, 3, 1, 1.5f, 5), cost);
            roster.Know(profile);
            return profile;
        }

        [Test]
        public void TryHire_Success_DeductsAndAddsToRoster()
        {
            var candidate = Profile("Warrior03", "Warrior", 1000);
            var startingAmount = wallet.CurrentAmount;

            Assert.AreEqual(MercenaryHireCheck.Available, service.Evaluate(candidate));
            Assert.IsTrue(service.TryHire(candidate));
            Assert.AreEqual(startingAmount - 1000, wallet.CurrentAmount);
            Assert.IsTrue(roster.IsHired("Warrior03"));
        }

        [Test]
        public void Evaluate_InsufficientFunds_BlocksHire()
        {
            wallet.TrySpend(wallet.CurrentAmount - 999);
            var candidate = Profile("Warrior03", "Warrior", 1000);

            Assert.AreEqual(MercenaryHireCheck.InsufficientFunds, service.Evaluate(candidate));
            Assert.IsFalse(service.TryHire(candidate));
            Assert.AreEqual(999, wallet.CurrentAmount);
            Assert.IsFalse(roster.IsHired("Warrior03"));
        }

        [Test]
        public void Evaluate_ClassFull_ReportsRosterFull()
        {
            for (var i = 1; i <= MercenaryHiringService.MaxOwnedPerClass; i++)
            {
                Assert.IsTrue(service.TryHire(Profile($"Archer0{i}", "Archer", 1)));
            }

            var sixth = Profile("Archer06", "Archer", 1);
            Assert.AreEqual(MercenaryHireCheck.RosterFull, service.Evaluate(sixth));
            Assert.IsFalse(service.TryHire(sixth));
            // 다른 직업은 영향받지 않는다.
            Assert.AreEqual(MercenaryHireCheck.Available, service.Evaluate(Profile("Warrior01", "Warrior", 1)));
        }

        [Test]
        public void Evaluate_AlreadyHired_BlocksSecondHire()
        {
            var candidate = Profile("Warrior03", "Warrior", 100);
            Assert.IsTrue(service.TryHire(candidate));

            Assert.AreEqual(MercenaryHireCheck.AlreadyHired, service.Evaluate(candidate));
            Assert.IsFalse(service.TryHire(candidate));
        }

        [Test]
        public void TryHire_RosterAddFails_RefundsCurrency()
        {
            var candidate = Profile("Warrior03", "Warrior", 1000);
            var startingAmount = wallet.CurrentAmount;
            roster.FailNextAdd = true;

            Assert.IsFalse(service.TryHire(candidate));
            Assert.AreEqual(startingAmount, wallet.CurrentAmount);
            Assert.IsFalse(roster.IsHired("Warrior03"));
        }
    }
}
