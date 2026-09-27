namespace Game.Core
{
    /// <summary>
    /// 교역품 데이터 테이블(TradeGoods.xlsx)의 TradeKind 열(Docs/기획/49번 §3.3). 판매 시설이 이 값으로 정해진다 -
    /// 일반 품목은 무역품 구매, 특산품은 특산물 구매. 셀에는 정수 Id를 쓴다(설계 18번 v2 규칙).
    /// </summary>
    public enum TradeGoodsKind
    {
        General = 1,
        Specialty = 2,
    }
}
