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
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // 전투 결과는 상행 종료 판정에 필수인 의존성이므로 Resolve(필수)로 조회한다.
            // BattleManager가 관리 목록에서 빠지면 여기서 즉시 예외가 발생해 결선 누락을 조기에 드러낸다.
            battleResultSource = registrar.Resolve<IBattleResultSource>();
            battleResultSource.OnBattleEnded += HandleBattleEnded;

            // SceneLoader는 전역 DI 대상이 아니라 GameManager 산하 컴포넌트 — 같은 GameObject에서 직접 조회한다.
            sceneLoader = GetComponent<ISceneLoader>();
            if (sceneLoader == null)
            {
                throw new InvalidOperationException($"{nameof(GameManager)}와 같은 GameObject에 {nameof(ISceneLoader)} 구현체가 없다.");
            }
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
