using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 배치 UI에서 적용한 FormationLayout을 현재 플레이 세션 동안 보관한다. 상행 종료나 앱 재시작과
    /// 함께 사라지는 인메모리 저장소다 - "게임 세이브"가 아니라 "현재 상행에 적용"이라는 개념에 맞춘 것이다.
    /// </summary>
    public class InMemoryFormationRepository : MonoBehaviour, IFormationRepository, IManagedComponent
    {
        private FormationLayout appliedLayout;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IFormationRepository>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // 다른 매니저에 대한 의존성이 없다.
        }

        public bool TryLoadCurrent(out FormationLayout layout)
        {
            layout = appliedLayout;
            return appliedLayout != null;
        }

        public void Apply(FormationLayout layout)
        {
            appliedLayout = layout;
        }
    }
}
