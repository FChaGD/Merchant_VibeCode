using System;

namespace Game.Core
{
    public class DependencyNotRegisteredException : Exception
    {
        public DependencyNotRegisteredException(Type dependencyType)
            : base($"{dependencyType.FullName}이(가) DependencyManager에 등록되어 있지 않다. RegisterSelf 단계에서 등록이 누락되었는지 확인하라.")
        {
        }
    }
}
