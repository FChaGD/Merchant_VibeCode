namespace Game.Core
{
    public interface ITacticsPanel : IUIPanel
    {
        /// <summary>
        /// FormationPanel/RegisterFormationUI와 같은 패턴 - 콘텐츠 씬(Hub/Field)이 로드될 때마다
        /// 다시 호출되어 그 씬의 화면 요소로 재바인딩된다. 역할군 후보+라벨(RoleGroupTacticsCatalogAsset)은
        /// 매개변수로 받지 않고 TacticsPanel 자신의 [SerializeField]로 직접 들고 있다 -
        /// InMemoryTacticsRepository/LiveBattleSimulationRule과 같은 자리(에디터에서 같은 에셋 파일을
        /// 각자 참조).
        /// </summary>
        void RegisterTacticsUI(ITacticsRepository repository, IUIManager uiManager, string sceneName);
    }
}
