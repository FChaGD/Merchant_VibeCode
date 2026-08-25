using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 승패 판정을 IBattleResultRule 전략에 위임한다(OCP 확장점) - 실제 전투 로직이 생겨도
    /// PlaceholderBattleResultRule 교체만으로 끝나고 BattleManager는 무변경으로 유지된다
    /// (Docs/설계/04_Field씬_아키텍처.md §6). BattleManager와 같은 GameObject에 부착해
    /// GetComponent&lt;IBattleResultEvaluator&gt;()로 조회된다.
    /// </summary>
    public class BattleResultEvaluator : MonoBehaviour, IBattleResultEvaluator
    {
        private IBattleResultRule resultRule;

        private void Awake()
        {
            resultRule = GetComponent<IBattleResultRule>();
            if (resultRule == null)
            {
                throw new InvalidOperationException($"{nameof(BattleResultEvaluator)}와 같은 GameObject에 {nameof(IBattleResultRule)} 구현체가 없다.");
            }
        }

        public void Evaluate(Action<BattleResult> onResult)
        {
            resultRule.Evaluate(onResult);
        }
    }
}
