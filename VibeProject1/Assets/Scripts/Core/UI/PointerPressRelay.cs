using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 짧은 클릭과 길게 누름을 구분해 이벤트로 중계하는 범용 컴포넌트(PointerHoverRelay와 같은 성격, 특정 UI 로직을 모름).
    /// Button.onClick은 누른 시간을 구분하지 못해 따로 둔다(기획 82번 D3). 시간 측정은 코루틴으로 한다 - Update 폴링 금지 컨벤션.
    /// 누른 채 영역 밖으로 나가면 클릭을 취소한다. 길게 누름이 시작된 뒤에는 손을 뗄 때까지 유지한다(EventSystem은 밖에서 떼도
    /// 누른 오브젝트에 PointerUp을 보낸다).
    /// </summary>
    public class PointerPressRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float longPressSeconds = 0.3f;

        public event Action ShortClicked;
        public event Action LongPressStarted;
        public event Action LongPressEnded;

        private Coroutine timer;
        private bool pressing;
        private bool longPressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            pressing = true;
            longPressed = false;
            StopTimer();
            timer = StartCoroutine(WaitForLongPress());
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !pressing) return;

            pressing = false;
            StopTimer();
            if (longPressed)
            {
                longPressed = false;
                LongPressEnded?.Invoke();
                return;
            }
            ShortClicked?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!pressing || longPressed) return;

            pressing = false;
            StopTimer();
        }

        private void OnDisable()
        {
            StopTimer();
            pressing = false;
            if (!longPressed) return;
            longPressed = false;
            LongPressEnded?.Invoke();
        }

        private IEnumerator WaitForLongPress()
        {
            yield return new WaitForSecondsRealtime(longPressSeconds);
            timer = null;
            if (!pressing) yield break;

            longPressed = true;
            LongPressStarted?.Invoke();
        }

        private void StopTimer()
        {
            if (timer == null) return;
            StopCoroutine(timer);
            timer = null;
        }
    }
}
