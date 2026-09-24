using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Hub 우상단 인벤토리 상시 호출 버튼 4개를 인벤토리 팝업 토글에 연결한다(Docs/기획/31번 §3.5 상시 호출).
    /// 버튼은 PersistentLayer에 있어 depth 전환과 무관하게 보이므로(모달 팝업 동안에는 PopupLayerGate가 숨김),
    /// 루트 depth만 다루는 HubUIController와 분리했다. 연결 후 들고 있을 상태가 없어 컴포넌트가 아니라 정적 바인더로 둔다.
    /// </summary>
    public static class HubInventoryShortcutBinder
    {
        // 표시 순서: 큰 버튼(교역품/전리품) 1개 + 그 아래 작은 버튼 3개(좌→우). 배치는 HubSceneInstaller가 한다.
        public static readonly string[] PopupIds =
        {
            InventoryPopupIds.TradeGoods,
            InventoryPopupIds.Equipment,
            InventoryPopupIds.Consumable,
            InventoryPopupIds.PersonalItem,
        };

        public static void Bind(SceneUIRoot sceneUIRoot, IUIManager uiManager)
        {
            foreach (var popupId in PopupIds)
            {
                var id = HubUIElementIds.InventoryShortcutButton(popupId);
                if (!sceneUIRoot.TryGetElement<Button>(id, out var button))
                {
                    Debug.LogWarning($"Hub UI에서 '{id}' 요소를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라(Tools > Game > Build Hub Scene).");
                    continue;
                }

                var capturedId = popupId;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => uiManager.ToggleInventoryPopup(capturedId));
            }
        }
    }
}
