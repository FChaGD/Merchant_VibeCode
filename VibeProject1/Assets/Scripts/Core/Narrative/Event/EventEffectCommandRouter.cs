using System;

namespace Game.Core
{
    /// <summary>
    /// 효과 명령의 현재 맥락 바꾸기(설계 109 §5.1). 이벤트 진행자가 시작할 때 자기와 맥락 서비스 묶음을 넣고, 끝(정상·중단)에 뺀다.
    /// </summary>
    public interface IEventEffectRouter
    {
        void Enter(IEventEffectHost host, EventEffectServices services);
        void Exit(IEventEffectHost host);
    }

    /// <summary>
    /// 효과 명령 중계(설계 109 §5.1). Yarn 명령은 실행기 하나에 한 번만 등록할 수 있는데 진행자는 상행·휴식 둘이다 - 명령은 이 라우터를 호스트로 삼고
    /// 지금 들어와 있는 진행자·서비스로 넘긴다. 맥락이 없으면 실행 중이 아닌 것으로 보여 모든 명령이 효과 없이 지나간다(끝난 이벤트의 남은 대본 Drain과 같다).
    /// 상행과 휴식은 동시에 일어나지 않으므로 겹친 Enter는 경고 후 덮어쓴다(방어).
    /// </summary>
    public sealed class EventEffectCommandRouter : IEventEffectRouter, IEventEffectHost
    {
        private readonly Action<string> warn;
        private IEventEffectHost host;
        private EventEffectServices services;

        public EventEffectCommandRouter(Action<string> warn = null) => this.warn = warn ?? (_ => { });

        public void Register(IDialogueCommandRegistry registry) => new EventEffectCommands(() => services, this).Register(registry);

        public void Enter(IEventEffectHost newHost, EventEffectServices newServices)
        {
            if (host != null && host != newHost) warn($"{nameof(EventEffectCommandRouter)}: 다른 이벤트 맥락이 끝나지 않은 채 새 맥락이 들어왔다 - 덮어쓴다.");
            host = newHost;
            services = newServices;
        }

        public void Exit(IEventEffectHost leaving)
        {
            if (host != leaving) return;   // 덮어써진 옛 진행자의 늦은 Exit - 지금 맥락을 지우지 않는다
            host = null;
            services = null;
        }

        // IEventEffectHost - 현재 맥락 진행자로 넘긴다.
        public bool IsRunning => host != null && host.IsRunning;

        public void PresentResult(string text, Action onDone)
        {
            if (host != null) host.PresentResult(text, onDone);
            else onDone?.Invoke();
        }

        public bool TryShowCargoRecovery(Action onDone) => host != null && host.TryShowCargoRecovery(onDone);

        public void RequestBattle(Action onVictory) => host?.RequestBattle(onVictory);
    }
}
