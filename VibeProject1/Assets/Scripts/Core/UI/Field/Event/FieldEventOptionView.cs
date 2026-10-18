using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 선택 화면 선택지 한 줄(설계 89 §6.2·§6.3) - 버튼·글자·흐림용 CanvasGroup을 직렬화 참조로 든다. 인스톨러가 셋을 함께 붙이고 연결하므로 화면이 템플릿에
    /// CanvasGroup이 있다고 전제하지 않는다(2026-10-16 검진).
    /// </summary>
    public class FieldEventOptionView : MonoBehaviour
    {
        private const float LockedAlpha = 0.5f;

        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup group;

        public void Show(string text, bool available, Action onClick)
        {
            gameObject.SetActive(true);
            button.interactable = available;
            group.alpha = available ? 1f : LockedAlpha;
            label.text = text;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
