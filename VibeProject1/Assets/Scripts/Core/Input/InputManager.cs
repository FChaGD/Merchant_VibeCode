using UnityEngine;

namespace Game.Core
{
    public class InputManager : MonoBehaviour, IInputManager, IManagedComponent
    {
        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IInputManager>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // TODO: IInputContextHandler 라우팅 구현 시 필요한 의존성(IUIManager 등)을 여기서 조회한다.
        }

        public void SetContext(string contextId)
        {
            // TODO: IInputContextHandler(필드/정비창/전투 등) 라우팅 - 하위 컴포넌트 설계 후 구현
        }
    }
}
