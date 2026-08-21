namespace Game.Core
{
    public interface ISceneLoader
    {
        /// <summary>
        /// 콘텐츠 씬(본부/상행 등)을 Additive로 전환한다. 매니저가 상주하는 Bootstrap 씬은 건드리지 않는다.
        /// </summary>
        void Transition(string sceneName);
    }
}
