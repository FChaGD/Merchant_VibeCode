using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 아군/적 전체 사기 게이지바 - 실제 게임 화면에는 없는 디버그 정보(요구사항 #5). PartyMorale에는
    /// 값 변경 이벤트가 없어(CurrentValue를 읽는 수밖에 없음, PartyMorale.cs 참고) 매 프레임 폴링한다.
    /// </summary>
    public class BattleTestMoraleGaugeView : MonoBehaviour
    {
        private const float MoraleMax = 100f;

        [SerializeField] private Image allyFillImage;
        [SerializeField] private Image enemyFillImage;
        [SerializeField] private BattleTestSimulationRule simulationRule;

        private float shownAlly = -1f;
        private float shownEnemy = -1f;

        // 사기 값은 이벤트가 없어 매 프레임 읽지만, 값이 바뀐 프레임에만 다시 그린다(Docs/Refactor/2026-10-26_씬전용.md I-4, FO-3).
        private void Update()
        {
            if (simulationRule == null) return;

            var ally = simulationRule.AllyMoraleValue;
            var enemy = simulationRule.EnemyMoraleValue;
            if (Mathf.Approximately(ally, shownAlly) && Mathf.Approximately(enemy, shownEnemy)) return;
            shownAlly = ally;
            shownEnemy = enemy;
            allyFillImage.fillAmount = ally / MoraleMax;
            enemyFillImage.fillAmount = enemy / MoraleMax;
        }
    }
}
