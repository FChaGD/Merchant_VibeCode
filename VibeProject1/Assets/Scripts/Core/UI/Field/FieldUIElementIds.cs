namespace Game.Core
{
    /// <summary>
    /// Field 씬에서 UIElementMarker.Id로 사용하는 문자열 상수.
    /// </summary>
    public static class FieldUIElementIds
    {
        public const string MovementViewRoot = "Field.MovementViewRoot";
        public const string Background = "Field.Background";
        public const string ProgressGauge = "Field.ProgressGauge";
        public const string FormationButton = "Field.FormationButton";
        public const string TacticsButton = "Field.TacticsButton";
        public const string EncounterWarning = "Field.EncounterWarning";
        public const string BattleViewRoot = "Field.BattleViewRoot";
        // BattleAllyLayer/BattleEnemyLayer는 전투 뷰 월드 오브젝트 전환(Docs/설계/13번)으로 폐기됨 -
        // 이제 UI 마커가 아니라 BattleWorldRoot 컴포넌트(씬 루트, Canvas 밖)로 조회한다.
        public const string ResultPopup = "Field.ResultPopup";
        public const string TransitionCurtain = "Field.TransitionCurtain";
    }
}
