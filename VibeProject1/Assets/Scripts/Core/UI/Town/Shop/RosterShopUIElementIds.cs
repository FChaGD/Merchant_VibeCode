namespace Game.Core
{
    /// <summary>
    /// 목록형 구매 화면(용병단 접촉·마구간, Docs/설계/56번 §7.1)의 UIElementMarker.Id. 두 화면이 요소 구성을 공유하므로 화면별
    /// 접두사 + 요소 이름으로 조립한다(인벤토리 편집 본문의 접두사 방식과 같음). 인스톨러(RosterShopUIBuilder)와 런타임 바인더
    /// (RosterShopElements)가 공유한다.
    /// </summary>
    public static class RosterShopUIElementIds
    {
        public const string MercenaryContactPrefix = "MercenaryContact";
        public const string StablePrefix = "Stable";

        public const string Root = "Root";
        public const string ExitButton = "ExitButton";
        public const string OwnedListLabel = "OwnedListLabel";
        public const string CandidateViewport = "CandidateViewport";
        public const string CandidateContent = "CandidateContent";
        public const string CandidateRowTemplate = "CandidateRowTemplate";
        public const string EmptyCandidateLabel = "EmptyCandidateLabel";
        public const string InfoName = "InfoName";
        public const string InfoKind = "InfoKind";
        public const string InfoDetail = "InfoDetail";
        public const string InfoPrice = "InfoPrice";
        public const string ReasonLabel = "ReasonLabel";
        public const string ActionButton = "ActionButton";

        public static string Of(string prefix, string element) => $"{prefix}.{element}";
    }
}
