using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// "메시지 + 확인/취소" 구조의 공용 확인 대화상자(Docs/설계/79번 §7·§9.2). 회수 적재 완료 확인과 마을 출발 경고가
    /// 같은 구조라 뷰 클래스 하나를 씬마다 인스턴스로 둔다 - 문구·버튼 라벨·콜백만 호출부가 정한다.
    /// 콜백보다 Hide를 먼저 해 콜백 안에서 같은 대화상자를 다시 Show해도 덮어쓰이지 않게 한다.
    /// </summary>
    public class ConfirmDialogView : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private TMP_Text confirmLabel;
        [SerializeField] private TMP_Text cancelLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        public void Show(string message, string confirmText, string cancelText, Action onConfirm, Action onCancel)
        {
            messageLabel.text = message;
            confirmLabel.text = confirmText;
            cancelLabel.text = cancelText;

            // 같은 인스턴스를 여러 번 띄우므로 이전 호출의 콜백이 남지 않게 매번 비우고 다시 건다.
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() =>
            {
                Hide();
                onConfirm?.Invoke();
            });

            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() =>
            {
                Hide();
                onCancel?.Invoke();
            });

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
