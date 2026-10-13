using NUnit.Framework;

namespace Game.Core.Tests
{
    public class YarnSourceScannerTests
    {
        private const string Source = @"title: A
tags: x
---
merc_a: 안녕하세요. #left
그냥 서술.
질문? #ask
-> 예
    merc_a: 좋아.
-> 아니오 <<if false>> #lock:안_됨
-> (무응답) #timeout
<<battle>>
===
title: B
---
줄
===";

        [Test]
        public void ScansNodesLinesTagsOptionsAndCommands()
        {
            var nodes = YarnSourceScanner.Scan(Source);
            Assert.AreEqual(2, nodes.Count);
            Assert.AreEqual("A", nodes[0].Title);
            var lines = nodes[0].Lines;
            Assert.AreEqual(("merc_a", "안녕하세요."), (lines[0].SpeakerId, lines[0].Text));
            CollectionAssert.AreEqual(new[] { "left" }, lines[0].Tags);
            Assert.AreEqual("", lines[1].SpeakerId);
            CollectionAssert.Contains(lines[2].Tags, "ask");
            Assert.IsTrue(lines[3].IsOption);
            Assert.AreEqual("merc_a", lines[4].SpeakerId);
            Assert.IsTrue(lines[5].IsOption);
            CollectionAssert.Contains(lines[5].Tags, "lock:안_됨");
            CollectionAssert.Contains(lines[6].Tags, "timeout");
            Assert.IsTrue(lines[7].IsCommand);
            Assert.AreEqual("battle", lines[7].CommandName);
        }
    }
}
