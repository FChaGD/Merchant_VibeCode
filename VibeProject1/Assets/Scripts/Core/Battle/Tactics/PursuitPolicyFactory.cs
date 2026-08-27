using System;

namespace Game.Core
{
    /// <summary>PursuitPreset→구현체 매핑(OCP, Docs/설계/12번 §7).</summary>
    public static class PursuitPolicyFactory
    {
        public static IPursuitPolicy Create(PursuitPreset preset)
        {
            return preset switch
            {
                PursuitPreset.Autonomous => new AutonomousPursuitPolicy(),
                PursuitPreset.HuntToKill => new HuntToKillPursuitPolicy(),
                PursuitPreset.OffensiveJudgment => new OffensiveJudgmentPursuitPolicy(),
                PursuitPreset.NoPursuit => new NoPursuitPursuitPolicy(),
                PursuitPreset.HoldPosition => new HoldPositionPursuitPolicy(),
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
            };
        }
    }
}
