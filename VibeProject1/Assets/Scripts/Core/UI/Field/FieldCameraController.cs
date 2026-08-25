using System;
using System.Collections;
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
    /// </summary>
    internal class FieldCameraController
    {
        private const float SlideDurationSeconds = 0.6f;

        private readonly MonoBehaviour coroutineRunner;
        private readonly RectTransform movementViewRoot;
        private readonly RectTransform battleViewRoot;

        public FieldCameraController(MonoBehaviour coroutineRunner, RectTransform movementViewRoot, RectTransform battleViewRoot)
        {
            this.coroutineRunner = coroutineRunner;
            this.movementViewRoot = movementViewRoot;
            this.battleViewRoot = battleViewRoot;
        }

        public void TransitionToBattle(Action onComplete)
        {
            coroutineRunner.StartCoroutine(SlideRoutine(toBattle: true, onComplete));
        }

        public void TransitionToMovement(Action onComplete)
        {
            coroutineRunner.StartCoroutine(SlideRoutine(toBattle: false, onComplete));
        }

        private float Width()
        {
            return movementViewRoot.rect.width;
        }

        // toBattle=true: 이동 뷰가 왼쪽으로 밀려나고 전투 뷰가 오른쪽에서 들어온다(카메라가 오른쪽으로 이동).
        // toBattle=false: 반대 방향으로 재생 - 전투 뷰가 오른쪽으로 밀려나고 이동 뷰가 왼쪽에서 들어온다
        // (카메라가 왼쪽으로 이동).
        private IEnumerator SlideRoutine(bool toBattle, Action onComplete)
        {
            var width = Width();
            var enteringView = toBattle ? battleViewRoot : movementViewRoot;
            var exitingView = toBattle ? movementViewRoot : battleViewRoot;
            var enterStartX = toBattle ? width : -width;
            var exitEndX = toBattle ? -width : width;

            enteringView.gameObject.SetActive(true);
            exitingView.anchoredPosition = Vector2.zero;
            enteringView.anchoredPosition = new Vector2(enterStartX, 0f);

            var elapsed = 0f;
            while (elapsed < SlideDurationSeconds)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / SlideDurationSeconds);
                exitingView.anchoredPosition = new Vector2(exitEndX * t, 0f);
                enteringView.anchoredPosition = new Vector2(enterStartX * (1f - t), 0f);
                yield return null;
            }

            exitingView.anchoredPosition = new Vector2(exitEndX, 0f);
            enteringView.anchoredPosition = Vector2.zero;
            exitingView.gameObject.SetActive(false);

            onComplete?.Invoke();
        }
    }
}
