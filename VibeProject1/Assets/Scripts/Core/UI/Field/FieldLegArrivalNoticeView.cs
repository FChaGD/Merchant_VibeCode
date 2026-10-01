using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 중간 도시에 도착했을 때 잠깐 보여 주는 "구간 도착" 알림(Docs/설계/76번 §7.2) - 확인 버튼 없이 정해진 시간 뒤 사라지고 콜백을 부른다
    /// (§10-6 - 자동으로 다음 구간 출발). 오브젝트를 켜고 끄지 않고 CanvasGroup 투명도로만 숨긴다 - 요소 조회(SceneUIRoot)가 씬 로드 때
    /// 한 번만 수집하므로 항상 활성 상태로 둔다.
    /// </summary>
    public class FieldLegArrivalNoticeView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text messageText;

        private Coroutine running;

        private void Awake() => SetVisible(false);

        public void Show(string message, float seconds, Action onHidden)
        {
            if (running != null) StopCoroutine(running);
            if (messageText != null) messageText.text = message;
            running = StartCoroutine(ShowRoutine(seconds, onHidden));
        }

        private IEnumerator ShowRoutine(float seconds, Action onHidden)
        {
            SetVisible(true);
            yield return new WaitForSeconds(seconds);
            SetVisible(false);
            running = null;
            onHidden?.Invoke();
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}
