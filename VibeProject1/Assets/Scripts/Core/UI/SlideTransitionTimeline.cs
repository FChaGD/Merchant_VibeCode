using System;
using System.Collections;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 지속시간/이징 곡선을 따라 매 프레임 진행률(0~1)을 흘려보내는 부분만 공용화한 유틸리티.
    /// "무엇을 움직이는가"는 onStep 콜백이 결정하므로, FieldCameraController(나가는 뷰+들어오는 뷰+커튼
    /// 3개)와 SceneTransitionEffectController(나가는 콘텐츠+커튼 2개)처럼 움직이는 대상 개수가 달라도
    /// 같은 타이밍 로직을 그대로 재사용할 수 있다(Docs/설계/10-2026-08-26-씬전환_연출_아키텍처.md §3).
    /// </summary>
    internal static class SlideTransitionTimeline
    {
        // 0.6초 2차 곡선(EaseInQuad)이 전체적으로 빠르다는 피드백에 따라, 전체 길이를 늘리고 3차 곡선
        // (EaseInCubic)으로 바꿨다 - 종료 시점 속도(3×width/0.9)가 이전 2차 곡선의 종료 속도
        // (2×width/0.6)와 거의 같도록 맞춰, "후반부는 지금과 비슷한 속도" 요구를 만족시키면서 초반부는
        // 3차 곡선의 더 완만한 시작 구간 덕에 훨씬 느려진다(원래 FieldCameraController에서 확정된 값).
        // 다른 용도(커튼 페이드 아웃 등)는 곡선(EaseInCubic)은 그대로 재사용하되 지속시간은 다를 수
        // 있어 Run의 매개변수로 받는다 - 이 상수는 슬라이드 전용 기본값이다.
        public const float DefaultDurationSeconds = 0.9f;

        public static float EaseInCubic(float t) => t * t * t;

        public static void Run(MonoBehaviour runner, float durationSeconds, Action<float> onStep, Action onComplete)
        {
            runner.StartCoroutine(RunRoutine(durationSeconds, onStep, onComplete));
        }

        private static IEnumerator RunRoutine(float durationSeconds, Action<float> onStep, Action onComplete)
        {
            var elapsed = 0f;
            while (elapsed < durationSeconds)
            {
                elapsed += Time.deltaTime;
                onStep(EaseInCubic(Mathf.Clamp01(elapsed / durationSeconds)));
                yield return null;
            }

            onStep(1f);
            onComplete?.Invoke();
        }
    }
}
