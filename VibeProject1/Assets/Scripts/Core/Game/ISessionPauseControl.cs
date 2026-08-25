namespace Game.Core
{
    /// <summary>
    /// 상행 진행 정지만 필요한 소비자를 위한 좁은 인터페이스. ISessionState가 이 인터페이스를
    /// 상속한다(Docs/설계/04_Field씬_아키텍처.md 3절, ISP). EncounterManager는 이후 주기 판정에
    /// OnProgressChanged 구독도 필요해져 ISessionState 전체로 의존성을 넓혔다
    /// (Docs/설계/05_인카운터_판정_아키텍처.md §6) - 이 인터페이스 자체는 더 좁은 요구만 있는 다른
    /// 소비자를 위해 그대로 유지한다.
    /// </summary>
    public interface ISessionPauseControl
    {
        void Pause();
    }
}
