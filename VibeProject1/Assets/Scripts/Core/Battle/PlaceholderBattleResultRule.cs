using System;
using System.Collections;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 실제 전투 시스템 설계 후 대체/제거 대상 - 1초 후 70% 확률 승리/30% 확률 패배로 판정한다
    /// (Docs/설계/04_Field씬_아키텍처.md §6). BattleResultEvaluator와 같은 GameObject에 부착해
    /// GetComponent&lt;IBattleResultRule&gt;()로 조회된다.
    /// </summary>
    public class PlaceholderBattleResultRule : MonoBehaviour, IBattleResultRule
    {
        private const float ResultDelaySeconds = 1f;
        private const float VictoryProbability = 0.7f;

        public void Evaluate(Action<BattleResult> onResult)
        {
            StartCoroutine(EvaluateAfterDelay(onResult));
        }

        private IEnumerator EvaluateAfterDelay(Action<BattleResult> onResult)
        {
            yield return new WaitForSeconds(ResultDelaySeconds);
            var outcome = UnityEngine.Random.value < VictoryProbability ? BattleOutcome.Victory : BattleOutcome.Defeat;
            onResult(new BattleResult(outcome));
        }
    }
}
