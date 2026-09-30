using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 포인터가 올라와 있는 동안 일정 간격으로 Fired를 반복 발생시킨다(Docs/기획/63번 §3.4 - 아이템을 끄는 중 스테퍼 위에 0.5초 머물면
    /// 마차 전환, 계속 머물면 반복). 시작 조건(CanRepeat - 예: 드래그 중인지)은 소비자가 정한다. 포인터 진입 이벤트가 시작점이고
    /// 대기는 코루틴이 맡는다 - Update 폴링을 두지 않는다. 드래그 중에도 EventSystem은 진입/이탈 이벤트를 보낸다.
    /// 대기는 실시간 기준이다 - 일시정지(Time.timeScale = 0) 중에도 UI 조작은 동작해야 한다.
    /// </summary>
    public class HoverRepeatTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private float interval = 0.5f;

        private Coroutine routine;

        public Func<bool> CanRepeat { get; set; }
        public event Action Fired;

        public void OnPointerEnter(PointerEventData eventData)
        {
            Stop();
            if (CanRepeat == null || !CanRepeat()) return;
            routine = StartCoroutine(Repeat());
        }

        public void OnPointerExit(PointerEventData eventData) => Stop();

        private void OnDisable() => Stop();

        private IEnumerator Repeat()
        {
            var wait = new WaitForSecondsRealtime(interval);
            while (true)
            {
                yield return wait;
                if (CanRepeat == null || !CanRepeat()) break;
                Fired?.Invoke();
            }
            routine = null;
        }

        private void Stop()
        {
            if (routine == null) return;
            StopCoroutine(routine);
            routine = null;
        }
    }
}
