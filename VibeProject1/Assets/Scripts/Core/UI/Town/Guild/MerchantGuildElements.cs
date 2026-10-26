using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>상인조합 화면 요소 묶음(TradeGoodsMarketElements와 같은 방식). 하나라도 없으면 화면을 등록하지 않는다.</summary>
    public sealed class MerchantGuildElements
    {
        public GameObject Root { get; private set; }
        public Button ExitButton { get; private set; }
        public RectTransform ListViewport { get; private set; }
        public RectTransform ListContent { get; private set; }
        public GuildQuestRowView RowTemplate { get; private set; }
        public TMP_Text EmptyLabel { get; private set; }
        public TMP_Text DetailTitle { get; private set; }
        public TMP_Text DetailDescription { get; private set; }
        public TMP_Text DetailReward { get; private set; }
        public TMP_Text StatusLabel { get; private set; }
        public Button AcceptButton { get; private set; }

        public static bool TryBind(SceneUIRoot sceneUIRoot, out MerchantGuildElements elements)
        {
            elements = null;
            var ok = InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.Root, out RectTransform root)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.ExitButton, out Button exitButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.ListViewport, out RectTransform listViewport)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.ListContent, out RectTransform listContent)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.RowTemplate, out GuildQuestRowView rowTemplate)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.EmptyLabel, out TMP_Text emptyLabel)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.DetailTitle, out TMP_Text detailTitle)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.DetailDescription, out TMP_Text detailDescription)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.DetailReward, out TMP_Text detailReward)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.StatusLabel, out TMP_Text statusLabel)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.AcceptButton, out Button acceptButton);
            if (!ok) return false;

            elements = new MerchantGuildElements
            {
                Root = root.gameObject,
                ExitButton = exitButton,
                ListViewport = listViewport,
                ListContent = listContent,
                RowTemplate = rowTemplate,
                EmptyLabel = emptyLabel,
                DetailTitle = detailTitle,
                DetailDescription = detailDescription,
                DetailReward = detailReward,
                StatusLabel = statusLabel,
                AcceptButton = acceptButton,
            };
            return true;
        }
    }
}
