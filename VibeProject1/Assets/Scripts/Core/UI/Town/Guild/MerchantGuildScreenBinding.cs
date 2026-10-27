using UnityEngine;

namespace Game.Core
{
    /// <summary>상인조합 화면 바인딩(설계 103번 §4.5) - 시설 화면 채널. 의존성: 게시판(필수).</summary>
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

            if (!MerchantGuildElements.TryBind(context.SceneUIRoot, out var elements)) return null;

            var panel = new MerchantGuildPanel(elements, board, context.UIManager);
            context.Panels.RegisterScreenPanel(panel);
            return panel;
        }
    }
}
