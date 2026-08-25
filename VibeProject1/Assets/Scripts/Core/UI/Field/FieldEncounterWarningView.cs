using System.Collections;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 인카운터 발생 시 화면 중앙에 점멸하는 경고창. CanvasGroup.alpha를 사인파로 0~1 사이에서
    /// 부드럽게 보간해 점멸시킨다(즉시 on/off 토글 방식이 아니다).
    /// </summary>
    public class FieldEncounterWarningView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float blinkPeriodSeconds = 0.8f;

        private Coroutine blinkRoutine;

        public void Show()
        {
            gameObject.SetActive(true);
            if (blinkRoutine == null)
            {
                blinkRoutine = StartCoroutine(BlinkLoop());
            }
        }

        public void Hide()
        {
            if (blinkRoutine != null)
            {
                StopCoroutine(blinkRoutine);
                blinkRoutine = null;
            }

            gameObject.SetActive(false);
        }

        private IEnumerator BlinkLoop()
        {
            var elapsed = 0f;
            while (true)
            {
                elapsed += Time.deltaTime;
                // 사인파를 0~1 범위로 정규화 - 0(암전)과 1(완전 표시) 사이를 매끄럽게 오간다.
                canvasGroup.alpha = (Mathf.Sin(elapsed * Mathf.PI * 2f / blinkPeriodSeconds) + 1f) * 0.5f;
                yield return null;
            }
        }
    }
}
