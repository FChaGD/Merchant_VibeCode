using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 목록형 구매 화면 요소 묶음(Docs/설계/56번 §7.1). 화면별 접두사로 찾는다. 씬 로드 시 한 번만 찾아 두고, 하나라도 없으면 화면
    /// 전체를 등록하지 않는다 - 요소가 빠진 화면은 열 수는 있어도 구매가 깨지기 때문이다.
    /// </summary>
    public sealed class RosterShopElements
    {
        public GameObject Root { get; private set; }
        public Button ExitButton { get; private set; }
        public TMP_Text OwnedListLabel { get; private set; }
        public RectTransform CandidateViewport { get; private set; }
        public RectTransform CandidateContent { get; private set; }
        public RosterShopCandidateRowView CandidateRowTemplate { get; private set; }
        public TMP_Text EmptyCandidateLabel { get; private set; }
        public TMP_Text InfoName { get; private set; }
        public TMP_Text InfoKind { get; private set; }
        public TMP_Text InfoDetail { get; private set; }
        public TMP_Text InfoPrice { get; private set; }
        public Button ActionButton { get; private set; }

        public static bool TryBind(SceneUIRoot sceneUIRoot, string prefix, out RosterShopElements elements, bool requireOwnedListLabel = true)
        {
            elements = null;
            // 마구간은 보유 영역을 전용 요소(StableOwnedElements)로 바꿔 텍스트 레이블이 없다(설계 91번 §5.2) - 용병 화면만 요구한다.
            TMP_Text ownedListLabel = null;
            var ownedOk = !requireOwnedListLabel
                || InventoryArrangementElements.TryGet(sceneUIRoot, RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.OwnedListLabel), out ownedListLabel);
            var ok = ownedOk
                & InventoryArrangementElements.TryGet(sceneUIRoot, RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.Root), out RectTransform root)
                & InventoryArrangementElements.TryGet(sceneUIRoot, RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.ExitButton), out Button exitButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.CandidateViewport), out RectTransform candidateViewport)
                & InventoryArrangementElements.TryGet(sceneUIRoot, RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.CandidateContent), out RectTransform candidateContent)
                & InventoryArrangementElements.TryGet(sceneUIRoot, RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.CandidateRowTemplate), out RosterShopCandidateRowView candidateRowTemplate)
                & InventoryArrangementElements.TryGet(sceneUIRoot, RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.EmptyCandidateLabel), out TMP_Text emptyCandidateLabel)
                & InventoryArrangementElements.TryGet(sceneUIRoot, RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.InfoName), out TMP_Text infoName)
                & InventoryArrangementElements.TryGet(sceneUIRoot, RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.InfoKind), out TMP_Text infoKind)
                & InventoryArrangementElements.TryGet(sceneUIRoot, RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.InfoDetail), out TMP_Text infoDetail)
                & InventoryArrangementElements.TryGet(sceneUIRoot, RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.InfoPrice), out TMP_Text infoPrice)
                & InventoryArrangementElements.TryGet(sceneUIRoot, RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.ActionButton), out Button actionButton);
            if (!ok) return false;

            elements = new RosterShopElements
            {
                Root = root.gameObject,
                ExitButton = exitButton,
                OwnedListLabel = ownedListLabel,
                CandidateViewport = candidateViewport,
                CandidateContent = candidateContent,
                CandidateRowTemplate = candidateRowTemplate,
                EmptyCandidateLabel = emptyCandidateLabel,
                InfoName = infoName,
                InfoKind = infoKind,
                InfoDetail = infoDetail,
                InfoPrice = infoPrice,
                ActionButton = actionButton,
            };
            return true;
        }
    }
}
