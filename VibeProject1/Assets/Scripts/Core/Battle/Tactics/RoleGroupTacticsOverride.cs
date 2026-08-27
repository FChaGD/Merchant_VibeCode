namespace Game.Core
{
    /// <summary>
    /// 역할군 단위의 방향성 지시 3축. TargetPriority/Positioning/SelfPreservation은 override 여부와
    /// 무관하게 항상 구체적인 값을 갖는다(초기값 = RoleGroupTacticsCatalogAsset의 후보 목록 첫 항목).
    /// IsOverridden은 UI 표시 전용 메타정보("상단 지침 ⇄ 직접 지정" 토글 상태)이고, 시뮬레이션은
    /// 이 필드를 참조하지 않는다 - 상단 지침(파티 축)과 역할군 축은 겹치는 축이 없어 override
    /// 관계 자체가 없기 때문이다(Docs/설계/12번 §6-1, 개체별 방향성이 생기면 그때 실제 override
    /// 관계가 생길 예정이라 필드는 미리 남겨둔다).
    /// </summary>
    public readonly struct RoleGroupTacticsOverride
    {
        public bool IsOverridden { get; }
        public TargetPriority TargetPriority { get; }
        public LocalPositioning Positioning { get; }
        public SelfPreservation SelfPreservation { get; }

        public RoleGroupTacticsOverride(bool isOverridden, TargetPriority targetPriority, LocalPositioning positioning, SelfPreservation selfPreservation)
        {
            IsOverridden = isOverridden;
            TargetPriority = targetPriority;
            Positioning = positioning;
            SelfPreservation = selfPreservation;
        }
    }
}
