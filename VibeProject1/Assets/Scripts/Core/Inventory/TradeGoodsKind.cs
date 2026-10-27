namespace Game.Core
{
    /// <summary>
    /// 교역품 데이터 테이블(TradeGoods.xlsx)의 TradeKind 열(Docs/기획/49번 §3.3). 판매 시설이 이 값으로 정해진다 -
    /// 일반 품목은 무역품 구매, 특산품은 특산물 구매. 셀에는 정수 Id를 쓴다(설계 18번 v2 규칙).
    /// 의뢰 화물(QuestCargo)은 퀘스트 수락 때만 지급되는 전용 행이다 - 마을 재고에 넣을 수 없다(Docs/설계/107번 §4, TownStock 임포트가 막는다).
    /// </summary>
    public enum TradeGoodsKind
    {
        General = 1,
        Specialty = 2,
        QuestCargo = 3,
    }
}
