namespace Game.Core
{
    /// <summary>
    /// 전장 기하 상수의 단일 출처. BattleFieldLayout과 IEncounterSpawnPointSelector 구현체가 이 값을
    /// 공유한다 - 각자 따로 들고 있으면 하나만 바뀌었을 때 조용히 어긋난다
    /// (Docs/설계/06_전투_핵심루프_아키텍처.md §4).
    /// </summary>
    public static class BattleFieldGeometry
    {
        public const int SpawnPointCount = 12;
        // 전투 좌표(BattleFieldLayout 반지름 단위) 1단위를 화면 픽셀로 바꾸는 배율 - 유닛 뷰(BattleCharacterUnitView,
        // BattleProtectedUnitView)와 전투 뷰 카메라(BattleFieldCameraView)가 모두 같은 배율을 써야
        // 좌표계가 어긋나지 않는다(Docs/설계/09_전투뷰_카메라_아키텍처.md §4).
        public const float CoordinateToPixelScale = 40f;
    }
}
