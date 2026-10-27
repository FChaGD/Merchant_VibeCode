using UnityEngine;

namespace Game.Core
{
    /// <summary>상인조합 화면 바인딩(설계 103번 §4.5·107번 §11.2) - 시설 화면 채널. 의존성: 게시판(필수). 버튼 사유 툴팁은 없어도 동작한다.</summary>
    internal sealed class MerchantGuildScreenBinding : HubScreenBinding<MerchantGuildPanel>
    {
        protected override MerchantGuildPanel Create(HubScreenContext context)
        {
            var board = context.Optional<IGuildQuestBoard>();
            if (board == null)
            {
                Debug.LogWarning($"상인조합 화면에 필요한 {nameof(IGuildQuestBoard)}가 연결되어 있지 않아 등록하지 못했다(Tools > Game > Build Bootstrap Scene).");
                return null;
            }

            var sceneUIRoot = context.SceneUIRoot;
            if (!MerchantGuildElements.TryBind(sceneUIRoot, out var elements)) return null;

            var acceptTooltip = ButtonTooltip.TryRegister(context.Tooltips, elements.AcceptButton, sceneUIRoot, MerchantGuildUIElementIds.AcceptTooltip, MerchantGuildUIElementIds.AcceptTooltipText);
            var abandonTooltip = ButtonTooltip.TryRegister(context.Tooltips, elements.AbandonButton, sceneUIRoot, MerchantGuildUIElementIds.AbandonTooltip, MerchantGuildUIElementIds.AbandonTooltipText);
            var reportTooltip = ButtonTooltip.TryRegister(context.Tooltips, elements.ReportButton, sceneUIRoot, MerchantGuildUIElementIds.ReportTooltip, MerchantGuildUIElementIds.ReportTooltipText);

            var panel = new MerchantGuildPanel(elements, board, context.UIManager, acceptTooltip, abandonTooltip, reportTooltip);
            context.Panels.RegisterScreenPanel(panel);
            return panel;
        }
    }
}
