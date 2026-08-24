using System;
using UnityEngine;

namespace Game.Core
{
    public class GameManager : MonoBehaviour, IGameManager, IManagedComponent
    {
        private IBattleResultSource battleResultSource;
        private ISceneLoader sceneLoader;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IGameManager>(this);

            // SessionStateTracker는 GameManager 산하 컴포넌트라 전역 DI 대상이 아니다(§5.1 규칙) - 대신
            // GameManager가 같은 GameObject에서 조회해 ISessionState/ISessionPauseControl 두 인터페이스로
            // 등록한다. 소비자가 필요한 만큼만 의존하도록 나눈 것이다(Docs/설계/04_Field씬_아키텍처.md 3절).
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
            // 전투 결과는 상행 종료 판정에 필수인 의존성이므로 Resolve(필수)로 조회한다.
            // BattleManager가 관리 목록에서 빠지면 여기서 즉시 예외가 발생해 결선 누락을 조기에 드러낸다.
            battleResultSource = registrar.Resolve<IBattleResultSource>();
            battleResultSource.OnBattleEnded += HandleBattleEnded;

            // 씬 전환 실행은 SceneLoader의 책임 — GameManager는 요청을 그대로 전달만 한다.
            sceneLoader = registrar.Resolve<ISceneLoader>();
        }

        public void RequestSceneTransition(string sceneName)
        {
            sceneLoader.Transition(sceneName);
        }

        private void HandleBattleEnded(BattleResult result)
        {
            // TODO: 전투 결과에 따른 상행 종료 판정(궤주/포로/사망/도주) - 후속 기획 확정 후 구현
        }

        private void OnDestroy()
        {
            if (battleResultSource != null)
            {
                battleResultSource.OnBattleEnded -= HandleBattleEnded;
            }
        }
    }
}
