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
        // 방진 형성 로직(Docs/설계/12번 §12.4)이 각 유닛 Tick 이전(코디네이터 Update 시점)에
        // "지금까지 인식된 적 전체"를 읽어야 해서 추가 - TickAndGetRecognized를 또 호출하면 인식
        // 타이머가 이번 틱에 두 번 흘러버린다. 이 프로퍼티는 상태를 갱신하지 않고 현재 집합만
        // 읽는다(죽은 적 필터링은 호출자 책임 - allEnemies 기준 필터링은 TickAndGetRecognized만 한다).
        IReadOnlyCollection<IDamageable> RecognizedSnapshot { get; }
    }
}
