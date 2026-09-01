using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 배틀 테스트 씬 전용 - 유닛 팔레트 드래그-드롭이 호출하는 진입점. 전투가 진행 중이면(IsRunning)
    /// 실행 중인 시뮬레이션에 즉시 유닛을 추가하고, 세팅 단계면 BattleTestAllyRoster/
    /// BattleTestEnemyRoster에만 기록해 다음 전투 시작 때 반영한다 - 팔레트 쪽은 이 차이를 몰라도 된다.
    /// </summary>
    public interface ILiveUnitSpawner
    {
        bool IsRunning { get; }
        void SpawnAlly(MercenaryClass unitClass, Vector2 worldPosition);
        void SpawnEnemy(EnemyType enemyType, Vector2 worldPosition);
    }
}
