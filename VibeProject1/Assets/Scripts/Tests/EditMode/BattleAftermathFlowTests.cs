using System;
using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>
    /// 결과 정리 플로우의 단계 진행·① 버튼 문구 판정(설계 79번 §6, 2026-10-05 검진 지적 1번). 마무리 단계는 "다음" 판정에 세지 않는다.
    /// </summary>
    public class BattleAftermathFlowTests
    {
        private sealed class StubStep : IBattleAftermathStep
        {
            private readonly bool applies;
            private readonly List<string> log;
            private readonly string name;
            public bool AutoComplete = true;

            public StubStep(string name, bool applies, List<string> log)
            {
                this.name = name;
                this.applies = applies;
                this.log = log;
            }

            public bool AppliesTo(BattleAftermathContext context) => applies;

            public void Run(BattleAftermathContext context, Action onDone)
            {
                log.Add(name);
                if (AutoComplete) onDone();
            }
        }

        private static BattleAftermathContext Context() => new BattleAftermathContext(new BattleResult(BattleOutcome.Victory), null, default);

        [Test]
        public void OnlyFinishAfterPopup_HasStepAfterFalse()
        {
            var log = new List<string>();
            var popup = new StubStep("popup", true, log);
            var flow = new BattleAftermathFlow(new IBattleAftermathStep[] { popup, new StubStep("cargo", false, log), new StubStep("repair", false, log) }, new StubStep("finish", true, log));

            Assert.IsFalse(flow.HasStepAfter(popup, Context()));
        }

        [Test]
        public void ApplicableStepAfterPopup_HasStepAfterTrue()
        {
            var log = new List<string>();
            var popup = new StubStep("popup", true, log);
            var flow = new BattleAftermathFlow(new IBattleAftermathStep[] { popup, new StubStep("cargo", false, log), new StubStep("repair", true, log) }, new StubStep("finish", true, log));

            Assert.IsTrue(flow.HasStepAfter(popup, Context()));
        }

        [Test]
        public void Start_RunsApplicableStepsThenFinishOnce()
        {
            var log = new List<string>();
            var flow = new BattleAftermathFlow(new IBattleAftermathStep[] { new StubStep("popup", true, log), new StubStep("cargo", false, log), new StubStep("repair", true, log) }, new StubStep("finish", true, log));

            flow.Start(Context());

            CollectionAssert.AreEqual(new[] { "popup", "repair", "finish" }, log);
        }

        [Test]
        public void Finish_WaitsUntilPreviousStepCompletes()
        {
            var log = new List<string>();
            var popup = new StubStep("popup", true, log) { AutoComplete = false };
            var flow = new BattleAftermathFlow(new IBattleAftermathStep[] { popup }, new StubStep("finish", true, log));

            flow.Start(Context());

            CollectionAssert.AreEqual(new[] { "popup" }, log);
        }
    }
}
