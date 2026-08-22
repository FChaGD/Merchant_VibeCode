namespace Game.Core
{
    /// <summary>
    /// 확정된 배치(FormationLayout)를 불러오고 저장한다.
    /// 실제 구현은 상행 관리 데이터 시스템 설계 후 연결한다.
    /// </summary>
    public interface IFormationRepository
    {
        bool TryLoadCurrent(out FormationLayout layout);
        void Save(FormationLayout layout);
    }
}
