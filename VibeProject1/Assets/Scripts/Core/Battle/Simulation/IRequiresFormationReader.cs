namespace Game.Core
{
    /// <summary>
    /// IFormationReader가 필요한 IBattleResultRule 구현체만 선택적으로 구현하는 마커 인터페이스.
    /// BattleManager가 이 인터페이스 구현 여부만 확인해 주입하므로, 새 규칙이 추가/교체돼도
    /// BattleManager는 다시 바뀌지 않는다(OCP, Docs/설계/06_전투_핵심루프_아키텍처.md §7.1).
    /// </summary>
    public interface IRequiresFormationReader
    {
        void SetFormationReader(IFormationReader reader);
    }
}
