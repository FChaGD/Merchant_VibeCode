namespace Game.Core
{
    /// <summary>기획 08번 문서 §7 테스트 수치. 플레이테스트 후 조정 대상.</summary>
    public static class MoraleTuning
    {
        public const float Initial = 100f;
        public const float SyncRatePerSecond = 10f;
        public const float PartyMoraleLossOnUnitLost = 20f;
        public const float FleeThreshold = 0f;

        // 도주 이탈 거리("화면 밖으로 이동해 사라진다", §7.3)는 더 이상 고정값이 아니다 - 대형
        // 크기(스폰 반지름)에 연동되도록 BattleFieldLayout.ComputeFleeTravelDistance로 옮겼다.
    }
}
