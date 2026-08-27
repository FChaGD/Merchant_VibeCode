namespace Game.Core
{
    /// <summary>
    /// 역할군 축 - 사기(도주) 시스템과 무관한, 유닛 생존성 관련 이동(Docs/기획/12번 §3.3).
    /// </summary>
    public enum SelfPreservation
    {
        // 전열 - 후퇴 없이 버팀.
        Resilient,
        // 전열 - 중상 시 일시 후퇴, 전투당 1회만 발동.
        FallBackOnHeavyDamage,
        // 원거리딜러 - 적이 근접하면 유지사격하며 후퇴.
        Kiting,
        // 원거리딜러 - 피격 시 일정 거리 후퇴 후 공격 재개.
        RetreatOnHit,
    }
}
