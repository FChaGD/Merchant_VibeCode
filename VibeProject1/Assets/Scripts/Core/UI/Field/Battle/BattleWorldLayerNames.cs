namespace Game.Core
{
    /// <summary>
    /// 전투 월드 루트 아래 유닛 레이어 이름(Docs/Refactor/2026-10-26_씬전용.md SE-3). 레이어는 Canvas 밖 일반 Transform이라 UIElementMarker가 아니라
    /// 이름으로 찾는다 - 런타임 조회와 인스톨러가 같은 상수를 써야 이름을 바꿔도 조회가 어긋나지 않는다.
    /// </summary>
    public static class BattleWorldLayerNames
    {
        public const string Ally = "AllyLayer";
        public const string Enemy = "EnemyLayer";
    }
}
