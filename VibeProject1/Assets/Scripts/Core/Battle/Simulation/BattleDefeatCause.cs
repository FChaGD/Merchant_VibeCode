namespace Game.Core
{
    /// <summary>
    /// 패배 원인(설계 79번 §4.1). 승패만으로는 전투 뒤 정산을 정할 수 없다 - 유닛 전멸 패배만 살아남은 마차 화물을 추가로
    /// 잃는다(기획 78번 §4-29). 승리면 None.
    /// </summary>
    public enum BattleDefeatCause
    {
        None,
        // 조건 1 - 모든 마차 파괴. 두 조건이 동시에 성립해도 이쪽이다(기획 78번 §4-27).
        AllWagonsDestroyed,
        // 조건 2 - 전투 가능 아군(캐릭터+시설) 없음.
        NoCombatants
    }
}
