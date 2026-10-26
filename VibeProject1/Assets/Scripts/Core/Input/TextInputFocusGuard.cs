using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 현재 선택된 UI가 포커스된 텍스트 입력 칸인지 확인한다. 입력 칸이 생기기 전에도 판정 지점을 먼저 둬
    /// 문자 키 단축키(I/B/C/P, Q)가 타이핑과 겹치지 않게 한다(Docs/설계/99번 §4.1).
    /// </summary>
    public sealed class TextInputFocusGuard : MonoBehaviour, ITextInputFocusGuard
    {
        public bool IsTextInputFocused
        {
            get
            {
                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                if (selected == null)
                    return false;
                if (selected.TryGetComponent<TMP_InputField>(out var tmpField) && tmpField.isFocused)
                    return true;
                return selected.TryGetComponent<UnityEngine.UI.InputField>(out var legacyField) && legacyField.isFocused;
            }
        }
    }
}
