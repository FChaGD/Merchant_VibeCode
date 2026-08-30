namespace Game.Core
{
    /// <summary>
    /// Docs/기획/08_전투_해석로직_기획.md §13.1 - 세력(파벌)이 아니라 순수 전투 아키타입 분류.
    /// MercenaryClass와 같은 성격(스탯을 가르는 종류 태그).
    /// </summary>
    public enum EnemyType
    {
        Marauder,   // 약탈자
        Monster,    // 괴수
        Adversary,  // 적대자
    }
}
