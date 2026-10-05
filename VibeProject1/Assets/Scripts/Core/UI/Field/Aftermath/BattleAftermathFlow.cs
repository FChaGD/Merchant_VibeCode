using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 결과 정리 단계(①팝업 ②회수 적재 ③대열 정리 ④마무리)를 순서대로 돌린다(설계 79번 §6). 조율자(FieldEncounterFlowCoordinator)는
    /// "패배 결과 판정 → 정산 반영 → 플로우 시작"만 하고 단계 진행은 여기서 맡는다(SRP). 단계가 이번 Field 씬의 화면 참조를 쥐므로
    /// 조율자가 씬을 로드할 때마다 새로 만든다 - 플로우는 ④에서 씬 전환·종료로 끝나 씬 재로드를 가로질러 진행되지 않는다.
    /// </summary>
    internal sealed class BattleAftermathFlow
    {
        private readonly IReadOnlyList<IBattleAftermathStep> steps;

        public BattleAftermathFlow(IReadOnlyList<IBattleAftermathStep> steps)
        {
            this.steps = steps;
        }

        public void Start(BattleAftermathContext context) => RunFrom(0, context);

        /// <summary>step 뒤에 실행될 단계가 하나라도 있는지 - ① 결과 팝업의 버튼 문구용(설계 79번 §6.1).</summary>
        public bool HasStepAfter(IBattleAftermathStep step, BattleAftermathContext context)
        {
            for (var i = IndexOf(step) + 1; i < steps.Count; i++)
            {
                if (steps[i].AppliesTo(context)) return true;
            }
            return false;
        }

        private void RunFrom(int index, BattleAftermathContext context)
        {
            for (var i = index; i < steps.Count; i++)
            {
                if (!steps[i].AppliesTo(context)) continue;

                // 버튼 연타 등으로 완료 콜백이 두 번 불려도 다음 단계가 중복 실행되지 않게 막는다.
                var next = i + 1;
                var done = false;
                steps[i].Run(context, () =>
                {
                    if (done) return;
                    done = true;
                    RunFrom(next, context);
                });
                return;
            }
        }

        private int IndexOf(IBattleAftermathStep step)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                if (ReferenceEquals(steps[i], step)) return i;
            }
            throw new ArgumentException($"{nameof(BattleAftermathFlow)}에 등록되지 않은 단계다: {step?.GetType().Name}", nameof(step));
        }
    }
}
