namespace Game.Core
{
    /// <summary>
    /// 배치(FormationLayout)를 불러오고 현재 상행에 적용한다. "저장(게임 세이브)"이 아니라
    /// "현재 상행에 적용"하는 개념이다.
    /// </summary>
    public interface IFormationRepository
    {
        bool TryLoadCurrent(out FormationLayout layout);
        void Apply(FormationLayout layout);
    }
}
