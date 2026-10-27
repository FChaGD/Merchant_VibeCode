using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>상인조합 화면 요소 묶음(TradeGoodsMarketElements와 같은 방식). 하나라도 없으면 화면을 등록하지 않는다. 사유 툴팁은 바인딩이 따로 찾는다(없어도 동작).</summary>
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
        public TMP_Text DetailObjectives { get; private set; }
        public TMP_Text DetailDeadline { get; private set; }
        public TMP_Text DetailReward { get; private set; }
        public TMP_Text StatusLabel { get; private set; }
        public Button AcceptButton { get; private set; }
        public Button AbandonButton { get; private set; }
        public Button ReportButton { get; private set; }
        public ConfirmDialogView ConfirmDialog { get; private set; }

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
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.DetailObjectives, out TMP_Text detailObjectives)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.DetailDeadline, out TMP_Text detailDeadline)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.DetailReward, out TMP_Text detailReward)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.StatusLabel, out TMP_Text statusLabel)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.AcceptButton, out Button acceptButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.AbandonButton, out Button abandonButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.ReportButton, out Button reportButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MerchantGuildUIElementIds.ConfirmDialog, out ConfirmDialogView confirmDialog);
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
                DetailObjectives = detailObjectives,
                DetailDeadline = detailDeadline,
                DetailReward = detailReward,
                StatusLabel = statusLabel,
                AcceptButton = acceptButton,
                AbandonButton = abandonButton,
                ReportButton = reportButton,
                ConfirmDialog = confirmDialog,
            };
            return true;
        }
    }
}
