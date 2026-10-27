namespace Game.Core
{
    /// <summary>
    /// n일 휴식 시작(Docs/Refactor/2026-10-26_씬전용.md E-1, SS-4) - 숙소 화면은 휴식 진행 화면의 구체 클래스가 아니라 이 계약만 받는다.
    /// 구현체(InnRestProgressPanel)가 시작 시점 값을 고정하고 같은 시설 화면 채널에서 진행 화면을 연다.
    /// </summary>
    public interface IInnRestStarter
    {
        void Begin(int days);
    }
}
