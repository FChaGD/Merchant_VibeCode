using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public class AIManager : MonoBehaviour, IAIManager, IManagedComponent
    {
        private readonly List<IAIControllable> units = new();

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IAIManager>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // TODO: AIProfileComponent 연결 - 하위 컴포넌트 설계 후 구현
        }

        public void Register(IAIControllable unit)
        {
            if (!units.Contains(unit))
            {
                units.Add(unit);
            }
        }

        public void Unregister(IAIControllable unit)
        {
            units.Remove(unit);
        }
    }
}
