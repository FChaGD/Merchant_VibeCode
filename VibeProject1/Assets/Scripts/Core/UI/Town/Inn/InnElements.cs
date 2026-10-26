using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>숙소 탭 1개 = 탭 버튼 + 인벤토리 편집 본문(Docs/설계/103번 §4.3).</summary>
    public sealed class InnTabElements
    {
        public InnTabElements(InventoryPopupSpec spec, Button button, InventoryArrangementElements body)
        {
            Spec = spec;
            Button = button;
            Body = body;
        }

        public InventoryPopupSpec Spec { get; }
        public Button Button { get; }
        public InventoryArrangementElements Body { get; }
    }

    /// <summary>
    /// 숙소 화면 요소 묶음(TradeGoodsMarketElements와 같은 방식). 하나라도 없으면 화면 전체를 등록하지 않는다 - 요소가 빠진 화면은
    /// 열 수는 있어도 휴식이나 정리가 깨지기 때문이다.
    /// </summary>
    public sealed class InnElements
    {
        public GameObject Root { get; private set; }
        public Button ExitButton { get; private set; }
        public Button DaysDownButton { get; private set; }
        public Button DaysUpButton { get; private set; }
        public TMP_Text DaysLabel { get; private set; }
        public TMP_Text CostPerDayLabel { get; private set; }
        public TMP_Text TotalCostLabel { get; private set; }
        public Button RestButton { get; private set; }
        public PointerHoverRelay RestHover { get; private set; }
        public PointerPressRelay RestPress { get; private set; }
        public GameObject RestTooltip { get; private set; }
        public TMP_Text RestTooltipText { get; private set; }
        public IReadOnlyList<InnTabElements> Tabs { get; private set; }

        public static bool TryBind(SceneUIRoot sceneUIRoot, IReadOnlyList<InventoryPopupSpec> tabSpecs, out InnElements elements)
        {
            elements = null;
            var ok = InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.Root, out RectTransform root)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.ExitButton, out Button exitButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.DaysDownButton, out Button daysDown)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.DaysUpButton, out Button daysUp)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.DaysLabel, out TMP_Text daysLabel)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.CostPerDayLabel, out TMP_Text costPerDayLabel)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.TotalCostLabel, out TMP_Text totalCostLabel)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.RestButton, out Button restButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.RestButton, out PointerHoverRelay restHover)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.RestButton, out PointerPressRelay restPress)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.RestTooltip, out RectTransform restTooltip)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.RestTooltipText, out TMP_Text restTooltipText);

            var tabs = new List<InnTabElements>();
            foreach (var spec in tabSpecs)
            {
                var tabOk = InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.TabButton(spec.PopupId), out Button tabButton)
                    & InventoryArrangementElements.TryBind(sceneUIRoot, InnUIElementIds.BodyPrefix(spec.PopupId), spec.HasStaging, spec.HasSections, out var body);
                if (tabOk) tabs.Add(new InnTabElements(spec, tabButton, body));
                ok &= tabOk;
            }

            if (!ok) return false;

            elements = new InnElements
            {
                Root = root.gameObject,
                ExitButton = exitButton,
                DaysDownButton = daysDown,
                DaysUpButton = daysUp,
                DaysLabel = daysLabel,
                CostPerDayLabel = costPerDayLabel,
                TotalCostLabel = totalCostLabel,
                RestButton = restButton,
                RestHover = restHover,
                RestPress = restPress,
                RestTooltip = restTooltip.gameObject,
                RestTooltipText = restTooltipText,
                Tabs = tabs,
            };
            return true;
        }
    }

    /// <summary>휴식 진행 화면 요소 묶음(Docs/설계/103번 §4.4).</summary>
    public sealed class InnRestProgressElements
    {
        public GameObject Root { get; private set; }
        public RectTransform GaugeFill { get; private set; }
        public TMP_Text DaysLabel { get; private set; }
        public TMP_Text DateLabel { get; private set; }
        public TMP_Text CostLabel { get; private set; }
        public TMP_Text GoldLabel { get; private set; }
        public TMP_Text ResultLabel { get; private set; }
        public Button StopButton { get; private set; }
        public Button ConfirmButton { get; private set; }

        public static bool TryBind(SceneUIRoot sceneUIRoot, out InnRestProgressElements elements)
        {
            elements = null;
            var ok = InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.ProgressRoot, out RectTransform root)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.ProgressGaugeFill, out RectTransform gaugeFill)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.ProgressDays, out TMP_Text days)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.ProgressDate, out TMP_Text date)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.ProgressCost, out TMP_Text cost)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.ProgressGold, out TMP_Text gold)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.ProgressResult, out TMP_Text result)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.ProgressStopButton, out Button stop)
                & InventoryArrangementElements.TryGet(sceneUIRoot, InnUIElementIds.ProgressConfirmButton, out Button confirm);
            if (!ok) return false;

            elements = new InnRestProgressElements
            {
                Root = root.gameObject,
                GaugeFill = gaugeFill,
                DaysLabel = days,
                DateLabel = date,
                CostLabel = cost,
                GoldLabel = gold,
                ResultLabel = result,
                StopButton = stop,
                ConfirmButton = confirm,
            };
            return true;
        }
    }
}
