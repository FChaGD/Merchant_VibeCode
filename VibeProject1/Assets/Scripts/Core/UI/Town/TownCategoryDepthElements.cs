using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 카테고리 depth의 화면 요소 묶음. 카테고리 4개의 TownCategoryPanel이 이 묶음 하나를 공유한다 -
    /// 단일 패널 정책(PanelNavigationStack)상 두 카테고리가 동시에 열리지 않으므로 공유가 안전하다.
    /// 요소 조회는 씬 로드 시 한 번만 한다.
    /// </summary>
    public class TownCategoryDepthElements
    {
        public GameObject Root { get; private set; }
        public TownButtonColumnView Column { get; private set; }
        public Button BackButton { get; private set; }

        private readonly Dictionary<string, Button> facilityButtonsById = new();

        public bool TryGetFacilityButton(string facilityId, out Button button) => facilityButtonsById.TryGetValue(facilityId, out button);

        public static bool TryBind(SceneUIRoot sceneUIRoot, out TownCategoryDepthElements elements)
        {
            elements = null;

            if (!sceneUIRoot.TryGetElement<TownButtonColumnView>(TownUIElementIds.CategoryDepthRoot, out var column))
            {
                WarnMissing(TownUIElementIds.CategoryDepthRoot);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<Button>(TownUIElementIds.BackButton, out var backButton))
            {
                WarnMissing(TownUIElementIds.BackButton);
                return false;
            }

            elements = new TownCategoryDepthElements
            {
                Root = column.gameObject,
                Column = column,
                BackButton = backButton,
            };

            foreach (var facilityId in TownFacilityCatalog.AllFacilityIds)
            {
                var id = TownUIElementIds.FacilityButton(facilityId);
                if (sceneUIRoot.TryGetElement<Button>(id, out var facilityButton))
                {
                    elements.facilityButtonsById[facilityId] = facilityButton;
                }
                else
                {
                    // 시설 버튼 하나가 빠져도 나머지 depth는 동작해야 한다 - 해당 버튼만 표시에서 제외된다.
                    WarnMissing(id);
                }
            }

            return true;
        }

        private static void WarnMissing(string id)
        {
            Debug.LogWarning($"마을 카테고리 depth에서 '{id}' 요소를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라(Tools > Game > Build Hub Scene).");
        }
    }
}
