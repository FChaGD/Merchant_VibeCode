using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// UIManager 산하 컴포넌트. Hub 씬이 로드될 때 필요한 UI 배선만 담당한다 - UIManager가 Hub 전용
    /// 패널/데이터 의존성을 직접 알지 않도록 분리했다(SRP, Docs/Refactor/2026-08-26-공통.md 3단계 수정안).
    /// </summary>
    public class HubUIWiring : MonoBehaviour, IContentSceneUIWiring
    {
        public ContentSceneId SceneId => ContentSceneId.Hub;

        public void Wire(IDependencyRegistrar registrar, IUIManager uiManager, IPanelRegistrar panelRegistrar)
        {
            var hubUIController = GetComponent<IHubUIController>();
            if (hubUIController == null)
            {
                throw new InvalidOperationException($"{nameof(HubUIWiring)}와 같은 GameObject에 {nameof(IHubUIController)} 구현체가 없다.");
            }

            var formationPanel = GetComponent<IFormationPanel>();
            if (formationPanel == null)
            {
                throw new InvalidOperationException($"{nameof(HubUIWiring)}와 같은 GameObject에 {nameof(IFormationPanel)} 구현체가 없다.");
            }

            var tripPanel = GetComponent<ITripPanel>();
            if (tripPanel == null)
            {
                throw new InvalidOperationException($"{nameof(HubUIWiring)}와 같은 GameObject에 {nameof(ITripPanel)} 구현체가 없다.");
            }

            var tacticsPanel = GetComponent<ITacticsPanel>();
            if (tacticsPanel == null)
            {
                throw new InvalidOperationException($"{nameof(HubUIWiring)}와 같은 GameObject에 {nameof(ITacticsPanel)} 구현체가 없다.");
            }

            var gameManager = registrar.Resolve<IGameManager>();
            // "상행 시작"/"상행 준비"/"배치" 버튼을 씬 전환 커튼이 완전히 걷힐 때까지 비활성화하는 데
            // 쓴다(사용자 확정) - HubUIController/TripPanel 둘 다 필요하므로 여기서 한 번만 조회한다.
            var sceneRevealSignal = registrar.Resolve<ISceneRevealSignal>();

            // 상행 관리 데이터 시스템이 아직 없어 선택적으로 조회한다 - 등록되면 자동으로 연결된다.
            registrar.TryResolve<ICaravanRosterProvider>(out var caravanRosterProvider);
            registrar.TryResolve<IFormationRepository>(out var formationRepository);
            registrar.TryResolve<ITripInfoProvider>(out var tripInfoProvider);
            registrar.TryResolve<ITacticsRepository>(out var tacticsRepository);

            hubUIController.RegisterHubUI(uiManager, sceneRevealSignal);

            formationPanel.RegisterFormationUI(caravanRosterProvider, formationRepository, uiManager, SceneNames.Hub);
            panelRegistrar.RegisterPanel(formationPanel);

            tripPanel.RegisterTripUI(uiManager, gameManager, formationRepository, tripInfoProvider, sceneRevealSignal);
            panelRegistrar.RegisterPanel(tripPanel);

            tacticsPanel.RegisterTacticsUI(tacticsRepository, uiManager, SceneNames.Hub);
            panelRegistrar.RegisterPanel(tacticsPanel);

            // Hub↔Field 씬 전환 연출(SceneTransitionEffectController)이 다음 전환 때 슬라이드시킬
            // 대상을 등록한다 - 씬을 다시 로드할 때마다 최신 참조로 갱신된다(Docs/설계/10-2026-08-26-씬전환_연출_아키텍처.md §8).
            // 반드시 맨 마지막에 둔다 - 여기서 예외가 나도(예: 설치 도구 미실행) 위 핵심 패널 등록은
            // 이미 끝난 뒤라 Hub UI 자체는 정상 동작한다.
            registrar.Resolve<ISceneTransitionContentRootRegistry>().RegisterContentRoot(ContentSceneId.Hub, hubUIController.ContentRoot);
        }
    }
}
