namespace Game.Core
{
    /// <summary>
    /// 마차 피격 화물 손실 수치(Docs/기획/77번 §4, 78번 §4-29, 설계 79번 §12). 값 6개뿐이라 엑셀 테이블·임포터를 두지 않고
    /// ProtectedUnitTuning과 같은 정적 수치 클래스로 둔다(설계 79번 §15-10).
    /// </summary>
    public static class CargoLossTuning
    {
        // 적재 물품이 있는 마차가 맞을 때마다 1회 굴림 - [0, 파손) 파손, [파손, 파손+도난) 도난, 나머지 없음.
        public const float HitBreakChance = 0.10f;
        public const float HitStealChance = 0.05f;
        // 파괴 시 남은 물품마다 독립 굴림 - 파손이 아니면 전부 무방비(잔해에 놓임). 파괴 순간엔 도난이 없다.
        public const float DestroyBreakChance = 0.30f;
        // 잔해 공격 1회당 무방비 물품 1개를 공격자가 가져갈 확률.
        public const float WreckStealChance = 0.25f;
        // 유닛 전멸 패배(도주·궤주 공통) 시 살아남은 마차의 적재 물품마다 독립 손실 확률 - 전투 뒤 정산이 쓴다.
        public const float UnitWipeLossChance = 0.30f;
        // 파괴 마차 1대당 현재 구간 상행 시간 추가(초) - 승리·도주에만 적용(정산이 판정).
        public const float SecondsPerDestroyedWagon = 20f;
    }
}
