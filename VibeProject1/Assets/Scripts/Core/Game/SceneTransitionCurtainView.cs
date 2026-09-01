using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Hub↔Field 씬 전환을 가리는 검은 커튼. Field 내부 전환용 FieldTransitionCurtainView와 달리
    /// Bootstrap(영속) 스코프에 둔다 - 콘텐츠 씬이 언로드되는 순간까지 화면을 덮고 있어야 하는데,
    /// 콘텐츠 씬 스코프 오브젝트는 그 씬이 언로드되면 함께 파괴되기 때문이다. CanvasGroup 기반
    /// 페이드 아웃도 지원한다(Field 쪽은 즉시 SetActive(false)로 걷지만, 이번 전환은 로딩 완료를
    /// 자연스럽게 알리기 위해 페이드로 걷는다 - Docs/설계/10-2026-08-26-씬전환_연출_아키텍처.md §5). 페이드 곡선은
    /// 카메라 슬라이드와 같은 EaseInCubic을 SlideTransitionTimeline으로 그대로 재사용한다(사용자 확정).
    /// </summary>
    public class SceneTransitionCurtainView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        private RectTransform rectTransform;

        public RectTransform RectTransform => rectTransform;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
        }

        public void SetAnchoredPosition(Vector2 position)
        {
            rectTransform.anchoredPosition = position;
        }

        public void FadeOut(MonoBehaviour coroutineRunner, float duration, Action onComplete)
        {
            SlideTransitionTimeline.Run(coroutineRunner, duration,
                onStep: t => canvasGroup.alpha = 1f - t,
                onComplete: () =>
                {
                    canvasGroup.alpha = 0f;
                    gameObject.SetActive(false);
                    onComplete?.Invoke();
                });
        }
    }
}
