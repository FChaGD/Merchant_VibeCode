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

        private void Update()
        {
            if (simulationRule == null) return;

            allyFillImage.fillAmount = simulationRule.AllyMoraleValue / MoraleMax;
            enemyFillImage.fillAmount = simulationRule.EnemyMoraleValue / MoraleMax;
        }
    }
}
