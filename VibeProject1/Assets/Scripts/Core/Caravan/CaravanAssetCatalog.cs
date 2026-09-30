using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마차·시설 한 개체의 표시·거래용 정보(Docs/설계/56번 §3). 종류는 정비창 유닛 종류(FormationUnitKind.Wagon/Facility)를
    /// 그대로 쓴다 - 로스터·팔레트가 이미 이 값으로 분류한다.
    /// </summary>
    public sealed class CaravanAssetProfile
    {
        public string Id { get; }
        public string Name { get; }
        public FormationUnitKind Kind { get; }
        public string KindLabel { get; }
        public int Price { get; }
        // 대열 영역 모양(Docs/기획/65번). 항상 null이 아니다 - 형식이 틀리면 카탈로그가 기준 칸 1칸으로 바꾼다(설계 66번 §8-3).
        public FormationAreaShape AreaShape { get; }
        // 마차 적재 공간 모양(Docs/기획/63번 §3.1). 시설이거나 테이블 값이 형식에 맞지 않으면 null - 임포터가 형식을 막으므로
        // null인 마차는 임포트 전 자산뿐이다.
        public InventoryShape CargoShape { get; }

        public CaravanAssetProfile(string id, string name, FormationUnitKind kind, string kindLabel, int price, FormationAreaShape areaShape = null, InventoryShape cargoShape = null)
        {
            Id = id;
            Name = name;
            Kind = kind;
            KindLabel = kindLabel;
            Price = price;
            AreaShape = areaShape ?? FormationAreaShape.Single;
            CargoShape = cargoShape;
        }
    }

    /// <summary>마차·시설 테이블 전체 조회(읽기 전용). 목록 순서는 마차 → 시설, 각 테이블 행 순서다.</summary>
    public interface ICaravanAssetCatalogReader
    {
        IReadOnlyList<CaravanAssetProfile> All { get; }
        bool TryGet(string id, out CaravanAssetProfile profile);
        string GetKindLabel(FormationUnitKind kind);
    }

    /// <summary>
    /// 마차·시설 데이터·이름 테이블을 조인한 카탈로그(TableCharacterCatalog와 같은 성격). 종류명은 테이블이 없어 상수로 둔다 -
    /// 지금까지 로스터가 표시명으로 쓰던 값과 같다.
    /// </summary>
    public sealed class TableCaravanAssetCatalog : ICaravanAssetCatalogReader
    {
        private const string MissingText = "값 없음";
        private const string WagonLabel = "마차";
        private const string FacilityLabel = "시설";

        private readonly List<CaravanAssetProfile> all = new();
        private readonly Dictionary<string, CaravanAssetProfile> byId = new();

        public IReadOnlyList<CaravanAssetProfile> All => all;

        public TableCaravanAssetCatalog(CaravanAssetTableAsset wagonTable, CaravanAssetStringsTableAsset wagonStrings, CaravanAssetTableAsset facilityTable, CaravanAssetStringsTableAsset facilityStrings)
        {
            Add(wagonTable, wagonStrings, FormationUnitKind.Wagon);
            Add(facilityTable, facilityStrings, FormationUnitKind.Facility);
        }

        public bool TryGet(string id, out CaravanAssetProfile profile) => byId.TryGetValue(id, out profile);

        public string GetKindLabel(FormationUnitKind kind) => kind switch
        {
            FormationUnitKind.Wagon => WagonLabel,
            FormationUnitKind.Facility => FacilityLabel,
            _ => MissingText,
        };

        private void Add(CaravanAssetTableAsset table, CaravanAssetStringsTableAsset strings, FormationUnitKind kind)
        {
            if (table == null) return;

            foreach (var entry in table.Entries)
            {
                var name = strings != null && strings.TryGetLabel(entry.Id, out var ko) ? ko : MissingText;
                var cargoShape = kind == FormationUnitKind.Wagon && InventoryShape.TryParse(entry.CargoShape, out var parsed, out _) ? parsed : null;
                var profile = new CaravanAssetProfile(entry.Id, name, kind, GetKindLabel(kind), entry.Price, ParseAreaShape(entry), cargoShape);
                all.Add(profile);
                byId[entry.Id] = profile;
            }
        }

        // 임포터가 형식을 막으므로 실패는 임포트 전 자산뿐이다 - 마차가 배치는 되도록 기준 칸 1칸으로 두고 알린다(설계 66번 §8-3).
        private static FormationAreaShape ParseAreaShape(CaravanAssetEntry entry)
        {
            if (FormationAreaShape.TryParse(entry.AreaShape, out var shape, out var error)) return shape;

            Debug.LogWarning($"{nameof(TableCaravanAssetCatalog)}: '{entry.Id}'의 대열 영역 모양(AreaShape)을 읽지 못해 기준 칸 1칸으로 처리한다 - {error} (엑셀 확인 후 Play).");
            return FormationAreaShape.Single;
        }
    }
}
