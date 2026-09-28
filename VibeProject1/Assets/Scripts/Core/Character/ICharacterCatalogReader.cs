using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 캐릭터 테이블 전체 조회(읽기 전용, Docs/설계/54번 §3). 목록 순서는 테이블 행 순서다.
    /// </summary>
    public interface ICharacterCatalogReader
    {
        IReadOnlyList<CharacterProfile> All { get; }
        bool TryGet(string characterId, out CharacterProfile profile);
        bool TryGetClassLabel(string mercenaryClass, out string classLabel);
    }
}
