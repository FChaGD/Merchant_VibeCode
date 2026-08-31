namespace Game.Core
{
    /// <summary>기획 08번 문서 §7.2/§7.5 - PartyMorale과 UnitMorale이 완전히 같은 임계치를 공유한다
    /// (사용자 확정, 설계 14번 §4). 임계치를 두 곳에 따로 하드코딩하지 않기 위한 단일 확장 메서드.</summary>
    public enum MoraleTier
    {
        Low,
        Normal,
        High,
    }

    public static class MoraleTierExtensions
    {
        public static MoraleTier ToMoraleTier(this float moraleValue) => moraleValue switch
        {
            >= MoraleTuning.HighTierThreshold => MoraleTier.High,
            < MoraleTuning.LowTierThreshold => MoraleTier.Low,
            _ => MoraleTier.Normal,
        };
    }
}
