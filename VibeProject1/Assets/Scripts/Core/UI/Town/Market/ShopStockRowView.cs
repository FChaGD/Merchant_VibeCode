using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 상점 판매 목록 한 줄(색 사각형 | 이름 | 가로×세로 | 가격, Docs/기획/48번 §4.2). 순수 표시 담당 - 클릭 판정은 주입된
    /// 델리게이트에 위임한다(InventoryItemView와 같은 방식). 색은 그리드와 같은 품목별 색(InventoryItemColorPalette)을 받아
    /// 구매 후 그리드에서 같은 품목을 알아볼 수 있게 한다.
    /// </summary>
    public class ShopStockRowView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image swatch;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text sizeLabel;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private TMP_Text remainingLabel;
        [SerializeField] private Outline selectionOutline;

        private Action<ShopStockRowView> onClicked;

        public int Index { get; private set; }
        public RectTransform RectTransform => (RectTransform)transform;

        public void Bind(int index, ShopStockEntry entry, Action<ShopStockRowView> clickHandler)
        {
            Index = index;
            onClicked = clickHandler;
            swatch.color = InventoryItemColorPalette.ColorFor(entry.Definition.Id);
            nameLabel.text = entry.Definition.DisplayName;
            // 곱셈 기호(×, U+00D7)는 현재 TMP 폰트에 글리프가 없어 □로 깨진다 - 아이템 이름과 같은 ASCII x를 쓴다.
            sizeLabel.text = $"{entry.Definition.FootprintWidth}x{entry.Definition.FootprintHeight}";
            priceLabel.text = entry.Price.ToString("N0");

            // 품절 행은 제자리에 남긴다(기획 80번 §3-5). 템플릿에 레이블이 없으면(인스톨러 미실행) 표시만 생략한다.
            if (remainingLabel != null) remainingLabel.text = entry.Remaining > 0 ? $"남은 수량 {entry.Remaining}" : "품절";

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
