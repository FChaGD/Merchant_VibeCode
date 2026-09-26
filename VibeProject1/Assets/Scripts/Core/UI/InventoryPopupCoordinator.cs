using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public class InventoryPopupCoordinator : MonoBehaviour, IInventoryPopupCoordinator
    {
        private readonly Dictionary<string, IUIPanel> popupsById = new();
        private readonly HashSet<string> openPopupIds = new();

        public void RegisterPopup(IUIPanel popup)
        {
            popupsById[popup.PanelId] = popup;
        }

        public void Toggle(string popupId)
        {
            if (!popupsById.TryGetValue(popupId, out var popup))
            {
                Debug.LogWarning($"'{popupId}'에 해당하는 인벤토리 팝업이 등록되어 있지 않다.");
                return;
            }

            if (openPopupIds.Contains(popupId))
            {
                if (popup is IPanelCloseGuard guard && !guard.TryPrepareClose())
                {
                    return;
                }

                popup.Close();
                openPopupIds.Remove(popupId);
            }
            else
            {
                popup.Open();
                openPopupIds.Add(popupId);
            }
        }

        public bool IsOpen(string popupId) => openPopupIds.Contains(popupId);

        public void Reset()
        {
            popupsById.Clear();
            openPopupIds.Clear();
        }
    }
}
