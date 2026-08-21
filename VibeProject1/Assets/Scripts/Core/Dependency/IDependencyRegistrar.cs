namespace Game.Core
{
    public interface IDependencyRegistrar
    {
        void Register<T>(T instance) where T : class;

        /// <summary>
        /// 필수 의존성 조회. 등록되어 있지 않으면 DependencyNotRegisteredException을 던진다(fail-fast).
        /// </summary>
        T Resolve<T>() where T : class;

        /// <summary>
        /// 선택적 의존성 조회. 등록되어 있지 않아도 예외 없이 false를 반환한다.
        /// </summary>
        bool TryResolve<T>(out T instance) where T : class;
    }
}
