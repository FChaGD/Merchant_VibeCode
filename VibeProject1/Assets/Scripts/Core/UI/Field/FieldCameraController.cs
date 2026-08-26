using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 이동 뷰 ↔ 전투 뷰 전환을 담당한다. Field 씬은 실제 카메라가 아니라 전부 Canvas UI로 구성돼
    /// 있어(Field.unity, Camera 고정) 두 RectTransform(모두 화면 전체를 채우는 stretch 앵커)의
    /// anchoredPosition.x를 함께 움직여 "카메라가 좌우로 이동하며 한쪽 뷰가 밀려나고 다른 쪽 뷰가
    /// 반대편에서 들어오는" 효과를 낸다(Docs/설계/04_Field씬_아키텍처.md §7 의도 재현). 양방향 모두
    /// 슬라이드 애니메이션을 사용한다(전투 뷰 복귀 시 즉시 전환한다는 §7 원안은 실플레이 피드백에
    /// 따라 폐기 - Docs/제작/02_인카운터_전투전환_구현.md 참고). MonoBehaviour가 아닌 순수 C# 합성
    /// 객체로, FieldUIController가 Field 씬 로드마다 새로 생성해 소유한다 - 두 RectTransform이 매번
    /// 새로 바인딩되는 이번 Field 씬의 오브젝트이기 때문이다.
    /// 전투 진입(toBattle) 시에는 전투 뷰와 같은 자리에서 같은 속도로 검은 커튼도 함께 슬라이드해
    /// 들어온다 - 커튼을 즉시 풀스크린으로 띄우면 슬라이드 애니메이션이 끊기고 화면이 깜빡이는 것처럼
    /// 보인다(연속 전투 시 이전 전투 잔여 유닛이 살짝 보이는 문제를 가리려다 발견됨). 커튼은 슬라이드가
    /// 끝난 뒤(화면을 완전히 덮은 채) 유지되고, 실제로 걷는 시점(전투 상태 재구성 완료 후)은
    /// FieldEncounterFlowCoordinator가 결정한다 - 여기서는 걷지 않는다.
    /// </summary>
    internal class FieldCameraController
    {
        private readonly MonoBehaviour coroutineRunner;
        private readonly RectTransform movementViewRoot;
        private readonly RectTransform battleViewRoot;
        private readonly FieldTransitionCurtainView transitionCurtain;
        private readonly RectTransform transitionCurtainRoot;

        public FieldCameraController(MonoBehaviour coroutineRunner, RectTransform movementViewRoot, RectTransform battleViewRoot, FieldTransitionCurtainView transitionCurtain)
        {
            this.coroutineRunner = coroutineRunner;
            this.movementViewRoot = movementViewRoot;
            this.battleViewRoot = battleViewRoot;
            this.transitionCurtain = transitionCurtain;
            transitionCurtainRoot = (RectTransform)transitionCurtain.transform;
        }

        public void TransitionToBattle(Action onComplete)
        {
            SlideRoutine(toBattle: true, onComplete);
        }

        public void TransitionToMovement(Action onComplete)
        {
            SlideRoutine(toBattle: false, onComplete);
        }

        private float Width()
        {
            return movementViewRoot.rect.width;
        }

        // toBattle=true: 이동 뷰가 왼쪽으로 밀려나고 전투 뷰가 오른쪽에서 들어온다(카메라가 오른쪽으로 이동).
        // toBattle=false: 반대 방향으로 재생 - 전투 뷰가 오른쪽으로 밀려나고 이동 뷰가 왼쪽에서 들어온다
        // (카메라가 왼쪽으로 이동). 타이밍(지속시간/이징 곡선) 자체는 SlideTransitionTimeline으로
        // 공용화했다 - Hub↔Field 씬 전환 연출(SceneTransitionEffectController)도 같은 타이밍을 쓴다
        // (Docs/설계/10_씬전환_연출_아키텍처.md §3).
        private void SlideRoutine(bool toBattle, Action onComplete)
        {
            var width = Width();
            var enteringView = toBattle ? battleViewRoot : movementViewRoot;
            var exitingView = toBattle ? movementViewRoot : battleViewRoot;
            var enterStartX = toBattle ? width : -width;
            var exitEndX = toBattle ? -width : width;

            enteringView.gameObject.SetActive(true);
            exitingView.anchoredPosition = Vector2.zero;
            enteringView.anchoredPosition = new Vector2(enterStartX, 0f);

            // 전투 뷰와 같은 시작 위치·같은 속도로 커튼도 함께 들어오게 한다 - 별도 타이밍으로
            // 움직이면 두 뷰 사이로 이전 전투의 잔여 유닛이 잠깐씩 비칠 수 있다.
            if (toBattle)
            {
                transitionCurtain.Show();
                transitionCurtainRoot.anchoredPosition = new Vector2(enterStartX, 0f);
            }

            SlideTransitionTimeline.Run(coroutineRunner, SlideTransitionTimeline.DefaultDurationSeconds,
                onStep: t =>
                {
                    exitingView.anchoredPosition = new Vector2(exitEndX * t, 0f);
                    enteringView.anchoredPosition = new Vector2(enterStartX * (1f - t), 0f);
                    if (toBattle)
                    {
                        transitionCurtainRoot.anchoredPosition = new Vector2(enterStartX * (1f - t), 0f);
                    }
                },
                onComplete: () =>
                {
                    exitingView.gameObject.SetActive(false);

                    // 커튼은 여기서 걷지 않는다 - 화면을 완전히 덮은 채로 유지되다가, 전투 상태가
                    // 실제로 재구성된 뒤(onComplete 안에서 StartBattle() 호출 후) FieldEncounterFlowCoordinator가 걷는다.
                    if (toBattle)
                    {
                        transitionCurtainRoot.anchoredPosition = Vector2.zero;
                    }

                    onComplete?.Invoke();
                });
        }
    }
}
