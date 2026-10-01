using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 순수 C# 객체 - Unity 생명주기가 필요 없어 EncounterManager가 필드로 직접 생성/소유한다.
    /// 같은 GameObject에 FixedEncounterRule이 추가되면 GetComponent&lt;IEncounterRule&gt;()로는 어느
    /// 쪽인지 구분할 수 없어 이 방식을 택했다(Docs/설계/05-2026-08-25-인카운터_판정_아키텍처.md §4).
    /// 확률은 EncounterManager가 판정마다 현재 구간 난이도로 정해 넣는다(Docs/설계/76번 §6.3).
    /// </summary>
    internal class RandomEncounterRule : IEncounterRule
    {
        public float Probability { get; set; }

        public bool ShouldTrigger()
        {
            return Random.value < Probability;
        }
    }
}
