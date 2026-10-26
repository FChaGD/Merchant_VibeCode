namespace Game.Core
{
    public interface IPlayerCurrencyHudController
    {
        /// <param name="roster">보유 현황(마차·용병/시설 수) 출처. 없으면 수 칸에 "-"를 표시한다(설계 101번 §4.3).</param>
        void RegisterCurrencyUI(SceneUIRoot sceneUIRoot, IGoldHoldingsReader goldHoldings, IPanelNavigator uiManager, ICaravanRosterProvider roster);
    }
}
