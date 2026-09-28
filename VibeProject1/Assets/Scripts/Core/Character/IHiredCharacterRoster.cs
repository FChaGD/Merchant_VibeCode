using System;

namespace Game.Core
{
    /// <summary>
    /// 고용 로직이 쓰는 로스터 조작만 모은 계약(ISP, Docs/설계/54번 §7.2). 정비창·전투는 이 계약을 모르고 기존 읽기 계약
    /// (ICaravanRosterProvider)만 쓴다.
    /// </summary>
    public interface IHiredCharacterRoster
    {
        bool IsHired(string characterId);
        int CountHiredOfClass(string mercenaryClass);
        bool TryAddHired(string characterId);
        event Action OnHiredChanged;
    }
}
