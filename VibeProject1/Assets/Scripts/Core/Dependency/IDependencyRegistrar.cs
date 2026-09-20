namespace Game.Core
{
    /// <summary>
    /// 의존성 등록 전용 인터페이스. 조회는 IDependencyResolver가 담당한다 - 등록 단계 콜백에는 이 인터페이스만,
    /// 해석 단계 콜백에는 IDependencyResolver만 전달되므로 단계 규약 위반이 컴파일 오류로 드러난다.
    /// </summary>
    public interface IDependencyRegistrar
    {
        void Register<T>(T instance) where T : class;
    }
}
