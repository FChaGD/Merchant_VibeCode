using System;

namespace Game.Core
{
    /// <summary>
    /// 배틀 테스트 씬 전용 - 진행 중인 전투를 완전히 종료하고 세팅 상태(전투 시작 전)로 되돌린다.
    /// 아군/적 세팅 데이터(BattleTestAllyRoster/BattleTestEnemyRoster)는 그대로 유지한다 - "세팅을
    /// 지우는" 것이 아니라 "세팅 상태로 돌아가는" 것이기 때문이다. OnReset은 뷰(스폰된 유닛/결과
    /// 배지 등)가 자신의 정리 로직을 실행할 신호로 쓴다.
    /// </summary>
    public interface IResettableBattleSimulation
    {
        event Action OnReset;
        void ResetToSetup();
    }
}
