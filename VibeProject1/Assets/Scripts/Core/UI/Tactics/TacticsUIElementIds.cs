namespace Game.Core
{
    /// <summary>
    /// 방향성 지시 UI에서 UIElementMarker.Id로 사용하는 문자열 상수(Docs/설계/11번 §5.1).
    /// </summary>
    public static class TacticsUIElementIds
    {
        public const string PanelRoot = "Tactics.PanelRoot";
        public const string CloseButton = "Tactics.CloseButton";

        public const string TabParty = "Tactics.TabParty";
        public const string TabRoleGroup = "Tactics.TabRoleGroup";
        public const string TabIndividual = "Tactics.TabIndividual";

        public const string PartyContentRoot = "Tactics.PartyContentRoot";
        public const string RoleGroupContentRoot = "Tactics.RoleGroupContentRoot";
        public const string IndividualContentRoot = "Tactics.IndividualContentRoot";

        public const string RecognitionDropdown = "Tactics.RecognitionDropdown";
        public const string RadiusDropdown = "Tactics.RadiusDropdown";
        public const string PursuitDropdown = "Tactics.PursuitDropdown";

        public const string FrontlineOverrideToggle = "Tactics.FrontlineOverrideToggle";
        public const string FrontlineTargetDropdown = "Tactics.FrontlineTargetDropdown";
        public const string FrontlinePositioningDropdown = "Tactics.FrontlinePositioningDropdown";
        public const string FrontlineSelfPreservationDropdown = "Tactics.FrontlineSelfPreservationDropdown";

        public const string RangedOverrideToggle = "Tactics.RangedOverrideToggle";
        public const string RangedTargetDropdown = "Tactics.RangedTargetDropdown";
        public const string RangedPositioningDropdown = "Tactics.RangedPositioningDropdown";
        public const string RangedSelfPreservationDropdown = "Tactics.RangedSelfPreservationDropdown";
    }
}
