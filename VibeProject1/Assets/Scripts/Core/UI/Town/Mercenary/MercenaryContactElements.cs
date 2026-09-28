using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 용병단 접촉 화면 요소 묶음(TradeGoodsMarketElements와 같은 방식). 씬 로드 시 한 번만 찾아 둔다. 하나라도 없으면 화면 전체를
    /// 등록하지 않는다 - 요소가 빠진 화면은 열 수는 있어도 고용이 깨지기 때문이다.
    /// </summary>
    public sealed class MercenaryContactElements
    {
        public GameObject Root { get; private set; }
        public Button ExitButton { get; private set; }
        public TMP_Text OwnedListLabel { get; private set; }
        public RectTransform CandidateViewport { get; private set; }
        public RectTransform CandidateContent { get; private set; }
        public MercenaryCandidateRowView CandidateRowTemplate { get; private set; }
        public TMP_Text EmptyCandidateLabel { get; private set; }
        public TMP_Text InfoName { get; private set; }
        public TMP_Text InfoClass { get; private set; }
        public TMP_Text InfoStats { get; private set; }
        public TMP_Text InfoCost { get; private set; }
        public TMP_Text ReasonLabel { get; private set; }
        public Button HireButton { get; private set; }

        public static bool TryBind(SceneUIRoot sceneUIRoot, out MercenaryContactElements elements)
        {
            elements = null;
            var ok = InventoryArrangementElements.TryGet(sceneUIRoot, MercenaryContactUIElementIds.Root, out RectTransform root)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MercenaryContactUIElementIds.ExitButton, out Button exitButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MercenaryContactUIElementIds.OwnedListLabel, out TMP_Text ownedListLabel)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MercenaryContactUIElementIds.CandidateViewport, out RectTransform candidateViewport)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MercenaryContactUIElementIds.CandidateContent, out RectTransform candidateContent)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MercenaryContactUIElementIds.CandidateRowTemplate, out MercenaryCandidateRowView candidateRowTemplate)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MercenaryContactUIElementIds.EmptyCandidateLabel, out TMP_Text emptyCandidateLabel)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MercenaryContactUIElementIds.InfoName, out TMP_Text infoName)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MercenaryContactUIElementIds.InfoClass, out TMP_Text infoClass)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MercenaryContactUIElementIds.InfoStats, out TMP_Text infoStats)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MercenaryContactUIElementIds.InfoCost, out TMP_Text infoCost)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MercenaryContactUIElementIds.ReasonLabel, out TMP_Text reasonLabel)
                & InventoryArrangementElements.TryGet(sceneUIRoot, MercenaryContactUIElementIds.HireButton, out Button hireButton);
            if (!ok) return false;

            elements = new MercenaryContactElements
            {
                Root = root.gameObject,
                ExitButton = exitButton,
                OwnedListLabel = ownedListLabel,
                CandidateViewport = candidateViewport,
                CandidateContent = candidateContent,
                CandidateRowTemplate = candidateRowTemplate,
                EmptyCandidateLabel = emptyCandidateLabel,
                InfoName = infoName,
                InfoClass = infoClass,
                InfoStats = infoStats,
                InfoCost = infoCost,
                ReasonLabel = reasonLabel,
                HireButton = hireButton,
            };
            return true;
        }
    }
}
