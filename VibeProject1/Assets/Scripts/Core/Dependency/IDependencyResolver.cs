namespace Game.Core
{
    /// <summary>
    /// 의존성 조회 전용 인터페이스. 등록(IDependencyRegistrar)과 분리해 두어, 등록 단계에서 조회하거나
    /// 해석 단계에서 등록하는 코드가 컴파일 단계에서 막히도록 한다(2단계 초기화 규약을 타입으로 강제).
    /// </summary>
    public interface IDependencyResolver
    {
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
