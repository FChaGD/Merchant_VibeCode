using UnityEngine;

namespace Game.Core
{
    public interface IBattleFieldLayout
    {
        Vector2 ComputeAllyPosition(int column, int row, int columnCount);
        // 스폰 반지름이 대형 크기(columnCount)에 연동되므로 두 메서드 모두 columnCount가 필요하다.
        Vector2 ComputeSpawnPoint(int spawnPointIndex, int columnCount);
        // 도주 유닛이 "전장을 완전히 벗어났다"고 볼 이동 거리 - 전장 반지름과 같다(BattleFieldLayout 참고).
        float ComputeFleeTravelDistance(int columnCount);
    }
}
