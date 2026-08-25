using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 승리/패배/도착 세 상황 모두 "메시지 + 버튼 1개" 구조가 동일해 하나의 뷰로 재사용한다
    /// (Docs/설계/04_Field씬_아키텍처.md §5.3). 문구·버튼 라벨·확인 콜백만 상황마다 다르다.
    /// </summary>
    public class FieldResultPopupView : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private TMP_Text buttonLabel;
        [SerializeField] private Button confirmButton;

        public void Show(string message, string buttonText, Action onConfirm)
        {
            messageLabel.text = message;
            buttonLabel.text = buttonText;

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
