using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 유닛 1기당 1개, 적마다 개별로 인식 여부를 판정한다(Docs/기획/12번 §2.1). 상태 갱신과 인식
    /// 목록 조회를 한 메서드로 합쳤다 - 별도 순회 2번(Tick+GetRecognized)을 피하기 위한 최적화
    /// 결정이다(Docs/설계/12번 §7 점검 이력).
    /// </summary>
    public interface IEnemyRecognitionTracker
    {
        IReadOnlyList<IDamageable> TickAndGetRecognized(float deltaTime, IReadOnlyList<IDamageable> allEnemies, IActivityRadiusZone radiusZone);
        void NotifyAttackedBy(IBattleCombatant attacker);
    }
}
