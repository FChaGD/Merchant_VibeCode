using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 버튼 하나에 비활성 사유 툴팁을 등록하는 공용 절차(Docs/Refactor/2026-10-26_공통.md D-1). 버튼의 중계 2개(호버·길게 누름)와 툴팁 루트·문구를
    /// 찾아 공용 툴팁 컨트롤러에 묶는다. 하나라도 없으면(인스톨러 미실행) 경고만 내고 null - 버튼 활성 판정은 그대로 동작하고 사유만 안 보인다.
    /// </summary>
    public static class ButtonTooltip
    {
        public static ITooltipHandle TryRegister(ITooltipController tooltips, Selectable button, SceneUIRoot sceneUIRoot, string rootId, string textId)
        {
            if (tooltips == null || button == null) return null;

            if (!button.TryGetComponent<PointerHoverRelay>(out var hover) || !button.TryGetComponent<PointerPressRelay>(out var press)
                || !sceneUIRoot.TryGetElement<RectTransform>(rootId, out var root) || !sceneUIRoot.TryGetElement<TMP_Text>(textId, out var text))
            {
                Debug.LogWarning($"'{button.name}' 버튼의 사유 툴팁 요소('{rootId}')를 찾을 수 없어 사유를 표시하지 않는다(Tools > Game > Build Hub Scene).");
                return null;
            }
            return tooltips.Register(hover, press, root.gameObject, text);
        }
    }
}
