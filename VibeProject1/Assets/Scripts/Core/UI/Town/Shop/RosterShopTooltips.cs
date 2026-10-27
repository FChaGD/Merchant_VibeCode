namespace Game.Core
{
    /// <summary>목록형 구매 화면(용병·마구간 공용)의 행동 버튼 사유 툴팁 - 요소 Id는 화면 접두사로 구분한다.</summary>
    internal static class RosterShopTooltips
    {
        public static ITooltipHandle Register(HubScreenContext context, RosterShopElements elements, string prefix)
            => ButtonTooltip.TryRegister(context.Tooltips, elements.ActionButton, context.SceneUIRoot,
                RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.ActionTooltip), RosterShopUIElementIds.Of(prefix, RosterShopUIElementIds.ActionTooltipText));
    }
}
