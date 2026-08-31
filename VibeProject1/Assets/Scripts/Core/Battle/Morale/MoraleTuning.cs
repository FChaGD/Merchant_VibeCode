namespace Game.Core
{
    /// <summary>기획 08번 문서 §7 테스트 수치(2026-09-01 리뉴얼, 설계 14번). 플레이테스트 후 조정 대상.</summary>
    public static class MoraleTuning
    {
        public const float Initial = 60f; // PartyMorale/UnitMorale/개인 목표치 공통 초기값

        // §7.1 PartyMorale 트리거
        public const float DeathLoss = 10f;
        public const float FleeLoss = 15f;
        public const float EnemyKilledGain = 4f;

        // §7.2/§7.5 3단계 임계치 - PartyMorale/UnitMorale이 MoraleTierExtensions로 공유(사용자 확정).
        public const float HighTierThreshold = 70f;
        public const float LowTierThreshold = 30f;
        public const float LowTierAmplifier = 1.5f; // PartyMorale 저사기 자기증폭 배율

        // §7.3 파동 팽창 속도 = min(WaveSpeedCap, WaveSpeedPerDeltaUnit * |delta| / 10)
        public const float WaveSpeedPerDeltaUnit = 2f;
        public const float WaveSpeedCap = 15f;

        // §7.5 UnitMorale 고사기 버프 / 저사기 동기화 가속
        public const float HighTierDefenseMultiplier = 1.2f;
        public const float HighTierMoveSpeedMultiplier = 1.5f;
        public const float LowTierSyncMultiplier = 2f;

        // §7.6 붕괴(도주) 확률제 - probability(%) = FleeProbabilityFloor +
        //   (100 - FleeProbabilityFloor) * ((FleeCandidateThreshold - UnitMorale) / FleeCandidateThreshold) ^ FleeProbabilityExponent
        public const float FleeCandidateThreshold = 10f;
        public const float FleeProbabilityFloor = 2f;
        public const float FleeProbabilityExponent = 4f;

        // 도주 이탈 거리("화면 밖으로 이동해 사라진다", §7.6)는 고정값이 아니다 - 대형 크기(스폰
        // 반지름)에 연동되도록 BattleFieldLayout.ComputeFleeTravelDistance로 옮겼다.
    }
}
