using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Hub↔Field 씬 전환 연출(슬라이드 아웃+커튼 동행 → 실제 전환 → 커튼 페이드 아웃)을 전담한다.
    /// "언제 씬을 로드/언로드할지"는 SceneLoader의 책임으로 남기고, 이 클래스는 "그 앞뒤로 연출을
    /// 어떻게 보여줄지"만 담당한다(SRP - UIManager/PanelNavigationStack 분리와 동일한 판단).
    ///
    /// 콘텐츠 씬이 Hub/Field 2개뿐이라는 현재 범위를 그대로 이용해 "현재 씬 = 대상 씬의 반대"로
    /// 추론한다(별도로 현재 씬을 추적하지 않음) - 씬이 3개 이상으로 늘어나면 이 추론은 깨지므로
    /// 그때는 ISceneLoader가 현재 씬 id를 명시적으로 노출하도록 다시 설계해야 한다
    /// (Docs/설계/10-2026-08-26-씬전환_연출_아키텍처.md §4/§12).
    ///
    /// 모든 전환(최초 진입 포함)은 같은 로딩 절차를 탄다(Docs/설계/67번): 검은 커튼 + 로딩바 → 불러오기·정리·연결·준비 작업
    /// (SceneLoadingCoordinator가 진행) → 100% → 로딩바 숨김 → 커튼 2초 페이드 → 드러남 신호. 최초 진입은 첫 화면이 그려지기 전
    /// (의존성 해결 시점)에 커튼을 띄운다 - 예전에는 커튼 없이 로드된 마을을 바로 보여 줬다.
    /// </summary>
    public class SceneTransitionEffectController : MonoBehaviour,
        ISceneTransitionEffectPlayer, ISceneTransitionContentRootRegistry, ISceneRevealSignal, IManagedComponent
    {
        [SerializeField] private SceneTransitionCurtainView curtain;
        [SerializeField] private SceneLoadingBarView loadingBar;

        // 상행 시작/전투 시작(사실상 세션 진행 시작)/배치·상행 준비 버튼 등은 화면이 완전히 드러난
        // 뒤에만 상호작용 가능해야 한다(사용자 확정) - Hub/Field UI 컨트롤러가 이 이벤트를 구독해
        // 자기 버튼의 interactable을 직접 제어한다.
        public event Action<ContentSceneId> SceneRevealed;

        private const float FadeOutDurationSeconds = 2f; // 사용자 확정값 - 슬라이드와 같은 EaseInCubic 곡선 사용

        private ISceneLoader sceneLoader;
        private ISceneLoadingSequence loadingSequence;
        private readonly Dictionary<ContentSceneId, RectTransform> contentRootsBySceneId = new();
        private bool isTransitioning;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ISceneTransitionEffectPlayer>(this);
            registrar.Register<ISceneTransitionContentRootRegistry>(this);
            registrar.Register<ISceneRevealSignal>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            sceneLoader = registrar.Resolve<ISceneLoader>();

            // UIManager가 먼저 OnSceneLoaded를 처리해 새 씬의 배선(Wire)을 끝내야, 그 다음에 이
            // 컨트롤러가 커튼을 걷어도 이미 완성된 화면이 드러난다 - ManagerHierarchyInstaller가
            // managedComponents 목록에서 이 컴포넌트를 uiManager 뒤에 둬 구독 순서를 보장한다.
            sceneLoader.OnSceneLoaded += HandleSceneLoaded;

            registrar.TryResolve(out loadingSequence);
            if (loadingBar != null && registrar.TryResolve<ISceneLoadingProgressReader>(out var progress)) loadingBar.Bind(progress);

            // SceneLoader는 목록 맨 뒤라 이보다 늦게 해결되며 최초 전환을 시작한다 - 그 전에 커튼을 덮어 둔다.
            ShowCurtainForInitialLoad();
        }

        private void ShowCurtainForInitialLoad()
        {
            if (curtain == null) return;

            isTransitioning = true;
            curtain.Show();
            curtain.SetAnchoredPosition(Vector2.zero);
            if (loadingBar != null) loadingBar.Show();
        }

        public void RegisterContentRoot(ContentSceneId sceneId, RectTransform contentRoot)
        {
            contentRootsBySceneId[sceneId] = contentRoot;
        }

        public void PlayTransition(ContentSceneId targetSceneId)
        {
            if (isTransitioning)
            {
                return; // 연출 도중 중복 트리거 방지
            }

            var currentSceneId = InferCurrentScene(targetSceneId);
            if (!contentRootsBySceneId.TryGetValue(currentSceneId, out var contentRoot) || contentRoot == null)
            {
                Debug.LogWarning($"'{currentSceneId}'의 콘텐츠 루트가 등록되어 있지 않아 연출 없이 즉시 전환한다.");
                sceneLoader.Transition(targetSceneId.ToString());
                return;
            }

            isTransitioning = true;

            var exitsToLeft = ExitsToLeft(targetSceneId);
            var width = contentRoot.rect.width;
            var exitEndX = exitsToLeft ? -width : width;
            var curtainStartX = -exitEndX; // 반대편에서 콘텐츠와 동행 - FieldCameraController와 동일한 관계

            curtain.Show();
            curtain.SetAnchoredPosition(new Vector2(curtainStartX, 0f));

            SlideTransitionTimeline.Run(this, SlideTransitionTimeline.DefaultDurationSeconds,
                onStep: t =>
                {
                    contentRoot.anchoredPosition = new Vector2(exitEndX * t, 0f);
                    curtain.SetAnchoredPosition(new Vector2(curtainStartX * (1f - t), 0f));
                },
                onComplete: () =>
                {
                    contentRoot.gameObject.SetActive(false);
                    if (loadingBar != null) loadingBar.Show();
                    sceneLoader.Transition(targetSceneId.ToString());
                });
        }

        private void HandleSceneLoaded(string sceneName)
        {
            if (!Enum.TryParse<ContentSceneId>(sceneName, out var sceneId))
            {
                return;
            }

            // 화면 연결(UIManager)은 이미 끝났다 - 남은 준비 작업까지 마친 뒤 걷는다.
            if (loadingSequence == null)
            {
                Reveal(sceneId);
                return;
            }
            loadingSequence.RunRemaining(() => Reveal(sceneId));
        }

        private void Reveal(ContentSceneId sceneId)
        {
            if (loadingBar != null) loadingBar.Hide();

            if (!isTransitioning)
            {
                // 커튼 없이 시작된 로드(콘텐츠 루트 미등록 등으로 연출 없이 전환) - 기다릴 페이드가 없다.
                SceneRevealed?.Invoke(sceneId);
                return;
            }

            isTransitioning = false;
            curtain.FadeOut(this, FadeOutDurationSeconds, onComplete: () => SceneRevealed?.Invoke(sceneId));
        }

        private static ContentSceneId InferCurrentScene(ContentSceneId target)
            => target == ContentSceneId.Field ? ContentSceneId.Hub : ContentSceneId.Field;

        private static bool ExitsToLeft(ContentSceneId target) => target == ContentSceneId.Field; // Hub→Field

        private void OnDestroy()
        {
            if (sceneLoader != null)
            {
                sceneLoader.OnSceneLoaded -= HandleSceneLoaded;
            }
        }
    }
}
