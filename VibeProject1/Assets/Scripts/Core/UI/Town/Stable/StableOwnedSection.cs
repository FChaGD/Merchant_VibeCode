using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마구간 보유 영역(Docs/설계/91번 §5.2, 기획 90번 §3-12·§3-13·§4.3). 목록 화면과 개별 정보 화면을 같은 자리에서 하나만 켠다.
    /// StablePanel에서 분리한 이유: 구매와 수리 두 흐름을 한 패널이 같이 갖지 않게(SRP). plain C#이며 HubUIWiring이 Hub 로드마다 새로 만든다 -
    /// 골드·보유·내구도 이벤트를 구독하므로 StablePanel.Dispose가 함께 해제한다. 수리 서비스·내구도 저장소가 없으면 "값 없음" + 수리 비활성.
    /// 개체 정보 화면의 매각 부분(수리 오른쪽)은 협력 객체 StableSaleController가 그린다(설계 103번 §4.6, 2026-10-26 좌측 이동).
    /// </summary>
    public sealed class StableOwnedSection : IDisposable
    {
        private const int VisibleRows = 10; // 목록 영역에 한 번에 보이는 줄 수(잠정)
        private const string RepairAllText = "전체 수리";

        private readonly StableOwnedElements elements;
        private readonly IOwnedCaravanAssetReader owned;
        private readonly ICaravanAssetCatalogReader catalog;
        private readonly ICaravanAssetDurabilityReader durability; // 선택적
        private readonly CaravanAssetRepairService repair;          // 선택적
        private readonly IGoldSpender gold;
        private readonly StableSaleController sale;                 // 선택적 - 매각 요소가 없으면(인스톨러 미실행) 수리만
        // 수리·전체 수리 버튼 비활성 사유 툴팁(Docs/Refactor/2026-10-26_공통.md D-1) - 배선이 등록해 넘기고 Dispose에서 해제한다. 요소가 없으면 null.
        private readonly ITooltipHandle repairTooltip;
        private readonly ITooltipHandle repairAllTooltip;
        private readonly List<StableOwnedRowView> rows = new();

        private string selectedId;
        private bool isOpen;

        public StableOwnedSection(StableOwnedElements elements, IOwnedCaravanAssetReader owned, ICaravanAssetCatalogReader catalog, ICaravanAssetDurabilityReader durability, CaravanAssetRepairService repair, IGoldSpender gold, StableSaleController sale, ITooltipHandle repairTooltip, ITooltipHandle repairAllTooltip)
        {
            this.sale = sale;
            this.repairTooltip = repairTooltip;
            this.repairAllTooltip = repairAllTooltip;
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

            gold.Changed += HandleGoldChanged;
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

        public void Close()
        {
            isOpen = false;
            sale?.Hide();
        }

        public void Dispose()
        {
            gold.Changed -= HandleGoldChanged;
            owned.OnOwnedChanged -= HandleChanged;
            if (durability != null) durability.Changed -= HandleChanged;
            sale?.Dispose();
            repairTooltip?.Dispose();
            repairAllTooltip?.Dispose();
        }

        private void HandleChanged()
        {
            if (isOpen) Refresh();
        }

        // 골드는 수리 가능 여부(버튼 활성·사유)만 바꾼다 - 목록 전체를 다시 만들지 않는다(Docs/Refactor/2026-10-26_씬전용.md I-3, HO-2).
        private void HandleGoldChanged()
        {
            if (!isOpen) return;
            if (selectedId != null) RenderRepairButton();
            else RenderRepairAll();
        }

        private void Refresh()
        {
            // 보던 개체가 사라졌으면 목록으로 돌아간다.
            if (selectedId != null && !owned.TryGetOwned(selectedId, out _)) selectedId = null;

            var showDetail = selectedId != null;
            elements.ListRoot.SetActive(!showDetail);
            elements.DetailRoot.SetActive(showDetail);
            if (showDetail)
            {
                RenderDetail();
                sale?.Show(selectedId);
            }
            else
            {
                sale?.Hide();
                RenderList();
            }
        }

        private void RenderList()
        {
            var entries = StableOwnedListModel.Build(owned, catalog, durability);
            var rowHeight = FixedRowListLayout.RowHeight(elements.ListViewport.rect.height, VisibleRows);
            FixedRowListLayout.Render(rows, entries.Count, elements.RowTemplate, elements.ListContent, rowHeight, (row, i) =>
            {
                var entry = entries[i];
                if (entry.IsHeader) row.BindHeader(entry.Name);
                else row.BindAsset(entry.Name, entry.DurabilityText, entry.Damaged, () => Select(entry.InstanceId));
            });
            RenderRepairAll();
        }

        private void RenderRepairAll()
        {
            var total = repair?.TotalCost() ?? 0;
            var check = repair?.EvaluateAll() ?? CaravanAssetRepairCheck.NotDamaged;
            elements.RepairAllLabel.text = total > 0 ? $"{RepairAllText} ({total:N0})" : RepairAllText;
            elements.RepairAllButton.interactable = check == CaravanAssetRepairCheck.Available;
            // 손상 없음은 사유 없이 비활성(기획 90번 §4-16).
            repairAllTooltip?.SetText(FacilityBlockReasons.ForRepairAll(check));
        }

        private void RenderDetail()
        {
            owned.TryGetOwned(selectedId, out var asset);
            elements.DetailName.text = OwnedCaravanAssetNames.Format(asset);
            elements.DetailKind.text = asset.Profile.KindLabel;
            elements.DetailDurability.text = "내구도 " + StableOwnedListModel.Describe(durability, selectedId, out _);
            elements.DetailCost.text = repair != null ? $"수리비 {repair.CostOf(selectedId):N0}" : "수리비 " + StableOwnedListModel.MissingText;
            RenderRepairButton();
        }

        private void RenderRepairButton()
        {
            var check = repair?.Evaluate(selectedId);
            elements.RepairButton.interactable = check == CaravanAssetRepairCheck.Available;
            repairTooltip?.SetText(FacilityBlockReasons.ForRepair(check));
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
