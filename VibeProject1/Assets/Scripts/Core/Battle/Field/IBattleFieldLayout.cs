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
        // 전장 반지름 자체 - 카메라(BattleFieldCameraView)가 전장을 감싸는 바운딩 박스 크기를 계산할 때
        // 쓴다(Docs/설계/09_전투뷰_카메라_아키텍처.md §3). ComputeFleeTravelDistance와 같은 값이지만
        // 소비자마다 의미가 다르므로 이름을 분리해 노출한다(ISP).
        float ComputeFieldRadius(int columnCount);
    }
}
