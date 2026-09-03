namespace Game.Core
{
    /// <summary>
    /// 파티 전체 축 - 활동 반경(ActivityRadiusPreset)을 벗어난 상태에서 배치 위치로 복귀하거나
    /// 재타겟하는 경향성. 반경을 벗어나는 것 자체는 이 축과 무관한 전제다(Docs/기획/12번 §2.3).
    /// 명시적 정수값 = 데이터 테이블 Id(Docs/설계/18번 §3), 0은 비워둔다.
    /// </summary>
    public enum PursuitPreset
    {
        // 활동 반경 자체를 무시 - 항상 자유 행동(단, 적 인식에는 영향 없음, Docs/설계/12번 §6-2).
        Autonomous = 1,
        // 타겟이 죽거나 도주할 때까지 무제한 추적.
        HuntToKill = 2,
        // 반경 밖에서 5초 이상 공격을 못 맞히면 복귀·재타겟(기본값).
        OffensiveJudgment = 3,
        // 반경 밖에 3초 이상 머물면(명중 여부 무관) 복귀·재타겟.
        NoPursuit = 4,
        // 활동 반경을 아예 벗어나지 않음(가장 수비적).
        HoldPosition = 5,
    }
}
