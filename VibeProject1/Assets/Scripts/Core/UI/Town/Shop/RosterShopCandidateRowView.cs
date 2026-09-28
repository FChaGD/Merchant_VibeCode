using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 목록형 구매 화면의 후보 한 줄(아이콘 | 이름 | 종류 | 가격, Docs/설계/56번 §7.1). 용병 직업·마차/시설 종류를 같은 "종류" 칸에
    /// 표시한다. 순수 표시 담당 - 클릭 판정은 주입된 델리게이트에 위임한다(ShopStockRowView와 같은 방식).
    /// </summary>
    public class RosterShopCandidateRowView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text kindLabel;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private Outline selectionOutline;

        private Action<RosterShopCandidateRowView> onClicked;

        public int Index { get; private set; }
        public RectTransform RectTransform => (RectTransform)transform;

        public void Bind(int index, Sprite iconSprite, string displayName, string kind, int price, Action<RosterShopCandidateRowView> clickHandler)
        {
            Index = index;
            onClicked = clickHandler;
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
            nameLabel.text = displayName;
            kindLabel.text = kind;
            priceLabel.text = price.ToString("N0");

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
