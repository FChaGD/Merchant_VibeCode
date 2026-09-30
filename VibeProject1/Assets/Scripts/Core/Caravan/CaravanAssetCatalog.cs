using System.Collections.Generic;

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
        public FormationAreaSpan AreaSpan { get; }

        public CaravanAssetProfile(string id, string name, FormationUnitKind kind, string kindLabel, int price, FormationAreaSpan areaSpan = default)
        {
            Id = id;
            Name = name;
            Kind = kind;
            KindLabel = kindLabel;
            Price = price;
            AreaSpan = areaSpan;
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
                var profile = new CaravanAssetProfile(entry.Id, name, kind, GetKindLabel(kind), entry.Price, new FormationAreaSpan(entry.Up, entry.Down, entry.Left, entry.Right));
                all.Add(profile);
                byId[entry.Id] = profile;
            }
        }
    }
}
