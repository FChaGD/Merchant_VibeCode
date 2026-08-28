using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 활동 반경 안에서 전투 중 실제로 이동하는 목적지를 계산한다(Docs/기획/12번 §3.2). 반환값은
    /// "이 지점을 향해 이동하라"는 목적지 좌표다 - selfPosition을 그대로 반환하면 "이동 없음"을
    /// 뜻한다. ISelfPreservationModifier가 덮어쓰지 않을 때만 이 결과를 쓴다(Docs/설계/12번 §4).
    /// </summary>
    public interface IPositioningStrategy
    {
        // self는 Blocking/Surround(코디네이터 기반 전략, Docs/설계/12번 §12/§13)가 "내 슬롯/각도가
        // 뭔지" 물어봐야 해서 추가됐다. sameSideUnits는 Disperse(산개, §13.2)가 동료 원거리딜러와의
        // 거리를 계산해야 해서 추가됐다 - 나머지 구현체는 각자 필요 없는 매개변수를 무시한다(OCP,
        // HoldPosition의 ClampDestination과 같은 성격의 no-op 허용).
        Vector2 ComputeMoveTarget(
            IBattleCombatant self, Vector2 selfPosition, IDamageable target, float range, Vector2 homePosition,
            IReadOnlyList<IBattleCombatant> sameSideUnits);
    }
}
