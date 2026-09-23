using System;

namespace Game.Core
{
    /// <summary>PursuitPreset→구현체 매핑(OCP, Docs/설계/12번 §7).</summary>
    public static class PursuitPolicyFactory
    {
        public static IPursuitPolicy Create(string preset)
        {
            return preset switch
            {
                "Autonomous" => new AutonomousPursuitPolicy(),
                "HuntToKill" => new HuntToKillPursuitPolicy(),
                "OffensiveJudgment" => new OffensiveJudgmentPursuitPolicy(),
                "NoPursuit" => new NoPursuitPursuitPolicy(),
                "HoldPosition" => new HoldPositionPursuitPolicy(),
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
            };
        }
    }
}
