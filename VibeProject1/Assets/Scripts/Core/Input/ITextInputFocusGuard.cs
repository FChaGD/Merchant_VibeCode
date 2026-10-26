namespace Game.Core
{
    /// <summary>텍스트 입력 칸에 포커스가 있는지. 문자 키 단축키를 막는 근거(Docs/설계/99번 §4.1).</summary>
    public interface ITextInputFocusGuard
    {
        bool IsTextInputFocused { get; }
    }
}
