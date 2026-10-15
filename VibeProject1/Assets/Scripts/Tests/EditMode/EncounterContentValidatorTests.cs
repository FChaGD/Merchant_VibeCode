using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class EncounterContentValidatorTests
    {
        [Test]
        public void Table_MissingNode_IsReported()
        {
            var events = new List<EventEntry> { new() { Id = "E1", YarnNode = "E1_Node" } };
            var dialogues = new List<DialogueEntry> { new() { Id = "D1", YarnNode = "Missing" } };
            var errors = EncounterContentValidator.ValidateTable(events, dialogues, n => n == "E1_Node");
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("Missing", errors[0]);
        }

        [Test]
        public void Scripts_UnknownSpeaker_AndTwoTimeouts()
        {
            var nodes = YarnSourceScanner.Scan(@"title: N
---
ghost: 누구?
-> 예
-> (무응답1) #timeout
-> (무응답2) #timeout
===");
            var errors = EncounterContentValidator.ValidateScripts(nodes, id => id != "ghost");
            Assert.IsTrue(errors.Exists(e => e.Contains("ghost")));
            Assert.IsTrue(errors.Exists(e => e.Contains("#timeout")));
        }

        [Test]
        public void Scripts_ValidNode_HasNoErrors()
        {
            // 선택지 앞 줄의 #ask는 선택 사항이다 - Yarn이 그 줄에 lastline을 자동으로 붙인다.
            var nodes = YarnSourceScanner.Scan(@"title: N
---
merc_a: 질문? #left
-> 예
    merc_a: 좋아.
-> 아니오
-> (무응답) #timeout
===");
            CollectionAssert.IsEmpty(EncounterContentValidator.ValidateScripts(nodes, _ => true));
        }

        [Test]
        public void Scripts_TimeoutCountedPerGroup()
        {
            var nodes = YarnSourceScanner.Scan(@"title: N
---
질문1?
-> 예
-> (무응답) #timeout
중간 줄.
질문2?
-> 아니오
-> (무응답) #timeout
===");
            CollectionAssert.IsEmpty(EncounterContentValidator.ValidateScripts(nodes, _ => true));
        }

        [Test]
        public void Scripts_SpeakerFirstLineNeedsSide()
        {
            var nodes = YarnSourceScanner.Scan(@"title: N
---
merc_a: 첫 대사
merc_a: 둘째 대사 #left
===");
            var errors = EncounterContentValidator.ValidateScripts(nodes, _ => true);
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("merc_a", errors[0]);
        }

        [Test]
        public void Scripts_TwoSpeakersOnSameSide_IsReported()
        {
            var nodes = YarnSourceScanner.Scan(@"title: N
---
merc_a: 하나 #left
merc_b: 둘 #left
서술 줄은 자리가 없다.
===");
            var errors = EncounterContentValidator.ValidateScripts(nodes, _ => true);
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("merc_b", errors[0]);
        }
    }
}
