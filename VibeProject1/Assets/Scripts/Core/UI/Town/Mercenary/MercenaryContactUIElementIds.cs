namespace Game.Core
{
    /// <summary>
    /// 용병단 접촉 화면(Docs/설계/54번 §8)에서 UIElementMarker.Id로 쓰는 문자열. 인스톨러(MercenaryContactUIBuilder)와
    /// 런타임 바인더(MercenaryContactElements)가 공유한다.
    /// </summary>
    public static class MercenaryContactUIElementIds
    {
        public const string Root = "MercenaryContact.Root";
        public const string ExitButton = "MercenaryContact.ExitButton";
        public const string OwnedListLabel = "MercenaryContact.OwnedListLabel";
        public const string CandidateViewport = "MercenaryContact.CandidateViewport";
        public const string CandidateContent = "MercenaryContact.CandidateContent";
        public const string CandidateRowTemplate = "MercenaryContact.CandidateRowTemplate";
        public const string EmptyCandidateLabel = "MercenaryContact.EmptyCandidateLabel";
        public const string InfoName = "MercenaryContact.InfoName";
        public const string InfoClass = "MercenaryContact.InfoClass";
        public const string InfoStats = "MercenaryContact.InfoStats";
        public const string InfoCost = "MercenaryContact.InfoCost";
        public const string ReasonLabel = "MercenaryContact.ReasonLabel";
        public const string HireButton = "MercenaryContact.HireButton";
    }
}
