using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 승리/패배/도착 세 상황 모두 "메시지 + 버튼 1개" 구조가 동일해 하나의 뷰로 재사용한다
    /// (Docs/설계/04-2026-08-25-Field씬_아키텍처.md §5.3). 문구·버튼 라벨·확인 콜백만 상황마다 다르다.
    /// 전투 결과는 제목과 정산 요약의 서식을 나누려고 상세 라벨을 따로 둔다(설계 79번 §6.1) - 상세가 비면 숨긴다.
    /// </summary>
    public class FieldResultPopupView : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageLabel;
        // 인스톨러 재실행 전(옛 씬)에는 비어 있을 수 있다 - 그때는 상세 없이 기존처럼 동작한다.
        [SerializeField] private TMP_Text detailLabel;
        [SerializeField] private TMP_Text buttonLabel;
        [SerializeField] private Button confirmButton;

        public void Show(string message, string buttonText, Action onConfirm) => Show(message, null, buttonText, onConfirm);

        public void Show(string message, string detail, string buttonText, Action onConfirm)
        {
            messageLabel.text = message;
            buttonLabel.text = buttonText;

            if (detailLabel != null)
            {
                var hasDetail = !string.IsNullOrEmpty(detail);
                detailLabel.text = hasDetail ? detail : string.Empty;
                detailLabel.gameObject.SetActive(hasDetail);
            }

            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() =>
            {
                Hide();
                onConfirm?.Invoke();
            });

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
