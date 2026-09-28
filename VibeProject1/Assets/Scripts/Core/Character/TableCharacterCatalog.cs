using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 캐릭터 스탯·이름·직업명 테이블을 조인한 카탈로그(Docs/설계/54번 §3, TableItemCatalog와 같은 성격). 조인은 생성 시 한 번만
    /// 하고 조회는 Dictionary로 한다. 이름이나 직업명이 빠진 행은 "값 없음"으로 채운다 - 임포터가 교차 검증하므로 정상 흐름에선
    /// 생기지 않는다.
    /// </summary>
    public sealed class TableCharacterCatalog : ICharacterCatalogReader
    {
        private const string MissingText = "값 없음";

        private readonly List<CharacterProfile> all = new();
        private readonly Dictionary<string, CharacterProfile> byId = new();
        private readonly MercenaryClassStringsTableAsset classStrings;

        public IReadOnlyList<CharacterProfile> All => all;

        public TableCharacterCatalog(CharacterStatsTableAsset statsTable, CharacterStringsTableAsset nameStrings, MercenaryClassStringsTableAsset classStrings)
        {
            this.classStrings = classStrings;
            if (statsTable == null) return;

            foreach (var entry in statsTable.Entries)
            {
                var name = nameStrings != null && nameStrings.TryGetLabel(entry.Id, out var ko) ? ko : MissingText;
                var classLabel = TryGetClassLabel(entry.MercenaryClass, out var label) ? label : MissingText;
                var profile = new CharacterProfile(entry.Id, name, entry.MercenaryClass, classLabel, TableBattleUnitStatProvider.ToStats(entry), entry.HireCost);
                all.Add(profile);
                byId[entry.Id] = profile;
            }
        }

        public bool TryGet(string characterId, out CharacterProfile profile) => byId.TryGetValue(characterId, out profile);

        public bool TryGetClassLabel(string mercenaryClass, out string classLabel)
        {
            classLabel = null;
            return classStrings != null && classStrings.TryGetLabel(mercenaryClass, out classLabel);
        }
    }
}
