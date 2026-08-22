using System;

namespace Game.Core
{
    public interface ISceneLoader
    {
        /// <summary>
        /// 콘텐츠 씬(본부/상행 등)을 Additive로 전환한다. 매니저가 상주하는 Bootstrap 씬은 건드리지 않는다.
        /// </summary>
        void Transition(string sceneName);

        /// <summary>
        /// 전환 대상 씬의 Additive 로드와 이전 콘텐츠 씬 언로드가 모두 끝난 시점에 발행된다.
        /// </summary>
        event Action<string> OnSceneLoaded;
    }
}
