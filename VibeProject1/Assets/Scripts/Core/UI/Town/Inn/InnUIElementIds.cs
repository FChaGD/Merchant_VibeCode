namespace Game.Core
{
    /// <summary>
    /// 숙소·휴식 진행 화면(Docs/설계/103번 §5.1)에서 UIElementMarker.Id로 쓰는 문자열. 인스톨러(InnUIBuilder)와 런타임 바인더
    /// (InnElements/InnRestProgressElements)가 공유한다. 탭 본문은 인벤토리 편집 본문이라 InventoryPopupUIElementIds에 BodyPrefix를 붙여 찾는다.
    /// </summary>
    public static class InnUIElementIds
    {
        public const string Root = "Inn.Root";
        public const string ExitButton = "Inn.ExitButton";
        public const string DaysDownButton = "Inn.DaysDownButton";
        public const string DaysUpButton = "Inn.DaysUpButton";
        public const string DaysLabel = "Inn.DaysLabel";
        public const string CostPerDayLabel = "Inn.CostPerDayLabel";
        public const string TotalCostLabel = "Inn.TotalCostLabel";
        public const string RestButton = "Inn.RestButton";
        public const string RestTooltip = "Inn.RestTooltip";
        public const string RestTooltipText = "Inn.RestTooltipText";
        // 탭은 인벤토리 팝업 Id(InventoryPopupIds)로 구분한다.
        public static string TabButton(string popupId) => $"Inn.Tab.{popupId}";
        public static string BodyPrefix(string popupId) => $"Inn.{popupId}";

        public const string ProgressRoot = "InnRestProgress.Root";
        public const string ProgressGaugeFill = "InnRestProgress.GaugeFill";
        public const string ProgressDays = "InnRestProgress.Days";
        public const string ProgressDate = "InnRestProgress.Date";
        public const string ProgressCost = "InnRestProgress.Cost";
        public const string ProgressGold = "InnRestProgress.Gold";
        public const string ProgressEventArea = "InnRestProgress.EventArea";
        public const string ProgressRecoveryArea = "InnRestProgress.RecoveryArea";
        public const string ProgressResult = "InnRestProgress.Result";
        public const string ProgressStopButton = "InnRestProgress.StopButton";
        public const string ProgressConfirmButton = "InnRestProgress.ConfirmButton";
    }
}
