using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// ② 회수 적재(설계 79번 §7). 승리 후 임시보관에 회수 물품이 있을 때만 실행한다. 패널은 완료 콜백만 부르고 닫기는 이 단계가
    /// UIManager.Close로 한다(UI 패널 규칙). 패널이 없으면(인스톨러 미실행 - 배선이 경고를 냄) 건너뛴다 - 회수 물품은 임시보관에 남는다.
    /// </summary>
    internal sealed class CargoRecoveryStep : IBattleAftermathStep
    {
        private readonly IUIManager uiManager;
        private readonly ITradeGoodsCargoSettlement settlement;
        private readonly FieldCargoRecoveryPanel panel;

        public CargoRecoveryStep(IUIManager uiManager, ITradeGoodsCargoSettlement settlement, FieldCargoRecoveryPanel panel)
        {
            this.uiManager = uiManager;
            this.settlement = settlement;
            this.panel = panel;
        }

        public bool AppliesTo(BattleAftermathContext context)
            => context.IsVictory && context.Summary.Recoverable > 0 && settlement != null && panel != null && settlement.StagedItems.Count > 0;

        public void Run(BattleAftermathContext context, Action onDone)
        {
            if (panel == null)
            {
                Debug.LogWarning($"{nameof(CargoRecoveryStep)}: 회수 적재 패널이 없어 단계를 건너뛴다.");
                onDone();
                return;
            }

            panel.SetCompletion(() =>
            {
                uiManager.Close(UIPanelIds.CargoRecovery);
                onDone();
            });
            uiManager.Open(UIPanelIds.CargoRecovery);
        }
    }
}
