using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 스폰/전장/도주/활동 반경 등 전부 같은 FormationExtentRadius(대형 반지름)에서 파생되는
    /// 계산 패밀리 - 아군 좌표 변환(IAllyPositionLayout)과는 분리된 책임이다(Docs/설계/12번 §5.2).
    /// </summary>
    public interface IBattleFieldGeometry
    {
        // 스폰 반지름이 대형 크기(columnCount)에 연동되므로 두 메서드 모두 columnCount가 필요하다.
        Vector2 ComputeSpawnPoint(int spawnPointIndex, int columnCount);
        // 도주 유닛이 "전장을 완전히 벗어났다"고 볼 이동 거리 - 전장 반지름과 같다(BattleFieldLayout 참고).
        float ComputeFleeTravelDistance(int columnCount);
        // 전장 반지름 자체 - 전투 카메라(BattleFieldWorldCameraView)가 전장을 감싸는 시야 경계를
        // 계산할 때 쓴다(Docs/설계/13_전투뷰_월드오브젝트_전환_아키텍처.md, 09번 문서의 UGUI
        // 버전에서 전환됨). ComputeFleeTravelDistance와 같은 값이지만 소비자마다 의미가 다르므로
        // 이름을 분리해 노출한다(ISP).
        float ComputeFieldRadius(int columnCount);
        // 스폰 반지름 자체(= ComputeFieldRadius + FieldBoundaryGap, 늘 더 바깥) - 전투 배경 타일
        // 그리드(BattleBackgroundGridView)가 적 스폰 링을 전부 감싸는 정사각형 크기를 계산할 때
        // 쓴다(Docs/설계/13번, 네 번째 소비자가 생겨 노출). ComputeSpawnPoint가 내부적으로 쓰던
        // 값을 그대로 승격했다.
        float ComputeSpawnRadius(int columnCount);
        // 방향성 지시 "활동 반경 - 표준" 프리셋의 기준 반지름(대형 중심→모서리 사선거리 + 마진,
        // Docs/기획/12번 §2.2). 다른 두 파생값과 같은 자리, 같은 패턴.
        float ComputeStandardActivityRadius(int columnCount);
    }
}
