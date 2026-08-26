namespace Game.Core
{
    public interface IBattleController
    {
        void StartBattle();

        /// <summary>
        /// 화면이 완전히 드러난 뒤 시뮬레이션 틱을 재개한다 - 규칙이 IPausableBattleSimulation을
        /// 구현하지 않으면(예: PlaceholderBattleResultRule) 아무 일도 하지 않는다.
        /// </summary>
        void ResumeSimulation();
    }
}
