namespace Game.Core
{
    /// <summary>
    /// ITacticsReader가 필요한 IBattleResultRule 구현체만 선택적으로 구현하는 마커 인터페이스 -
    /// IRequiresFormationReader와 같은 패턴(OCP, Docs/설계/12번 §5.3).
    /// </summary>
    public interface IRequiresTacticsReader
    {
        void SetTacticsReader(ITacticsReader reader);
    }
}
