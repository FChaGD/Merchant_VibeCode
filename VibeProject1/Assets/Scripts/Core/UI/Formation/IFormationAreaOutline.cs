namespace Game.Core
{
    /// <summary>
    /// 정비창 격자 위 대열 영역 외곽선 표시(디버그 전용, 2026-09-29). 마을 배치 패널만 이 계약을 찾아 격자 편집기에 넘긴다 - 구현
    /// (FormationAreaOutlineDebugView)은 에디터 전용 디버그 도구라, 설치하지 않았거나 빌드에서는 없는 것이 정상이다(null 허용).
    /// </summary>
    public interface IFormationAreaOutline
    {
        void Render(FormationArea area, FormationGridView grid);
    }
}
