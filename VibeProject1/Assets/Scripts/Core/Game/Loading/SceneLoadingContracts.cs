using System;
using System.Collections;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 씬 로딩 중 실행되는 작업 1개(Docs/설계/67번 §4.1). 로딩바는 작업이 "끝났는지"만 본다 - 작업 내부 진행(칸 1개 생성 등)은
    /// 보고하지 않는다(사용자 결정: 앞으로 어떤 작업이 추가될지 모르므로 작업 단위로만 연동). 여러 프레임에 걸쳐도, 한 프레임에
    /// 끝나도 된다.
    /// </summary>
    public interface ISceneLoadingTask
    {
        string Label { get; }
        IEnumerator Run();
    }

    /// <summary>
    /// 대상 씬에 들어갈 때마다 실행할 준비 작업 등록(설계 67번 §4.3). Bootstrap 초기화 때 등록해 두므로 전환을 시작하는 순간
    /// 전체 작업 수가 정해진다 - 도중에 작업이 늘어 게이지가 뒤로 가는 일이 없다. 작업을 새로 추가할 때는 등록만 더하면 된다.
    /// </summary>
    public interface ISceneLoadingTaskRegistry
    {
        void Register(ContentSceneId scene, ISceneLoadingTask task);
    }

    /// <summary>
    /// 준비 작업을 가진 Bootstrap 상주 컴포넌트(UIManager 산하 패널 등). UIManager가 같은 GameObject에서 모아 등록한다 -
    /// 패널은 전역 DI 대상이 아니라 조율자를 직접 알지 못한다(CLAUDE.md 매니저 종속 하위 컴포넌트 규칙).
    /// </summary>
    public interface ISceneLoadingTaskSource
    {
        ContentSceneId LoadingScene { get; }
        IEnumerable<ISceneLoadingTask> GetLoadingTasks();
    }

    /// <summary>로딩바가 보는 진행 상태(ISP - 로딩바는 절차를 조작하지 않는다).</summary>
    public interface ISceneLoadingProgressReader
    {
        int CompletedCount { get; }
        int TotalCount { get; }
        string CurrentLabel { get; }
        event Action Changed;
    }

    /// <summary>
    /// 씬 전환 한 번의 로딩 절차(설계 67번 §4.5). SceneLoader가 시작·불러오기·정리 완료를 알리고, 연출 컨트롤러가 새 씬 UI 연결 뒤
    /// 남은 작업(연결 완료 + 준비 작업)을 실행시킨다.
    /// </summary>
    public interface ISceneLoadingSequence
    {
        /// <param name="hasPreviousScene">이전 콘텐츠 씬이 있으면(최초 진입이 아니면) "이전 화면 정리" 작업이 들어간다.</param>
        void Begin(string sceneName, bool hasPreviousScene);
        void CompleteCurrent();
        /// <summary>화면 연결을 완료로 치고 준비 작업을 차례로 실행한 뒤, 100%를 1프레임 보여 주고 onComplete.</summary>
        void RunRemaining(Action onComplete);
    }

    /// <summary>한 번에 끝나는 작업(Action). 예외는 로그로 남기고 작업은 끝난 것으로 친다 - 준비 작업 하나 때문에 로딩이 멈추면 안 된다.</summary>
    public sealed class ActionSceneLoadingTask : ISceneLoadingTask
    {
        private readonly Action action;

        public string Label { get; }

        public ActionSceneLoadingTask(string label, Action action)
        {
            Label = label;
            this.action = action;
        }

        public IEnumerator Run()
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
            yield break;
        }
    }
}
