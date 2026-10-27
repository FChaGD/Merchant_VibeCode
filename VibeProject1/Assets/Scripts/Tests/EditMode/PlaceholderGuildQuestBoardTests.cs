using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>상인조합 게시판 Placeholder(Docs/설계/103번 §4.1). 숙소 휴식·마구간 매각은 실제 구현으로 교체돼 InnRestServiceTests·CaravanAssetSaleServiceTests로 옮겼다(설계 104·105).</summary>
    public class PlaceholderGuildQuestBoardTests
    {
        private readonly List<GameObject> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created) UnityEngine.Object.DestroyImmediate(go);
            created.Clear();
        }

        private T Create<T>() where T : MonoBehaviour
        {
            var go = new GameObject(typeof(T).Name);
            created.Add(go);
            return go.AddComponent<T>();
        }

        [Test]
        public void GuildBoard_ThreeQuests_AcceptOnce()
        {
            var board = Create<PlaceholderGuildQuestBoard>();
            board.ResolveDependencies(null);
            var changed = 0;
            board.Changed += () => changed++;

            Assert.AreEqual(3, board.Quests.Count);
            Assert.AreEqual("퀘스트이름1", board.Quests[0].Title);
            Assert.AreEqual("값 없음", board.Quests[0].Requester);
            Assert.IsTrue(board.TryAccept(board.Quests[1].Id));
            Assert.IsFalse(board.TryAccept(board.Quests[1].Id));
            Assert.IsFalse(board.TryAccept("missing"));
            Assert.IsTrue(board.IsAccepted(board.Quests[1].Id));
            Assert.IsFalse(board.IsAccepted(board.Quests[0].Id));
            Assert.AreEqual(1, changed);
        }
    }
}
