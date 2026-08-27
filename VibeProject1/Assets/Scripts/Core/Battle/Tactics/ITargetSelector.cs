using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 인식된 적 중 실제로 공격할 대상을 고른다(Docs/기획/12번 §3.1). BattleCharacterUnit은 이걸
    /// 매 틱 호출하지 않는다 - 타겟이 죽었거나 IPursuitPolicy가 이탈을 트리거했을 때만 스티키
    /// 타겟팅을 깨고 재호출한다(Docs/설계/12번 §4, §7 점검 이력 - 최적화).
    /// </summary>
    public interface ITargetSelector
    {
        IDamageable Select(Vector2 selfPosition, IReadOnlyList<IDamageable> recognizedCandidates);
    }
}
