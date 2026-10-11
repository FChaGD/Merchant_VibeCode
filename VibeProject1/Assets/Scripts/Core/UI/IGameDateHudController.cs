namespace Game.Core
{
    public interface IGameDateHudController
    {
        void RegisterDateUI(SceneUIRoot sceneUIRoot, IDateReader dateReader);
    }
}
