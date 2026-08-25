using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 기획 08번 문서 §10.1 테스트 확률(도주30/궤주35/포로25/사망10). 파티 전체에 대한 1회성 판정이라
    /// PlaceholderBattleResultRule과 달리 지연 없이 즉시 값을 반환한다(코루틴 불필요).
    /// </summary>
    public class PlaceholderDefeatConsequenceRule : MonoBehaviour, IDefeatConsequenceRule
    {
        private const float FleeProbability = 0.30f;
        private const float RoutProbability = 0.35f;
        private const float CapturedProbability = 0.25f;
        // 나머지(0.10)는 Death

        public DefeatConsequence Resolve()
        {
            var roll = Random.value;
            if (roll < FleeProbability) return DefeatConsequence.Flee;
            if (roll < FleeProbability + RoutProbability) return DefeatConsequence.Rout;
            if (roll < FleeProbability + RoutProbability + CapturedProbability) return DefeatConsequence.Captured;
            return DefeatConsequence.Death;
        }
    }
}
