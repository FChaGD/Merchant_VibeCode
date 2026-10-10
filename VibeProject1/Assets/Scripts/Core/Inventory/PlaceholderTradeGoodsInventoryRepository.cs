using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 보유 마차 1대 = 그리드 섹션 1개(Docs/기획/63번 §3.2, 설계 64번 §5). 모양은 마차 테이블의 적재 모양(CargoShape)이고,
    /// 섹션 Id는 마차 개체 Id(설계 81번 §5.1), 이름은 "n번 이름", 순서는 보유 순서다. 섹션은 보유 목록 변경 때마다 맞춘다 - 마구간 구매로 늘어난 마차는 추가하고, 전투에서
    /// 파괴돼 보유 목록에서 빠진 마차는 제거한다(기획 77번 §3-8, 설계 79번 §5.2). 보유 목록 쪽이 시작 보유분을 넣은 뒤 변경 이벤트를
    /// 내므로 DI 해결 순서와 무관하게 맞춰진다.
    ///
    /// 골드 상자(아이템 테이블 "gold-box" 행)도 이 저장소에서는 일반 아이템이다 - 지갑 차감(변환)·인출(지출)은 골드 보유 서비스가
    /// 맡는다(Docs/설계/83번 §3.4). 골드 상자는 기타 카테고리에 있지만 교역품 그리드에 놓이므로, 카탈로그를 교역품 + 기타로
    /// 합쳐 조회한다(Docs/설계/50번 §5.2).
    /// 전투 정산 계약(ITradeGoodsCargoSettlement)은 이제 일반 경로와 동작이 같지만 battle 브랜치 소비자가 있어 유지한다(설계 83번 §10).
    /// </summary>
    public class PlaceholderTradeGoodsInventoryRepository : MonoBehaviour, ITradeGoodsInventoryRepository, ITradeGoodsCargoSettlement, IManagedComponent
    {
        // 상단 물류품 팝업의 드래그/교환/회전 검증용 초기 배치(Docs/기획/39번 §3.5/§4.4). 품목은 실제
        // 아이템 테이블(TradeGoods.xlsx)의 "placeholder-*" 행 - 실제 아이템 획득 시스템이 생기면 이 목록과
        // 테이블의 placeholder 행을 함께 제거한다.
        // 첫 섹션(시작 마차, 5 × 4)이 생길 때 한 번 그 섹션에 놓는다(설계 64번 §5).
        private static readonly (string itemId, int x, int y)[] PlaceholderInitialItems =
        {
            ("placeholder-1x1", 0, 0),
            ("placeholder-1x1", 0, 1),
            ("placeholder-2x1", 1, 0),
            ("placeholder-1x2", 3, 0),
            ("placeholder-2x2", 1, 2),
        };

        [SerializeField] private ItemDefinitionTableAsset itemTable;
        [SerializeField] private ItemStringTableAsset itemStrings;
        [SerializeField] private ItemDefinitionTableAsset miscItemTable;
        [SerializeField] private ItemStringTableAsset miscItemStrings;

        private IOwnedCaravanAssetReader ownedAssets;
        private InventoryGrid grid = new();
        private IItemCatalogReader catalog;
        private bool seeded;

        public IReadOnlyList<InventorySection> Sections => grid.Sections;
        public IReadOnlyCollection<InventoryItemInstance> Items => grid.Items;
        public IReadOnlyList<InventoryItemInstance> StagedItems => grid.StagedItems;
        public event Action OnChanged;

        public IReadOnlyList<IInventoryItemDefinition> CatalogItems => catalog.CatalogItems;
        public bool TryGetDefinition(string id, out IInventoryItemDefinition definition) => catalog.TryGetDefinition(id, out definition);

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ITradeGoodsInventoryRepository>(this);
            // 보조 계약은 상위 타입 등록으로 조회되지 않는다 - 정산 소비자가 TryResolve로 찾을 수 있게 그 타입으로 따로 등록한다.
            registrar.Register<ITradeGoodsCargoSettlement>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            catalog = new CompositeItemCatalog(new TableItemCatalog(itemTable, itemStrings), new TableItemCatalog(miscItemTable, miscItemStrings));

            grid = new InventoryGrid();
            seeded = false;

            if (ownedAssets != null) ownedAssets.OnOwnedChanged -= HandleOwnedChanged;
            ownedAssets = null;
            if (registrar == null || !registrar.TryResolve(out ownedAssets))
            {
                Debug.LogWarning($"{nameof(PlaceholderTradeGoodsInventoryRepository)}: {nameof(IOwnedCaravanAssetReader)}가 없어 마차 적재 공간 없이 시작한다(Tools > Game > Build Bootstrap Scene).");
                return;
            }

            ownedAssets.OnOwnedChanged += HandleOwnedChanged;
            SyncSections();
        }

        private void OnDestroy()
        {
            if (ownedAssets != null) ownedAssets.OnOwnedChanged -= HandleOwnedChanged;
        }

        public bool TryGetItemAt(string sectionId, GridPosition position, out InventoryItemInstance item) => grid.TryGetAt(sectionId, position, out item);

        public bool TryPlaceItem(IInventoryItemDefinition definition, GridPosition position, out InventoryItemInstance placed, int quarterTurns = 0, string sectionId = null)
        {
            if (!grid.TryPlace(definition, position, out placed, quarterTurns, sectionId)) return false;

            OnChanged?.Invoke();
            return true;
        }

        // 재배치는 인벤토리 유입/유출이 아니다(Docs/설계/40번 §3.2).
        public bool TryApplyPlacements(IReadOnlyList<ItemPlacement> placements)
        {
            if (!grid.TryApplyPlacements(placements)) return false;

            OnChanged?.Invoke();
            return true;
        }

        public bool CanApplyPlacements(IReadOnlyList<ItemPlacement> placements) => grid.CanApplyPlacements(placements);

        public bool RemoveItem(string instanceId)
        {
            if (!grid.Remove(instanceId)) return false;

            OnChanged?.Invoke();
            return true;
        }

        public bool RemoveWithoutRefund(string instanceId)
        {
            if (!grid.Remove(instanceId)) return false;

            OnChanged?.Invoke();
            return true;
        }

        public InventoryItemInstance StageWithoutCharge(IInventoryItemDefinition definition)
        {
            var staged = grid.StageNew(definition);
            OnChanged?.Invoke();
            return staged;
        }

        public void DiscardStaged()
        {
            if (grid.StagedItems.Count == 0) return;

            var stagedIds = new List<string>(grid.StagedItems.Count);
            foreach (var staged in grid.StagedItems) stagedIds.Add(staged.InstanceId);
            foreach (var instanceId in stagedIds) grid.Remove(instanceId);

            OnChanged?.Invoke();
        }

        private void HandleOwnedChanged()
        {
            if (SyncSections()) OnChanged?.Invoke();
        }

        // 보유 목록에서 빠진 마차의 섹션을 제거하고, 아직 섹션이 없는 보유 마차를 보유 순서대로 추가한다. 섹션이 바뀌면 true.
        private bool SyncSections()
        {
            var changed = RemoveUnownedSections();
            var added = false;
            foreach (var wagonId in ownedAssets.GetOwnedIds(FormationUnitKind.Wagon))
            {
                if (!ownedAssets.TryGetOwned(wagonId, out var asset) || asset.Profile.CargoShape == null)
                {
                    Debug.LogWarning($"{nameof(PlaceholderTradeGoodsInventoryRepository)}: 마차 '{wagonId}'의 적재 모양(CargoShape)이 없어 적재 공간을 만들지 않았다(Wagon.xlsx 확인 후 Play).");
                    continue;
                }

                var name = OwnedCaravanAssetNames.Format(asset);
                // 재번호(설계 81번 §5.3) - 이미 있는 섹션도 이름을 다시 맞춘다. 이름만 바뀌어도 변경 이벤트를 내야 팝업이 다시 그린다.
                if (grid.HasSection(wagonId)) added |= grid.TrySetSectionDisplayName(wagonId, name);
                else added |= grid.AddSection(new InventorySection(wagonId, name, asset.Profile.CargoShape));
            }

            if (!seeded && grid.Sections.Count > 0)
            {
                seeded = true;
                PlaceholderInventorySeeder.PlaceAll(grid, catalog, PlaceholderInitialItems, nameof(PlaceholderTradeGoodsInventoryRepository), grid.Sections[0].Id);
            }
            return changed | added;
        }

        // 정상 흐름(설계 79번 §5 순서 1)에서는 파괴 마차의 물품이 이미 모두 정산으로 제거돼 섹션이 비어 있다. 남은 물품이 있으면
        // 섹션과 함께 사라지지 않도록 임시 보관으로 옮기고 경고한다 - 정산 순서가 어긋난 버그를 조용히 숨기지 않기 위함이다.
        private bool RemoveUnownedSections()
        {
            var ownedWagonIds = new HashSet<string>(ownedAssets.GetOwnedIds(FormationUnitKind.Wagon));
            var unownedSectionIds = new List<string>();
            foreach (var section in grid.Sections)
            {
                if (!ownedWagonIds.Contains(section.Id)) unownedSectionIds.Add(section.Id);
            }

            var removed = false;
            foreach (var sectionId in unownedSectionIds)
            {
                var leftovers = new List<ItemPlacement>();
                foreach (var item in grid.Items)
                {
                    if (item.SectionId == sectionId) leftovers.Add(ItemPlacement.ToStaging(item.InstanceId, item.QuarterTurns));
                }

                if (leftovers.Count > 0)
                {
                    grid.TryApplyPlacements(leftovers); // 임시 보관은 칸을 점유하지 않아 실패하지 않는다.
                    Debug.LogWarning($"{nameof(PlaceholderTradeGoodsInventoryRepository)}: 보유 목록에서 빠진 마차 '{sectionId}'의 적재 공간에 물품 {leftovers.Count}개가 남아 있어 임시 보관으로 옮겼다(정산 순서 확인).");
                }

                removed |= grid.RemoveSection(sectionId);
            }
            return removed;
        }
    }
}
