using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 이동 뷰 → 전투 뷰 전환 중 전장을 가리는 검은 화면. MovementView/BattleView와 달리 슬라이드하지
    /// 않고 화면 전체를 고정으로 덮는다 - 두 뷰가 어느 위치까지 슬라이드했든 항상 뷰포트 전체를 가려야
    /// 하기 때문이다. 전투 상태(BuildSimulation+뷰 재생성)가 실제로 갱신된 뒤에야 걷혀야, 이전 전투의
    /// 잔여 유닛이 슬라이드 중 잠깐이라도 보이거나 초기화되는 순간이 카메라에 잡히지 않는다
    /// (FieldEncounterFlowCoordinator.TransitionAfterWarning 참고). 걷는 방식은 사용자 확정으로
    /// Hub↔Field 전환용 SceneTransitionCurtainView와 동일하게 통일했다 - 즉시 SetActive(false)가
    /// 아니라 같은 EaseInCubic 곡선(SlideTransitionTimeline 재사용)으로 페이드 아웃한다.
    /// </summary>
    public class FieldTransitionCurtainView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        public void Show()
        {
            gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
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
