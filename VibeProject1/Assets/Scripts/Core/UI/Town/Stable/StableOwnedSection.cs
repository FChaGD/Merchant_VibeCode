using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마구간 보유 영역(Docs/설계/91번 §5.2, 기획 90번 §3-12·§3-13·§4.3). 목록 화면과 개별 정보 화면을 같은 자리에서 하나만 켠다.
    /// StablePanel에서 분리한 이유: 구매와 수리 두 흐름을 한 패널이 같이 갖지 않게(SRP). plain C#이며 HubUIWiring이 Hub 로드마다 새로 만든다 -
    /// 골드·보유·내구도 이벤트를 구독하므로 StablePanel.Dispose가 함께 해제한다. 수리 서비스·내구도 저장소가 없으면 "값 없음" + 수리 비활성.
    /// </summary>
    public sealed class StableOwnedSection : IDisposable
    {
        private const int VisibleRows = 10; // 목록 영역에 한 번에 보이는 줄 수(잠정)
        private const string NotNeededText = "수리할 필요 없음";
        private const string InsufficientText = "골드 부족";
        private const string RepairAllText = "전체 수리";

        private readonly StableOwnedElements elements;
        private readonly IOwnedCaravanAssetReader owned;
        private readonly ICaravanAssetCatalogReader catalog;
        private readonly ICaravanAssetDurabilityReader durability; // 선택적
        private readonly CaravanAssetRepairService repair;          // 선택적
        private readonly IGoldSpender gold;
        private readonly List<StableOwnedRowView> rows = new();

        private string selectedId;
        private bool isOpen;

        public StableOwnedSection(StableOwnedElements elements, IOwnedCaravanAssetReader owned, ICaravanAssetCatalogReader catalog, ICaravanAssetDurabilityReader durability, CaravanAssetRepairService repair, IGoldSpender gold)
        {
            this.elements = elements;
            this.owned = owned;
            this.catalog = catalog;
            this.durability = durability;
            this.repair = repair;
            this.gold = gold;

            elements.RowTemplate.gameObject.SetActive(false);
            elements.RepairAllButton.onClick.RemoveAllListeners();
            elements.RepairAllButton.onClick.AddListener(RepairAll);
            elements.RepairButton.onClick.RemoveAllListeners();
            elements.RepairButton.onClick.AddListener(RepairSelected);
            elements.BackButton.onClick.RemoveAllListeners();
            elements.BackButton.onClick.AddListener(ShowList);

            gold.Changed += HandleChanged;
            owned.OnOwnedChanged += HandleChanged;
            if (durability != null) durability.Changed += HandleChanged;
        }

        // 열 때마다 목록 화면부터(설계 91번 §5.2).
        public void Open()
        {
            isOpen = true;
            selectedId = null;
            Refresh();
        }

        public void Close() => isOpen = false;

        public void Dispose()
        {
            gold.Changed -= HandleChanged;
            owned.OnOwnedChanged -= HandleChanged;
            if (durability != null) durability.Changed -= HandleChanged;
        }

        private void HandleChanged()
        {
            if (isOpen) Refresh();
        }

        private void Refresh()
        {
            // 보던 개체가 사라졌으면 목록으로 돌아간다.
            if (selectedId != null && !owned.TryGetOwned(selectedId, out _)) selectedId = null;

            var showDetail = selectedId != null;
            elements.ListRoot.SetActive(!showDetail);
            elements.DetailRoot.SetActive(showDetail);
            if (showDetail) RenderDetail();
            else RenderList();
        }

        private void RenderList()
        {
            var entries = StableOwnedListModel.Build(owned, catalog, durability);
            var rowHeight = Mathf.Max(elements.ListViewport.rect.height, 1f) / VisibleRows;

            for (var i = 0; i < entries.Count; i++)
            {
                if (i >= rows.Count) rows.Add(UnityEngine.Object.Instantiate(elements.RowTemplate, elements.ListContent));

                var row = rows[i];
                row.gameObject.SetActive(true);
                var entry = entries[i];
                if (entry.IsHeader) row.BindHeader(entry.Name);
                else row.BindAsset(entry.Name, entry.DurabilityText, entry.Damaged, () => Select(entry.InstanceId));

                var rect = row.RectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -i * rowHeight);
                rect.sizeDelta = new Vector2(0f, rowHeight);
            }
            for (var i = entries.Count; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
            elements.ListContent.sizeDelta = new Vector2(elements.ListContent.sizeDelta.x, entries.Count * rowHeight);

            var total = repair?.TotalCost() ?? 0;
            var check = repair?.EvaluateAll() ?? CaravanAssetRepairCheck.NotDamaged;
            elements.RepairAllLabel.text = total > 0 ? $"{RepairAllText} ({total:N0})" : RepairAllText;
            elements.RepairAllButton.interactable = check == CaravanAssetRepairCheck.Available;
            // 손상 없음은 사유 없이 비활성(기획 90번 §4-16).
            elements.RepairAllReason.text = check == CaravanAssetRepairCheck.InsufficientFunds ? InsufficientText : string.Empty;
        }

        private void RenderDetail()
        {
            owned.TryGetOwned(selectedId, out var asset);
            elements.DetailName.text = OwnedCaravanAssetNames.Format(asset);
            elements.DetailKind.text = asset.Profile.KindLabel;
            elements.DetailDurability.text = "내구도 " + StableOwnedListModel.Describe(durability, selectedId, out _);
            elements.DetailCost.text = repair != null ? $"수리비 {repair.CostOf(selectedId):N0}" : "수리비 " + StableOwnedListModel.MissingText;

            var check = repair?.Evaluate(selectedId);
            elements.RepairButton.interactable = check == CaravanAssetRepairCheck.Available;
            elements.DetailReason.text = check switch
            {
                CaravanAssetRepairCheck.NotDamaged => NotNeededText,
                CaravanAssetRepairCheck.InsufficientFunds => InsufficientText,
                _ => string.Empty,
            };
        }

        private void Select(string instanceId)
        {
            selectedId = instanceId;
            Refresh();
        }

        private void ShowList()
        {
            selectedId = null;
            Refresh();
        }

        // 성공하면 골드·내구도 이벤트로 다시 그려지지만, 실패(경합)도 버튼 상태를 맞추도록 한 번 더 그린다. 확인 창 없음(기획 90번 §4-18).
        private void RepairSelected()
        {
            if (selectedId != null) repair?.TryRepair(selectedId);
            Refresh();
        }

        private void RepairAll()
        {
            repair?.TryRepairAll();
            Refresh();
        }
    }
}
