namespace Game.Core
{
    /// <summary>
    /// Docs/기획/08-2026-09-01-전투_해석로직_기획.md §13.1 - 세력(파벌)이 아니라 순수 전투 아키타입 분류.
    /// MercenaryClass와 같은 성격(스탯을 가르는 종류 태그).
    /// 명시적 정수값 = 데이터 테이블 Id(Docs/설계/18번 §3), 0은 비워둔다.
    /// </summary>
    public enum EnemyType
    {
        Marauder = 1,   // 약탈자
        Monster = 2,    // 괴수
        Adversary = 3,  // 적대자
    }
}
