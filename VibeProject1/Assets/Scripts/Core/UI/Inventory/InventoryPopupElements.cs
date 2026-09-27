using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 팝업 1개의 화면 요소 묶음(TownCategoryDepthElements와 같은 방식). 편집 본문(InventoryArrangementElements)에
    /// 떠 있는 창 전용 요소(창 제목 줄, 닫기 버튼)를 더한다(Docs/설계/50번 §6.4). 임시 보관 요소는 스펙이 켠 팝업에서만
    /// 찾는다(설계 42번 §4.1).
    /// </summary>
    public sealed class InventoryPopupElements
    {
        public InventoryArrangementElements Arrangement { get; private set; }
        public DraggableWindow TitleBar { get; private set; }
        public Button CloseButton { get; private set; }

        public static bool TryBind(SceneUIRoot sceneUIRoot, InventoryPopupSpec spec, out InventoryPopupElements elements)
        {
            elements = null;
            var popupId = spec.PopupId;
            var ok = InventoryArrangementElements.TryBind(sceneUIRoot, popupId, spec.HasStaging, out var arrangement)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InventoryPopupUIElementIds.TitleBar(popupId), out DraggableWindow titleBar)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InventoryPopupUIElementIds.CloseButton(popupId), out Button closeButton);
            if (!ok) return false;

            elements = new InventoryPopupElements
            {
                Arrangement = arrangement,
                TitleBar = titleBar,
                CloseButton = closeButton,
            };
            return true;
        }
    }
}
