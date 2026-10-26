using System;

namespace Game.Core
{
    /// <summary>
    /// 입력 액션 구독 창구. 동작 여부(팝업이 열려 있는지 등)는 구독자가 UI 상태로 판단한다(Docs/설계/99번 §4.3).
    /// </summary>
    public interface IInputManager
    {
        /// <summary>actionId는 GameInputActionIds 상수. 반환 핸들을 Dispose하면 구독이 끝난다.</summary>
        IDisposable Subscribe(string actionId, Action onPerformed);

        /// <summary>버튼에 표시할 키 글자. 액션이 없으면 빈 문자열.</summary>
        string GetBindingLabel(string actionId);
    }
}
