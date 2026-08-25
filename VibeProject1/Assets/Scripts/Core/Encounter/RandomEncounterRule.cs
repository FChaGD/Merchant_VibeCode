using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 순수 C# 객체 - Unity 생명주기가 필요 없어 EncounterManager가 필드로 직접 생성/소유한다.
    /// 같은 GameObject에 FixedEncounterRule이 추가되면 GetComponent&lt;IEncounterRule&gt;()로는 어느
    /// 쪽인지 구분할 수 없어 이 방식을 택했다(Docs/설계/05_인카운터_판정_아키텍처.md §4).
    /// 확률은 테스트 단계 추정치이며 실제 밸런싱 전까지 유지(Docs/기획/07_인카운터_판정_기획.md §3).
    /// </summary>
    internal class RandomEncounterRule : IEncounterRule
    {
        private const float TriggerProbability = 0.3f;

        public bool ShouldTrigger()
        {
            return Random.value < TriggerProbability;
        }
    }
}
