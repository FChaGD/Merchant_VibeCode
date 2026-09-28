using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 고용 후보 한 줄(직업 아이콘 | 이름 | 직업명 | 고용비, Docs/기획/53번 §4.1). 순수 표시 담당 - 클릭 판정은 주입된 델리게이트에
    /// 위임한다(ShopStockRowView와 같은 방식).
    /// </summary>
    public class MercenaryCandidateRowView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text classLabel;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private Outline selectionOutline;

        private Action<MercenaryCandidateRowView> onClicked;

        public int Index { get; private set; }
        public RectTransform RectTransform => (RectTransform)transform;

        public void Bind(int index, CharacterProfile candidate, Sprite classIcon, Action<MercenaryCandidateRowView> clickHandler)
        {
            Index = index;
            onClicked = clickHandler;
            icon.sprite = classIcon;
            icon.enabled = classIcon != null;
            nameLabel.text = candidate.Name;
            classLabel.text = candidate.ClassLabel;
            costLabel.text = candidate.HireCost.ToString("N0");

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(this));
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectionOutline != null) selectionOutline.enabled = selected;
        }
    }
}
