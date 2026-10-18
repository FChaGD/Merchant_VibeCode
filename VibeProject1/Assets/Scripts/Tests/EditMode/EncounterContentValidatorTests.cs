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
        public void Table_EventIdWithDashOrReserved_IsReported()
        {
            // 사건 Id `이벤트Id-MMDD-순번`이 겹치지 않게(설계 89 §5.2) - 구분자 `-`와 예약 접두(debug·event)는 이벤트 Id로 쓸 수 없다.
            var events = new List<EventEntry>
            {
                new() { Id = "E1", YarnNode = "N" },
                new() { Id = "E-2", YarnNode = "N" },
                new() { Id = "debug", YarnNode = "N" },
                new() { Id = "event", YarnNode = "N" },
            };
            var errors = EncounterContentValidator.ValidateTable(events, new List<DialogueEntry>(), _ => true);
            Assert.AreEqual(3, errors.Count);
            Assert.IsTrue(errors.Exists(e => e.Contains("'E-2'")));
            Assert.IsTrue(errors.Exists(e => e.Contains("'debug'")));
            Assert.IsTrue(errors.Exists(e => e.Contains("'event'")));
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

        private static EventScriptRules Rules() => new()
        {
            ItemExists = id => id == "wheat",
            CharacterExists = id => id == "Escort01",
            SpeakerExists = id => id == "bandit" || id == "Warrior01",
        };

        private static List<string> ValidateEvent(string source)
        {
            var nodes = YarnSourceScanner.Scan(source);
            return EncounterContentValidator.ValidateEventScripts(nodes, new HashSet<string> { "E" }, Rules());
        }

        [Test]
        public void Event_ValidScript_NoErrors()
        {
            var errors = ValidateEvent(@"title: E
---
bandit: 내라. #right
무리가 막는다. #choice #people:bandit,Warrior01
-> 낸다 <<if gold() >= 300>> #lock:골드_300_필요
    <<gold -300>>
-> 거절
    <<battle>>
    <<goods_add wheat 2>>
    <<goods_remove random 1>>
    <<difficulty 10 days 3>>
    <<difficulty -5 leg>>
    <<delay -10>>
    <<join Escort01>>
===");
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Event_EverySpecCommand_AcceptedWithValidArgs()
        {
            var sample = new Dictionary<EventArgKind, string>
            {
                [EventArgKind.Int] = "-1", [EventArgKind.PositiveInt] = "1", [EventArgKind.Seconds] = "1.5",
                [EventArgKind.ItemId] = "wheat", [EventArgKind.ItemIdOrRandom] = "random", [EventArgKind.CharacterId] = "Escort01",
                [EventArgKind.DifficultyMode] = "leg",
            };
            var lines = new System.Text.StringBuilder();
            foreach (var spec in EventCommandSpecs.Commands)
            {
                var args = new List<string>();
                for (var i = 0; i < spec.MinArgs; i++) args.Add(sample[spec.Args[i]]);
                lines.Append("<<").Append(spec.Name).Append(args.Count > 0 ? " " + string.Join(" ", args) : "").Append(">>\n");
            }
            var errors = ValidateEvent($"title: E\n---\n{lines}===");
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Event_OptionsWithoutChoiceLine_Error()
        {
            var errors = ValidateEvent(@"title: E
---
bandit: 내라. #right
-> 낸다
-> 거절
===");
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("#choice", errors[0]);
        }

        [Test]
        public void Event_BadCommands_Errors()
        {
            var errors = ValidateEvent(@"title: E
---
<<gold>>
<<goods_add ghost 1>>
<<goods_remove wheat 0>>
<<difficulty 10 days>>
<<difficulty 10 sometimes>>
<<join ghost>>
<<explode>>
===");
            Assert.AreEqual(7, errors.Count, string.Join("\n", errors));
        }

        [Test]
        public void Event_UnknownPeople_Error()
        {
            var errors = ValidateEvent(@"title: E
---
설명. #choice #people:ghost
-> 가
===");
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
        }

        [Test]
        public void NonEventNode_NotChecked()
        {
            var nodes = YarnSourceScanner.Scan("title: D\n---\n<<explode>>\n===");
            CollectionAssert.IsEmpty(EncounterContentValidator.ValidateEventScripts(nodes, new HashSet<string> { "E" }, Rules()));
        }
    }
}
