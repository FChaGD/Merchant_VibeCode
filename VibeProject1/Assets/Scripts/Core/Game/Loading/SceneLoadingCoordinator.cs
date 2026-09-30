using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 씬 전환 로딩 절차의 작업 목록과 진행을 가진다(Docs/설계/67번 §4.5). 목록 = 씬 불러오기 + (이전 화면 정리) + 화면 연결 +
    /// 대상 씬에 등록된 준비 작업. 불러오기·정리는 SceneLoader가, 화면 연결 이후는 연출 컨트롤러가 이 클래스에 알린다.
    /// 커튼·로딩바를 언제 보여 주고 걷을지는 연출 컨트롤러 책임이다(SRP) - 이 클래스는 화면을 모른다.
    /// Bootstrap 상주·전역 DI 대상이다(준비 작업 등록은 Bootstrap 초기화 때 일어나므로).
    /// </summary>
    public class SceneLoadingCoordinator : MonoBehaviour, ISceneLoadingSequence, ISceneLoadingTaskRegistry, ISceneLoadingProgressReader, IManagedComponent
    {
        private const string UnloadLabel = "이전 화면 정리 중";
        private const string WireLabel = "화면 연결 중";

        private readonly SceneLoadingProgress progress = new();
        private readonly Dictionary<ContentSceneId, List<ISceneLoadingTask>> tasksByScene = new();
        private readonly List<ISceneLoadingTask> pendingTasks = new();
        private Coroutine running;

        public int CompletedCount => progress.CompletedCount;
        public int TotalCount => progress.TotalCount;
        public string CurrentLabel => progress.CurrentLabel;

        public event Action Changed
        {
            add => progress.Changed += value;
            remove => progress.Changed -= value;
        }

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ISceneLoadingSequence>(this);
            registrar.Register<ISceneLoadingTaskRegistry>(this);
            registrar.Register<ISceneLoadingProgressReader>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
        }

        public void Register(ContentSceneId scene, ISceneLoadingTask task)
        {
            if (task == null) return;
            if (!tasksByScene.TryGetValue(scene, out var list)) tasksByScene[scene] = list = new List<ISceneLoadingTask>();
            list.Add(task);
        }

        public void Begin(string sceneName, bool hasPreviousScene)
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }

            pendingTasks.Clear();
            if (Enum.TryParse<ContentSceneId>(sceneName, out var sceneId) && tasksByScene.TryGetValue(sceneId, out var registered))
            {
                pendingTasks.AddRange(registered);
            }

            var labels = new List<string> { $"{DisplayName(sceneName)} 불러오는 중" };
            if (hasPreviousScene) labels.Add(UnloadLabel);
            labels.Add(WireLabel);
            foreach (var task in pendingTasks) labels.Add(task.Label);
            progress.Reset(labels);
        }

        public void CompleteCurrent() => progress.CompleteCurrent();

        public void RunRemaining(Action onComplete)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(RunRemainingRoutine(onComplete));
        }

        private IEnumerator RunRemainingRoutine(Action onComplete)
        {
            // 화면 연결은 로드 완료 이벤트에서 UIManager가 이미 끝냈다(구독 순서 보장, 설계 67번 §2).
            progress.CompleteCurrent();

            foreach (var task in pendingTasks)
            {
                yield return task.Run();
                progress.CompleteCurrent();
            }

            // 100%를 한 프레임은 보여 준 뒤 넘긴다.
            yield return null;
            running = null;
            onComplete?.Invoke();
        }

        private static string DisplayName(string sceneName)
            => Enum.TryParse<ContentSceneId>(sceneName, out var sceneId)
                ? sceneId switch
                {
                    ContentSceneId.Hub => "마을",
                    ContentSceneId.Field => "필드",
                    _ => sceneName,
                }
                : sceneName;
    }
}
