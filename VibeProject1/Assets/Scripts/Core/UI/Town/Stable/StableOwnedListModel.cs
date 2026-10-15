using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>마구간 보유 목록 한 줄 - 종류 제목("마차 2") 또는 개체("1번 마차이름1  150/200").</summary>
    public readonly struct StableOwnedEntry
    {
        public bool IsHeader { get; }
        public string InstanceId { get; }
        public string Name { get; }
        public string DurabilityText { get; }
        public bool Damaged { get; }

        public StableOwnedEntry(bool isHeader, string instanceId, string name, string durabilityText, bool damaged)
        {
            IsHeader = isHeader;
            InstanceId = instanceId;
            Name = name;
            DurabilityText = durabilityText;
            Damaged = damaged;
        }
    }

    /// <summary>
    /// 마구간 보유 목록 줄 만들기(Docs/설계/91번 §5.2, 기획 90번 §4-12). 화면(TMP)과 분리해 줄 구성·표시값을 Unity 밖에서 검증한다.
    /// 종류 순서는 마차 → 시설(정비창 팔레트 순서와 같음), 종류 안은 보유 순서. 제목의 상한 표기 없음은 기획 80번 §3-10 그대로.
    /// </summary>
    public static class StableOwnedListModel
    {
        public const string MissingText = "값 없음";
        private static readonly FormationUnitKind[] Kinds = { FormationUnitKind.Wagon, FormationUnitKind.Facility };

        public static List<StableOwnedEntry> Build(IOwnedCaravanAssetReader owned, ICaravanAssetCatalogReader catalog, ICaravanAssetDurabilityReader durability)
        {
            var entries = new List<StableOwnedEntry>();
            foreach (var kind in Kinds)
            {
                var ids = owned.GetOwnedIds(kind);
                entries.Add(new StableOwnedEntry(true, null, $"{catalog.GetKindLabel(kind)} {ids.Count}", string.Empty, false));
                foreach (var id in ids)
                {
                    if (!owned.TryGetOwned(id, out var asset)) continue;
                    var text = Describe(durability, id, out var damaged);
                    entries.Add(new StableOwnedEntry(false, id, OwnedCaravanAssetNames.Format(asset), text, damaged));
                }
            }
            return entries;
        }

        /// <summary>"현재/최대"(현재는 올림). 내구도 저장소가 없거나 기록이 없으면 "값 없음".</summary>
        public static string Describe(ICaravanAssetDurabilityReader durability, string instanceId, out bool damaged)
        {
            damaged = false;
            if (durability == null || !durability.TryGet(instanceId, out var current, out var max)) return MissingText;
            damaged = CaravanAssetDurabilityRules.IsDamaged(current, max);
            return $"{CaravanAssetDurabilityRules.Display(current)}/{max}";
        }
    }
}
