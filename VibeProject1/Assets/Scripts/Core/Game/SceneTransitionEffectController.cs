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
    /// (Docs/설계/10_씬전환_연출_아키텍처.md §4/§12).
    /// </summary>
    public class SceneTransitionEffectController : MonoBehaviour,
        ISceneTransitionEffectPlayer, ISceneTransitionContentRootRegistry, ISceneRevealSignal, IManagedComponent
    {
        [SerializeField] private SceneTransitionCurtainView curtain;

        // 상행 시작/전투 시작(사실상 세션 진행 시작)/배치·상행 준비 버튼 등은 화면이 완전히 드러난
        // 뒤에만 상호작용 가능해야 한다(사용자 확정) - Hub/Field UI 컨트롤러가 이 이벤트를 구독해
        // 자기 버튼의 interactable을 직접 제어한다.
        public event Action<ContentSceneId> SceneRevealed;

        private const float FadeOutDurationSeconds = 2f; // 사용자 확정값 - 슬라이드와 같은 EaseInCubic 곡선 사용

        private ISceneLoader sceneLoader;
        private readonly Dictionary<ContentSceneId, RectTransform> contentRootsBySceneId = new();
        private bool isTransitioning;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ISceneTransitionEffectPlayer>(this);
            registrar.Register<ISceneTransitionContentRootRegistry>(this);
            registrar.Register<ISceneRevealSignal>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            sceneLoader = registrar.Resolve<ISceneLoader>();

            // UIManager가 먼저 OnSceneLoaded를 처리해 새 씬의 배선(Wire)을 끝내야, 그 다음에 이
            // 컨트롤러가 커튼을 걷어도 이미 완성된 화면이 드러난다 - ManagerHierarchyInstaller가
            // managedComponents 목록에서 이 컴포넌트를 uiManager 뒤에 둬 구독 순서를 보장한다.
            sceneLoader.OnSceneLoaded += HandleSceneLoaded;
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
                    sceneLoader.Transition(targetSceneId.ToString());
                });
        }

        private void HandleSceneLoaded(string sceneName)
        {
            if (!Enum.TryParse<ContentSceneId>(sceneName, out var sceneId))
            {
                return;
            }

            if (!isTransitioning)
            {
                // 최초 진입/디버그 재진입 등 이 컨트롤러가 시작하지 않은 로드 - 커튼이 뜬 적이 없으므로
                // "드러남" 신호를 즉시 보낸다(기다릴 페이드가 없음).
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
