namespace Game.Core
{
    /// <summary>
    /// 대열 영역을 여는 유닛(마차·시설)의 계약(Docs/기획/59번 §3.1). 방향별 범위는 개체 단위 테이블(Wagon/Facility.xlsx
    /// Up/Down/Left/Right 열, 기획 61번)에서 온다. 캐릭터는 영역을 열지 않으므로 구현하지 않는다(ISP).
    /// </summary>
    public interface IAreaAnchorUnit : IFormationUnit
    {
        FormationAreaSpan AreaSpan { get; }
    }
}
