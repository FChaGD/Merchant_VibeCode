using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 골드 변환 모달 요소 묶음(TradeGoodsMarketElements와 같은 방식). 하나라도 없으면 모달을 등록하지 않는다 - HUD 클릭은
    /// "등록되지 않은 패널" 경고만 낸다.
    /// </summary>
    public sealed class GoldConversionElements
    {
        public GameObject Root { get; private set; }
        public TMP_Text InfoText { get; private set; }
        public TMP_Text CountText { get; private set; }
        public Button IncreaseButton { get; private set; }
        public Button DecreaseButton { get; private set; }
        public Button ExcessAllButton { get; private set; }
        public Button ConvertButton { get; private set; }
        public Button CloseButton { get; private set; }

        public static bool TryBind(SceneUIRoot sceneUIRoot, out GoldConversionElements elements)
        {
            elements = null;
            var ok = InventoryArrangementElements.TryGet(sceneUIRoot, GoldConversionUIElementIds.Root, out RectTransform root)
                & InventoryArrangementElements.TryGet(sceneUIRoot, GoldConversionUIElementIds.InfoText, out TMP_Text infoText)
                & InventoryArrangementElements.TryGet(sceneUIRoot, GoldConversionUIElementIds.CountText, out TMP_Text countText)
                & InventoryArrangementElements.TryGet(sceneUIRoot, GoldConversionUIElementIds.IncreaseButton, out Button increaseButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, GoldConversionUIElementIds.DecreaseButton, out Button decreaseButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, GoldConversionUIElementIds.ExcessAllButton, out Button excessAllButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, GoldConversionUIElementIds.ConvertButton, out Button convertButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, GoldConversionUIElementIds.CloseButton, out Button closeButton);
            if (!ok) return false;

            elements = new GoldConversionElements
            {
                Root = root.gameObject,
                InfoText = infoText,
                CountText = countText,
                IncreaseButton = increaseButton,
                DecreaseButton = decreaseButton,
                ExcessAllButton = excessAllButton,
                ConvertButton = convertButton,
                CloseButton = closeButton,
            };
            return true;
        }
    }
}
