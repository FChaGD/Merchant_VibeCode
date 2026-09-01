namespace Game.Core
{
    /// <summary>
    /// 전장 기하 상수의 단일 출처. BattleFieldLayout과 IEncounterSpawnPointSelector 구현체가 이 값을
    /// 공유한다 - 각자 따로 들고 있으면 하나만 바뀌었을 때 조용히 어긋난다
    /// (Docs/설계/06-2026-08-31-전투_핵심루프_아키텍처.md §4).
    /// </summary>
    public static class BattleFieldGeometry
    {
        public const int SpawnPointCount = 12;
        // CoordinateToPixelScale(전투 좌표→픽셀 배율)은 전투 뷰가 월드 오브젝트로 전환되면서 폐기됨
        // (Docs/설계/13-2026-08-29-전투뷰_월드오브젝트_전환_아키텍처.md) - 1시뮬레이션 좌표=1월드 유닛이라 변환이 필요 없다.
        // 전투 월드 오브젝트(유닛/배경 타일)가 공유하는 레이어 이름 - Editor 인스톨러(FieldUIInstaller)와
        // 런타임 코드(BattleBackgroundGridView 등) 양쪽에서 문자열이 따로 놀지 않도록 한곳에 둔다.
        public const string BattleLayerName = "Battle";
    }
}
