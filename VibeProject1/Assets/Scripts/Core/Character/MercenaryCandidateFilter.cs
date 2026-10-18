using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 고용 후보 거르기(설계 53·81·89 §5.5) - 고용 중, 사망 기록, 후보 제외 열(이벤트 전용 캐릭터)을 뺀다. 테이블 순서 유지. 이벤트 합류(&lt;&lt;join&gt;&gt;)는 이 규칙과
    /// 무관하다. 후보 제공자(MonoBehaviour)에서 떼어 낸 이유는 규칙을 EditMode로 검증하기 위해서다.
    /// </summary>
    public static class MercenaryCandidateFilter
    {
        public static List<CharacterProfile> Filter(IReadOnlyList<CharacterProfile> all, Func<string, bool> isHired, Func<string, bool> isDeceased)
        {
            var result = new List<CharacterProfile>();
            if (all == null) return result;
            foreach (var profile in all)
            {
                if (profile.ExcludedFromHiring) continue;
                if (isHired != null && isHired(profile.CharacterId)) continue;
                if (isDeceased != null && isDeceased(profile.CharacterId)) continue;
                result.Add(profile);
            }
            return result;
        }
    }
}
