using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>마구간 보유 영역 요소 묶음(Docs/설계/91번 §5.2). 씬 로드 시 한 번 찾고, 하나라도 없으면 보유 영역 없이 구매만 등록한다.</summary>
    public sealed class StableOwnedElements
    {
        public GameObject ListRoot { get; private set; }
        public RectTransform ListViewport { get; private set; }
        public RectTransform ListContent { get; private set; }
        public StableOwnedRowView RowTemplate { get; private set; }
        public Button RepairAllButton { get; private set; }
        public TMP_Text RepairAllLabel { get; private set; }
        public TMP_Text RepairAllReason { get; private set; }
        public GameObject DetailRoot { get; private set; }
        public TMP_Text DetailName { get; private set; }
        public TMP_Text DetailKind { get; private set; }
        public TMP_Text DetailDurability { get; private set; }
        public TMP_Text DetailCost { get; private set; }
        public TMP_Text DetailReason { get; private set; }
        public Button RepairButton { get; private set; }
        public Button BackButton { get; private set; }

        public static bool TryBind(SceneUIRoot sceneUIRoot, out StableOwnedElements elements)
        {
            elements = null;
            var ok = InventoryArrangementElements.TryGet(sceneUIRoot, StableUIElementIds.ListRoot, out RectTransform listRoot)
                & InventoryArrangementElements.TryGet(sceneUIRoot, StableUIElementIds.ListViewport, out RectTransform listViewport)
                & InventoryArrangementElements.TryGet(sceneUIRoot, StableUIElementIds.ListContent, out RectTransform listContent)
                & InventoryArrangementElements.TryGet(sceneUIRoot, StableUIElementIds.RowTemplate, out StableOwnedRowView rowTemplate)
                & InventoryArrangementElements.TryGet(sceneUIRoot, StableUIElementIds.RepairAllButton, out Button repairAllButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, StableUIElementIds.RepairAllLabel, out TMP_Text repairAllLabel)
                & InventoryArrangementElements.TryGet(sceneUIRoot, StableUIElementIds.RepairAllReason, out TMP_Text repairAllReason)
                & InventoryArrangementElements.TryGet(sceneUIRoot, StableUIElementIds.DetailRoot, out RectTransform detailRoot)
                & InventoryArrangementElements.TryGet(sceneUIRoot, StableUIElementIds.DetailName, out TMP_Text detailName)
                & InventoryArrangementElements.TryGet(sceneUIRoot, StableUIElementIds.DetailKind, out TMP_Text detailKind)
                & InventoryArrangementElements.TryGet(sceneUIRoot, StableUIElementIds.DetailDurability, out TMP_Text detailDurability)
                & InventoryArrangementElements.TryGet(sceneUIRoot, StableUIElementIds.DetailCost, out TMP_Text detailCost)
                & InventoryArrangementElements.TryGet(sceneUIRoot, StableUIElementIds.DetailReason, out TMP_Text detailReason)
                & InventoryArrangementElements.TryGet(sceneUIRoot, StableUIElementIds.RepairButton, out Button repairButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, StableUIElementIds.BackButton, out Button backButton);
            if (!ok) return false;

            elements = new StableOwnedElements
            {
                ListRoot = listRoot.gameObject,
                ListViewport = listViewport,
                ListContent = listContent,
                RowTemplate = rowTemplate,
                RepairAllButton = repairAllButton,
                RepairAllLabel = repairAllLabel,
                RepairAllReason = repairAllReason,
                DetailRoot = detailRoot.gameObject,
                DetailName = detailName,
                DetailKind = detailKind,
                DetailDurability = detailDurability,
                DetailCost = detailCost,
                DetailReason = detailReason,
                RepairButton = repairButton,
                BackButton = backButton,
            };
            return true;
        }
    }
}
