using System.Collections;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class SceneLoadingProgressTests
    {
        private class NoopTask : ISceneLoadingTask
        {
            public string Label { get; }
            public NoopTask(string label) => Label = label;
            public IEnumerator Run() { yield break; }
        }

        [Test]
        public void Progress_CountsCompletedTasks_AndTracksCurrentLabel()
        {
            var progress = new SceneLoadingProgress();
            var changed = 0;
            progress.Changed += () => changed++;

            progress.Reset(new[] { "a", "b", "c" });
            Assert.AreEqual(3, progress.TotalCount);
            Assert.AreEqual(0, progress.Percent);
            Assert.AreEqual("a", progress.CurrentLabel);

            progress.CompleteCurrent();
            Assert.AreEqual(33, progress.Percent);
            Assert.AreEqual("b", progress.CurrentLabel);

            progress.CompleteCurrent();
            progress.CompleteCurrent();
            progress.CompleteCurrent(); // 넘쳐도 100%에서 멈춘다
            Assert.IsTrue(progress.IsComplete);
            Assert.AreEqual(100, progress.Percent);
            Assert.AreEqual("c", progress.CurrentLabel);
            Assert.AreEqual(4, changed); // Reset 1 + 완료 3
        }

        [Test]
        public void Coordinator_Begin_BuildsTaskListForTargetScene()
        {
            var go = new GameObject(nameof(SceneLoadingProgressTests));
            try
            {
                var coordinator = go.AddComponent<SceneLoadingCoordinator>();
                coordinator.Register(ContentSceneId.Hub, new NoopTask("상단 배치 준비 중"));
                coordinator.Register(ContentSceneId.Field, new NoopTask("정비창 준비 중"));

                // 최초 진입: 불러오기 + 연결 + 마을 준비 작업(정리 없음).
                coordinator.Begin("Hub", hasPreviousScene: false);
                Assert.AreEqual(3, coordinator.TotalCount);
                Assert.AreEqual("마을 불러오는 중", coordinator.CurrentLabel);

                // 필드→마을: 정리 작업이 더해진다. 다른 씬의 준비 작업은 들어가지 않는다.
                coordinator.Begin("Hub", hasPreviousScene: true);
                Assert.AreEqual(4, coordinator.TotalCount);
                coordinator.CompleteCurrent();
                Assert.AreEqual("이전 화면 정리 중", coordinator.CurrentLabel);
                coordinator.CompleteCurrent();
                Assert.AreEqual("화면 연결 중", coordinator.CurrentLabel);
                coordinator.CompleteCurrent();
                Assert.AreEqual("상단 배치 준비 중", coordinator.CurrentLabel);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
