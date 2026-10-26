using System;

namespace Game.Core
{
    /// <summary>
    /// 전역 입력 액션 자산과 InputManager 사이의 경계. InputManager는 Unity 입력 API를 직접 다루지 않고
    /// 이 하위 컴포넌트에 연결·활성화만 지시한다(Docs/설계/99번 §4.2).
    /// </summary>
    public interface IGameInputActionSource
    {
        bool HasAction(string actionId);
        void Connect(Action<string> onPerformed);
        void Disconnect();
        void SetEnabled(bool enabled);
        string GetBindingLabel(string actionId);
    }
}
