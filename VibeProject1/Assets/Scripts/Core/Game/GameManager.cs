using System;
using UnityEngine;

namespace Game.Core
{
    public class GameManager : MonoBehaviour, IGameManager, IManagedComponent
    {
        private ISceneTransitionEffectPlayer transitionEffectPlayer;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IGameManager>(this);

            // SessionStateTracker는 GameManager 산하 컴포넌트라 전역 DI 대상이 아니다(§5.1 규칙) - 대신
            // GameManager가 같은 GameObject에서 조회해 ISessionState/ISessionPauseControl 두 인터페이스로
            // 등록한다. 소비자가 필요한 만큼만 의존하도록 나눈 것이다(Docs/설계/04-2026-08-25-Field씬_아키텍처.md 3절).
            var sessionState = GetComponent<SessionStateTracker>();
            if (sessionState == null)
            {
                throw new InvalidOperationException($"{nameof(GameManager)}와 같은 GameObject에 {nameof(SessionStateTracker)}가 없다.");
            }
            registrar.Register<ISessionState>(sessionState);
            registrar.Register<ISessionPauseControl>(sessionState);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // 씬 전환 실행·연출은 SceneTransitionEffectController의 책임 — GameManager는 요청을
            // 그대로 전달만 한다(Docs/설계/10-2026-08-26-씬전환_연출_아키텍처.md §7).
            transitionEffectPlayer = registrar.Resolve<ISceneTransitionEffectPlayer>();
        }

        public void RequestSceneTransition(ContentSceneId sceneId)
        {
            transitionEffectPlayer.PlayTransition(sceneId);
        }
    }
}
