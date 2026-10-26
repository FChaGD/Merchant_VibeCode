using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Hub 우상단 인벤토리 상시 호출 버튼 4개를 인벤토리 팝업 토글에 연결한다(Docs/기획/31번 §3.5 상시 호출).
    /// 버튼은 PersistentLayer에 있어 depth 전환과 무관하게 보이므로(모달 팝업 동안에는 PopupLayerGate가 숨김),
    /// 루트 depth만 다루는 HubUIController와 분리했다.
    /// 키(I/B/C/P)는 버튼 클릭을 대신 누르는 방식이라 버튼이 막힌 화면에서는 키도 막힌다(Docs/설계/99번 §5.1).
    /// 키 구독 핸들을 들고 있어야 하므로 인스턴스로 두고, Hub 재로드마다 이전 구독을 먼저 해제한다 —
    /// 남기면 키 1회에 팝업이 두 번 토글된다.
    /// </summary>
    public sealed class HubInventoryShortcutBinder : IDisposable
    {
        // 표시 순서: 큰 버튼(교역품/전리품) 1개 + 그 아래 작은 버튼 3개(좌→우). 배치는 HubSceneInstaller가 한다.
        public static readonly string[] PopupIds =
        {
            InventoryPopupIds.TradeGoods,
            InventoryPopupIds.Equipment,
            InventoryPopupIds.Consumable,
            InventoryPopupIds.PersonalItem,
        };

        private readonly IInputManager input;
        private readonly List<IDisposable> keySubscriptions = new();

        // input이 없으면(InputManager 미구성) 버튼 클릭만 연결하고 키 글자는 비운다.
        public HubInventoryShortcutBinder(IInputManager input)
        {
            this.input = input;
        }

        public void Bind(SceneUIRoot sceneUIRoot, IUIManager uiManager)
        {
            Dispose();

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

                var actionId = GameInputActionIds.ForInventoryPopup(popupId);
                if (input != null && actionId != null)
                {
                    keySubscriptions.Add(input.Subscribe(actionId, () =>
                    {
                        if (ShortcutKeyGate.ShouldInvoke(button)) button.onClick.Invoke();
                    }));
                }

                BindKeyLabel(sceneUIRoot, popupId, actionId);
            }
        }

        public void Dispose()
        {
            foreach (var subscription in keySubscriptions) subscription.Dispose();
            keySubscriptions.Clear();
        }

        private void BindKeyLabel(SceneUIRoot sceneUIRoot, string popupId, string actionId)
        {
            var labelId = HubUIElementIds.InventoryShortcutKeyLabel(popupId);
            if (!sceneUIRoot.TryGetElement<TMP_Text>(labelId, out var label))
            {
                Debug.LogWarning($"Hub UI에서 '{labelId}' 요소를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라(Tools > Game > Build Hub Scene).");
                return;
            }

            label.text = input != null && actionId != null ? input.GetBindingLabel(actionId) : string.Empty;
        }
    }
}
