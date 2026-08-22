using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 상행 관리 데이터에서 배치 가능한 보유 유닛 전체 목록을 제공한다.
    /// 실제 구현은 상행 관리 데이터 시스템 설계 후 연결한다.
    /// </summary>
    public interface ICaravanRosterProvider
    {
        IReadOnlyList<IFormationUnit> GetRoster();
    }
}
