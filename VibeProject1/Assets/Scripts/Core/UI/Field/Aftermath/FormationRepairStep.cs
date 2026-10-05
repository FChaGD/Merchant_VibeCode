using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// ③ 대열 정리(설계 79번 §8). 상행을 이어 가는 결과(승리·도주)에서 정산 후 대열이 끊어졌을 때만 실행한다 - 궤주·포로·사망은
    /// 대열을 다시 쓸 일이 없다. 정비창은 전투 뷰 위에서 열리고(§15-7), 닫힌 뒤에야 ④ 슬라이드가 시작된다.
    /// </summary>
    internal sealed class FormationRepairStep : IBattleAftermathStep
    {
        private readonly IUIManager uiManager;
        private readonly IFormationRepairMode repairMode;

        public FormationRepairStep(IUIManager uiManager, IFormationRepairMode repairMode)
        {
            this.uiManager = uiManager;
            this.repairMode = repairMode;
        }

        public bool AppliesTo(BattleAftermathContext context)
            => context.ContinuesTrip && context.Summary.FormationDisconnected && repairMode != null && repairMode.CanRepair;

        public void Run(BattleAftermathContext context, Action onDone)
        {
            if (repairMode == null)
            {
                Debug.LogWarning($"{nameof(FormationRepairStep)}: {nameof(IFormationRepairMode)}가 없어 대열 정리 단계를 건너뛴다.");
                onDone();
                return;
            }

            uiManager.Open(UIPanelIds.Formation);
            repairMode.BeginRepair(() =>
            {
                uiManager.Close(UIPanelIds.Formation);
                onDone();
            });
        }
    }
}
