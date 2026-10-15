using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 마구간 보유 목록 한 줄(Docs/설계/91번 §5.2) - 종류 제목 줄 또는 개체 줄(이름 | 현재/최대). 제목 줄도 같은 템플릿을 써서 줄 재사용을
    /// 한 목록으로 처리한다. 순수 표시 담당 - 클릭 처리는 주입된 델리게이트에 위임한다.
    /// </summary>
    public class StableOwnedRowView : MonoBehaviour
    {
        private static readonly Color HeaderColor = new(0.85f, 0.81f, 0.72f, 1f);
        private static readonly Color AssetColor = Color.white;
        private static readonly Color NormalTextColor = new(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color DamagedTextColor = new(0.75f, 0.15f, 0.1f, 1f);

        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text durabilityLabel;

        public RectTransform RectTransform => (RectTransform)transform;

        public void BindHeader(string title)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = false;
            background.color = HeaderColor;
            nameLabel.text = $"<b>{title}</b>";
            durabilityLabel.text = string.Empty;
        }

        public void BindAsset(string displayName, string durabilityText, bool damaged, Action onClicked)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke());
            button.interactable = true;
            background.color = AssetColor;
            nameLabel.text = displayName;
            durabilityLabel.text = durabilityText;
            durabilityLabel.color = damaged ? DamagedTextColor : NormalTextColor;
        }
    }
}
