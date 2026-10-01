namespace Game.Core
{
    /// <summary>
    /// 전투 패배 판정(Docs/기획/73번 §3, 설계 74번 §2.2). 루프 밖 순수 함수로 둔 이유 - 루프는 협력 객체가 많아 테스트
    /// 구성이 크지만, 판정 자체는 생존 수 네 개만으로 결정된다.
    /// </summary>
    public static class BattleDefeatRule
    {
        public static bool IsDefeated(int totalWagons, int aliveWagons, int aliveCharacters, int aliveFacilities)
        {
            // 조건 1 - 모든 마차 파괴. 마차 0대 전투는 시작부터 참이 되므로 적용하지 않는다(기획 73번 §4-1).
            var allWagonsDestroyed = totalWagons > 0 && aliveWagons == 0;
            // 조건 2 - 전투 가능 아군 없음. 캐릭터(사망·도주 제외)와 시설(파괴 제외)을 함께 센다.
            var noFightingAllies = aliveCharacters == 0 && aliveFacilities == 0;
            return allWagonsDestroyed || noFightingAllies;
        }
    }
}
