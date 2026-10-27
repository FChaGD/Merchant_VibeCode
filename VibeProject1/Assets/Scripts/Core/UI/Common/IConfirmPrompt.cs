using System;

namespace Game.Core
{
    /// <summary>
    /// "메시지 + 확인/취소" 확인 창 계약(Docs/Refactor/2026-10-26_씬전용.md D-1). 출발 절차 같은 흐름이 화면 컴포넌트(ConfirmDialogView) 없이
    /// 테스트되도록 뺐다 - 콜백보다 창을 먼저 닫는 규칙은 구현체(ConfirmDialogView)가 지킨다.
    /// </summary>
    public interface IConfirmPrompt
    {
        void Show(string message, string confirmText, string cancelText, Action onConfirm, Action onCancel);
    }
}
