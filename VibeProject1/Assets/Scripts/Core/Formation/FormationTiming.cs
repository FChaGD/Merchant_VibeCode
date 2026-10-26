namespace Game.Core
{
    /// <summary>
    /// Field 정비창 배치/이동 소요시간 테스트 수치를 한 곳에 모은다(TacticsTuning/MoraleTuning과 같은
    /// 자리, Docs/기획/20번 §3.2/§3.3, 설계 25번 §9-12). 전부 플레이테스트 후 조정 대상.
    /// </summary>
    public static class FormationTiming
    {
        // 유닛 추가(§3.2) - 캐릭터는 짧게, 마차/시설은 훨씬 길게.
        public const float CharacterAddSeconds = 3f;
        public const float WagonAddSeconds = 10f;
        public const float FacilityAddSeconds = 10f;

        // 유닛 이동(§3.3) - 경로 슬롯 1칸당 이동에 걸리는 시간. 최소 한 칸 분량은 보장한다(같은 슬롯
        // 재드롭은 FormationGridEditor가 이미 걸러내므로 경로 길이가 0이 되는 경우는 없다).
        public const float MoveSecondsPerSlot = 1f;
    }
}
