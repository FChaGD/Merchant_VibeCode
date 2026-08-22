namespace Game.Core
{
    /// <summary>
    /// 배치(FormationLayout) 조회만 필요한 소비자를 위한 읽기 전용 계약. 상행 준비 UI의 편성 요약처럼
    /// 배치를 변경할 필요가 없는 곳은 이 인터페이스만 의존해 IFormationRepository.Apply에 대한
    /// 접근 권한을 아예 갖지 않도록 한다.
    /// </summary>
    public interface IFormationReader
    {
        bool TryLoadCurrent(out FormationLayout layout);
    }
}
