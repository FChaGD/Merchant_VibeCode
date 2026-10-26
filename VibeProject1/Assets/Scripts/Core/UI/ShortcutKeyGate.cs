using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 키 입력으로 버튼을 대신 누를지 판정한다. Selectable.IsInteractable()이 부모 CanvasGroup을 반영하므로
    /// 모달·시설 화면(LayerHideGate가 CanvasGroup을 끔) 동안 자동으로 막힌다 — 입력 매니저가 화면 상태를
    /// 따로 보유하지 않는다(Docs/설계/99번 §5.1).
    /// </summary>
    public static class ShortcutKeyGate
    {
        public static bool ShouldInvoke(Selectable button)
        {
            return button != null && button.IsActive() && button.IsInteractable();
        }
    }
}
