namespace Game.Core
{
    /// <summary>
    /// 직업(MercenaryClass)을 대체하는 게 아니라 그 위에 얹는 방향성 지시용 분류 - 직업은 스탯/아이콘을
    /// 그대로 담당하고, 역할군은 어떤 방향성 지시 후보 집합을 쓸지만 결정한다(Docs/기획/12번 §1).
    /// Supporter/Assassin은 대응 직업이 아직 없어 이름만 존재한다(Docs/설계/12번 §0).
    /// </summary>
    public enum RoleGroup
    {
        Frontline,
        RangedDealer,
        Supporter,
        Assassin,
    }
}
