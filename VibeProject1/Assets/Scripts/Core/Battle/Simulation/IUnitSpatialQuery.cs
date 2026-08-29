using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// "위치 하나를 주고 목록에서 공간적으로 관련된 대상을 찾는" 질의를 전략으로 분리한다 - 최근접
    /// 타겟팅(FindNearest)과 근접 반발(ComputeSeparationPush)은 둘 다 이 문제의 변형이다. 지금은
    /// 선형 탐색(O(n)) 구현체(LinearScanUnitSpatialQuery)만 있지만, 전투 규모가 커지면 같은
    /// 인터페이스의 공간 분할(그리드/쿼드트리) 구현체로 교체할 수 있다(OCP) - BattleCharacterUnit은
    /// 무변경으로 남는다.
    /// </summary>
    public interface IUnitSpatialQuery
    {
        /// <summary>candidates 중 position에서 가장 가까운 살아있는 대상을 찾는다. 없으면 null.</summary>
        IDamageable FindNearest(Vector2 position, IReadOnlyList<IDamageable> candidates);

        /// <summary>
        /// candidates 중 self를 제외하고 radius 안으로 겹치는 이웃들을 밀어내는 방향·크기(정규화되지
        /// 않은 push 벡터 합)를 계산한다. 실제로 Position에 얼마나/어떤 속도로 반영할지는 호출자가 정한다.
        /// candidates는 Position/IsAlive만 읽으므로 IDamageable로 충분하다 - SurroundPositioningStrategy가
        /// 아군(IBattleCombatant)뿐 아니라 인식된 적(IDamageable) 목록도 그대로 넘길 수 있어야 해서
        /// IBattleCombatant보다 넓혀뒀다(Docs/설계/12번 §13.4 갱신).
        /// </summary>
        Vector2 ComputeSeparationPush(IBattleCombatant self, Vector2 position, float radius, IReadOnlyList<IDamageable> candidates);
    }
}
