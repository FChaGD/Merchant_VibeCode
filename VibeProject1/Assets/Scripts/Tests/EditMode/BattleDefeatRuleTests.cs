using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class BattleDefeatRuleTests
    {
        // 인자 순서: 전체 마차, 남은 마차, 남은 캐릭터, 남은 시설(Docs/기획/73번 §3·§4).
        [Test]
        public void SomeWagonsDestroyed_NotDefeated() => Assert.IsFalse(BattleDefeatRule.IsDefeated(2, 1, 3, 1));

        [Test]
        public void AllWagonsDestroyed_Defeated() => Assert.IsTrue(BattleDefeatRule.IsDefeated(2, 0, 3, 1));

        [Test]
        public void NoWagonsInBattle_WagonConditionNotApplied() => Assert.IsFalse(BattleDefeatRule.IsDefeated(0, 0, 3, 0));

        [Test]
        public void NoCharacters_FacilityRemains_NotDefeated() => Assert.IsFalse(BattleDefeatRule.IsDefeated(1, 1, 0, 1));

        [Test]
        public void NoCharacters_NoFacilities_Defeated() => Assert.IsTrue(BattleDefeatRule.IsDefeated(1, 1, 0, 0));

        [Test]
        public void CharactersRemain_NoFacilities_NotDefeated() => Assert.IsFalse(BattleDefeatRule.IsDefeated(1, 1, 2, 0));

        // 패배 원인(설계 79번 §4.1) - 둘 다 성립하면 마차 전멸이 우선(기획 78번 §4-27).
        [Test]
        public void Cause_AllWagons() => Assert.AreEqual(BattleDefeatCause.AllWagonsDestroyed, BattleDefeatRule.ResolveCause(2, 0, 3, 1));

        [Test]
        public void Cause_NoCombatants() => Assert.AreEqual(BattleDefeatCause.NoCombatants, BattleDefeatRule.ResolveCause(2, 1, 0, 0));

        [Test]
        public void Cause_Both_PrefersWagons() => Assert.AreEqual(BattleDefeatCause.AllWagonsDestroyed, BattleDefeatRule.ResolveCause(1, 0, 0, 0));

        [Test]
        public void Cause_NoWagonBattle_Condition1NotApplied() => Assert.AreEqual(BattleDefeatCause.None, BattleDefeatRule.ResolveCause(0, 0, 1, 0));
    }
}
