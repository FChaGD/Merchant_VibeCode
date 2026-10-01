namespace Game.Core
{
    /// <summary>
    /// 기획 08번 문서 §4/§9는 Wagon/Facility가 "HP 스탯만 가진다"고만 정의했고 구체 수치는 없다 -
    /// Character 평균 체력(70~150)보다 튼튼하게 잡아 사소한 교전 한 번으로 즉시 패배로 이어지지
    /// 않도록 새로 제안하는 테스트 값. 기획 확정 전 확인 필요.
    /// </summary>
    public static class ProtectedUnitTuning
    {
        public const float MaxHp = 200f;
        // 전투 화면 크기(월드 유닛) - 뷰 표시와 장애물 회피 반경(ObstacleAvoidanceTuning)이 같은 값에서 나온다(설계 72번 §3.1).
        // 개체별 크기 데이터가 생기면 BattleProtectedUnit 생성 시점에 그 값을 넣는다.
        public const float BodySize = 0.7f;
    }
}
