namespace Game.Core
{
    /// <summary>
    /// 값이 없는 칸에 쓰는 자리표시자 문구(CLAUDE.md Placeholder 컨벤션 - 창작하지 않는다). 클래스마다 같은 문구를 따로 선언하던 것을
    /// 한곳으로 모았다(Docs/Refactor/2026-10-26_공통.md D-5) - 문구를 바꾸면 이 상수만 고친다.
    /// </summary>
    public static class PlaceholderText
    {
        public const string Missing = "값 없음";
    }
}
