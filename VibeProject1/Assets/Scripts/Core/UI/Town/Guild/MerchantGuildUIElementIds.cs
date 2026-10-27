namespace Game.Core
{
    /// <summary>
    /// 상인조합 화면(Docs/설계/103번 §5.1, 107번 §11.2)에서 UIElementMarker.Id로 쓰는 문자열. 인스톨러(MerchantGuildUIBuilder)와 런타임 바인더
    /// (MerchantGuildElements)가 공유한다.
    /// </summary>
    public static class MerchantGuildUIElementIds
    {
        public const string Root = "MerchantGuild.Root";
        public const string ExitButton = "MerchantGuild.ExitButton";
        public const string ListViewport = "MerchantGuild.ListViewport";
        public const string ListContent = "MerchantGuild.ListContent";
        public const string RowTemplate = "MerchantGuild.RowTemplate";
        public const string EmptyLabel = "MerchantGuild.EmptyLabel";
        public const string DetailTitle = "MerchantGuild.DetailTitle";
        public const string DetailDescription = "MerchantGuild.DetailDescription";
        public const string DetailObjectives = "MerchantGuild.DetailObjectives";
        public const string DetailDeadline = "MerchantGuild.DetailDeadline";
        public const string DetailReward = "MerchantGuild.DetailReward";
        public const string StatusLabel = "MerchantGuild.StatusLabel";
        public const string AcceptButton = "MerchantGuild.AcceptButton";
        public const string AbandonButton = "MerchantGuild.AbandonButton";
        public const string ReportButton = "MerchantGuild.ReportButton";
        public const string AcceptTooltip = "MerchantGuild.AcceptTooltip";
        public const string AcceptTooltipText = "MerchantGuild.AcceptTooltipText";
        public const string AbandonTooltip = "MerchantGuild.AbandonTooltip";
        public const string AbandonTooltipText = "MerchantGuild.AbandonTooltipText";
        public const string ReportTooltip = "MerchantGuild.ReportTooltip";
        public const string ReportTooltipText = "MerchantGuild.ReportTooltipText";
        public const string ConfirmDialog = "MerchantGuild.ConfirmDialog";
    }
}
