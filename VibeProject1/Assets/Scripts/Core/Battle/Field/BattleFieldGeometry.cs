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
    }
}
