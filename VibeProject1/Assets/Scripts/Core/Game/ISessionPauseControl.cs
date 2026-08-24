namespace Game.Core
{
    /// <summary>
    /// 상행 진행 정지만 필요한 소비자(EncounterManager)를 위한 좁은 인터페이스.
    /// ISessionState가 이 인터페이스를 상속한다(Docs/설계/04_Field씬_아키텍처.md 3절, ISP).
    /// </summary>
    public interface ISessionPauseControl
    {
        void Pause();
    }
}
