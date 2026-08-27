namespace Game.Core
{
    /// <summary>
    /// 역할군 축 - 활동 반경 안에서 전투 중 실제로 이동하는 세부 로직(Docs/기획/12번 §3.2).
    /// </summary>
    public enum LocalPositioning
    {
        // 전열 - 적에게 직진 접근(기존 기본 동작).
        Charge,
        // 전열 - 아군과 적 사이에 위치해 전열 형성.
        Blocking,
        // 원거리딜러 - 항상 최대 사거리를 유지하도록 위치 조정.
        MaintainRange,
        // 원거리딜러 - 제자리에서 사격.
        Stationary,
    }
}
