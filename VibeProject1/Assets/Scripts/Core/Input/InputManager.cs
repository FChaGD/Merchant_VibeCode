using UnityEngine;

namespace Game.Core
{
    public class InputManager : MonoBehaviour, IInputManager, IManagedComponent
    {
        private IUIManager uiManager;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IInputManager>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            uiManager = registrar.Resolve<IUIManager>();
        }

        public void SetContext(string contextId)
        {
            // TODO: IInputContextHandler(필드/정비창/전투 등) 라우팅 - 하위 컴포넌트 설계 후 구현
        }
    }
}
