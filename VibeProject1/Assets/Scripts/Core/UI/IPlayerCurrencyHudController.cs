namespace Game.Core
{
    public interface IPlayerCurrencyHudController
    {
        void RegisterCurrencyUI(SceneUIRoot sceneUIRoot, IPlayerCurrencyReader currencyReader);
    }
}
