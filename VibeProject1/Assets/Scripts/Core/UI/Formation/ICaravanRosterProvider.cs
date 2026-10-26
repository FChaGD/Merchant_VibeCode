using System;
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

        /// <summary>
        /// 고용·사망 제거·구매·파괴 등으로 목록이 바뀌면 발생한다. 목록만 읽는 소비자(재화 HUD 보유 현황)가 쓰기 조작이 섞인
        /// 고용·보유 계약을 따로 구독하지 않게 여기 둔다(Docs/설계/101번 §3·§4.1).
        /// </summary>
        event Action RosterChanged;
    }
}
