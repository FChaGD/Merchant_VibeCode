using UnityEngine;

namespace Game.Core
{
    public class UIManager : MonoBehaviour, IUIManager, IManagedComponent
    {
        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IUIManager>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // TODO: IUIPanel 구현체(FormationPanel, TacticsPanel, HUDPanel, ResultPanel 등) 연결 - 하위 컴포넌트 설계 후 구현
        }

        public void Open(string panelId)
        {
            // TODO: 패널 오픈 로직 - 하위 컴포넌트 설계 후 구현
        }

        public void Close(string panelId)
        {
            // TODO: 패널 클로즈 로직 - 하위 컴포넌트 설계 후 구현
        }
    }
}
