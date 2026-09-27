namespace Game.Core
{
    /// <summary>
    /// 공용 아이템 테이블(Item.xlsx)의 Category 열(Docs/설계/50번 §3.2). 셀에는 정수 Id를 쓴다(설계 18번 v2 규칙) -
    /// 멤버의 명시적 정수값이 곧 Id이므로 순서를 바꾸거나 값을 재사용하지 않는다.
    /// Misc(기타)는 판매 대상이 아닌 아이템(골드 상자)을 교역품 판매 규칙에서 떼어 내려고 신설했다(기획 49번 §3.4).
    /// </summary>
    public enum ItemCategory
    {
        TradeGoods = 1,
        Equipment = 2,
        Consumable = 3,
        PersonalItem = 4,
        Misc = 5,
    }
}
